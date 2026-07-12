using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.Diagnostics;

/// <summary>
/// Scoped implementation of <see cref="IRecognitionForensicsAudit"/> (AI17.RUNTIME). See the interface
/// remarks for why this is a separate class from <see cref="RecognitionPipelineDiagnostics"/> rather
/// than an extension of it. Registered Scoped — one instance per DI scope, exactly like
/// <see cref="RecognitionPipelineDiagnostics"/> and <c>RecognitionExecutionContext</c> — so all the
/// per-job running state below (open-object tracking, peak-so-far, previous-checkpoint memory) is
/// naturally isolated per job with no explicit reset step required.
/// </summary>
public sealed class RecognitionForensicsAudit : IRecognitionForensicsAudit
{
    // Exact thresholds specified by the AI17.RUNTIME.1/.6 prompts — not configurable, because the
    // prompts specify them as fixed diagnostic trip-wires ("more than 25 MB" / "more than 30 MB"),
    // not tunable business behavior.
    private const long StageSpikeThresholdBytes = 25L * 1024 * 1024;
    private const long MatchingSpikeThresholdBytes = 30L * 1024 * 1024;

    // A disposable object still alive more than this long after its own creation is flagged
    // "LONG LIVED DISPOSABLE" regardless of which stage eventually disposes it — independent of the
    // per-checkpoint Working Set spike threshold above, which measures process-wide memory growth,
    // not one object's own lifetime.
    private const long LongLivedDisposableThresholdMs = 2000;

    private readonly RecognitionDiagnosticsOptions _options;
    private readonly IRecognitionExecutionContext _executionContext;
    private readonly ILogger<RecognitionForensicsAudit> _logger;

    // AI17.RUNTIME: InsightFaceEngine's DetectFaces/ExtractEmbedding/DetectAsync helpers are SHARED
    // between the classroom recognition pipeline and the unrelated student face-enrollment pipeline
    // (InsightFaceEmbeddingGenerator.GenerateSingleFaceEmbedding) and the debug FaceDetectionController
    // endpoint — neither of which ever calls Checkpoint(). Every method below except Checkpoint itself
    // is a no-op until the classroom pipeline's very first checkpoint ("Queue Received") has fired,
    // mirroring the exact "inert until Begin()" pattern IRecognitionPipelineDiagnostics already uses
    // for the same reason (see that interface's remarks) — this is what keeps AI17's new logging out of
    // the student-enrollment and debug-detection code paths entirely.
    private bool _active;

    private string _currentStage = "(none)";
    private long _peakWorkingSetSoFarBytes;
    private long? _previousCheckpointWorkingSetBytes;
    private string? _previousCheckpointStage;

    private int _studentEmbeddingLoadCount;

    private sealed class OpenObjectRecord
    {
        public required string ObjectType { get; init; }
        public required string CreationStage { get; init; }
        public required DateTime CreatedUtc { get; init; }
    }

    private readonly Dictionary<string, OpenObjectRecord> _openObjects = new();
    private int _totalObjectsCreated;
    private int _totalObjectsDisposed;
    private int _currentOpenImageCount;
    private int _peakConcurrentImageCount;
    private int _currentSourceImageCount;

    public RecognitionForensicsAudit(
        IOptions<RecognitionDiagnosticsOptions> options,
        IRecognitionExecutionContext executionContext,
        ILogger<RecognitionForensicsAudit> logger)
    {
        _options = options.Value;
        _executionContext = executionContext;
        _logger = logger;
    }

    public void Checkpoint(string stageName, int? faceNumber = null)
    {
        if (!_options.Enabled)
        {
            return;
        }

        try
        {
            _active = true;
            _currentStage = faceNumber.HasValue ? $"{stageName} (Face {faceNumber})" : stageName;
            var snapshot = RecognitionMemorySnapshot.Capture();
            var elapsedMs = ExecutionTraceLog.ElapsedSinceQueueMs(_executionContext);

            if (snapshot.WorkingSetBytes > _peakWorkingSetSoFarBytes)
            {
                _peakWorkingSetSoFarBytes = snapshot.WorkingSetBytes;
            }

            _logger.LogInformation("====================================================");
            _logger.LogInformation("AI17 STAGE CHECKPOINT");
            _logger.LogInformation("  Stage Name                         : {StageName}", _currentStage);
            _logger.LogInformation("  Execution Trace Id                 : {TraceId}", ExecutionTraceLog.FormatTraceId(_executionContext));
            _logger.LogInformation("  Working Set MB                     : {WorkingSetMB} MB", snapshot.WorkingSetMegabytes);
            _logger.LogInformation("  Private Memory MB                  : {PrivateMemoryMB} MB", snapshot.PrivateMegabytes);
            _logger.LogInformation("  Managed Heap MB                     : {ManagedHeapMB} MB", snapshot.ManagedHeapMegabytes);
            _logger.LogInformation("  Native Estimate MB                 : {NativeEstimateMB} MB", snapshot.NativeEstimateMegabytes);
            _logger.LogInformation("  Gen0                               : {Gen0}", snapshot.Gen0Collections);
            _logger.LogInformation("  Gen1                               : {Gen1}", snapshot.Gen1Collections);
            _logger.LogInformation("  Gen2                               : {Gen2}", snapshot.Gen2Collections);
            _logger.LogInformation("  Current Process Threads            : {ThreadCount}", snapshot.ProcessThreadCount);
            _logger.LogInformation("  Elapsed Time                       : {ElapsedMs} ms", elapsedMs);
            _logger.LogInformation("  Peak Memory So Far                 : {PeakMB} MB", ToMb(_peakWorkingSetSoFarBytes));
            _logger.LogInformation("====================================================");

            if (_previousCheckpointWorkingSetBytes.HasValue)
            {
                var delta = snapshot.WorkingSetBytes - _previousCheckpointWorkingSetBytes.Value;
                if (delta > StageSpikeThresholdBytes)
                {
                    _logger.LogWarning(
                        "MEMORY SPIKE DETECTED: Working Set grew by {DeltaMB} MB between checkpoints '{PreviousStage}' -> '{CurrentStage}' (threshold 25 MB).",
                        ToMb(delta),
                        _previousCheckpointStage,
                        _currentStage);
                }
            }

            _previousCheckpointWorkingSetBytes = snapshot.WorkingSetBytes;
            _previousCheckpointStage = _currentStage;
        }
        catch (Exception ex)
        {
            SafeLogInternalFailure(ex);
        }
    }

    public void ObjectCreated(
        string objectType,
        string detail,
        int? width = null,
        int? height = null,
        string? pixelFormat = null,
        long? estimatedBytes = null)
    {
        if (!_options.Enabled || !_active)
        {
            return;
        }

        try
        {
            var key = $"{objectType}:{detail}";
            _openObjects[key] = new OpenObjectRecord
            {
                ObjectType = objectType,
                CreationStage = _currentStage,
                CreatedUtc = DateTime.UtcNow,
            };
            _totalObjectsCreated++;

            if (IsImageObject(objectType))
            {
                _currentOpenImageCount++;
                if (_currentOpenImageCount > _peakConcurrentImageCount)
                {
                    _peakConcurrentImageCount = _currentOpenImageCount;
                }

                if (detail.Contains("source image", StringComparison.OrdinalIgnoreCase))
                {
                    _currentSourceImageCount++;
                    if (_currentSourceImageCount > 1)
                    {
                        _logger.LogWarning(
                            "WARNING: Multiple classroom images resident. {Count} source classroom images are open simultaneously at stage '{Stage}'.",
                            _currentSourceImageCount,
                            _currentStage);
                    }
                }
            }

            _logger.LogInformation(
                "AI17 Object Created: {ObjectType} ({Detail}) at stage '{Stage}'{DimensionsSuffix}{PixelFormatSuffix}{EstimatedBytesSuffix}",
                objectType,
                detail,
                _currentStage,
                width.HasValue && height.HasValue ? $" [{width}x{height}]" : string.Empty,
                pixelFormat is not null ? $" PixelFormat={pixelFormat}" : string.Empty,
                estimatedBytes.HasValue ? $" EstimatedBytes={estimatedBytes.Value}" : string.Empty);
        }
        catch (Exception ex)
        {
            SafeLogInternalFailure(ex);
        }
    }

    public void ObjectDisposed(string objectType, string detail)
    {
        if (!_options.Enabled || !_active)
        {
            return;
        }

        try
        {
            var key = $"{objectType}:{detail}";
            var disposalStage = _currentStage;

            if (_openObjects.Remove(key, out var record))
            {
                var lifetimeMs = Math.Round((DateTime.UtcNow - record.CreatedUtc).TotalMilliseconds, 1);
                _totalObjectsDisposed++;

                if (IsImageObject(objectType))
                {
                    _currentOpenImageCount = Math.Max(0, _currentOpenImageCount - 1);
                    if (detail.Contains("source image", StringComparison.OrdinalIgnoreCase))
                    {
                        _currentSourceImageCount = Math.Max(0, _currentSourceImageCount - 1);
                    }
                }

                _logger.LogInformation(
                    "AI17 Disposable Audit: {ObjectType} ({Detail}) | Creation Stage={CreationStage} | Disposal Stage={DisposalStage} | Disposed Successfully=True | Elapsed Lifetime={LifetimeMs} ms",
                    objectType,
                    detail,
                    record.CreationStage,
                    disposalStage,
                    lifetimeMs);

                if (lifetimeMs > LongLivedDisposableThresholdMs)
                {
                    _logger.LogWarning(
                        "LONG LIVED DISPOSABLE: {ObjectType} ({Detail}) lived {LifetimeMs} ms (created at '{CreationStage}', disposed at '{DisposalStage}'; threshold {ThresholdMs} ms).",
                        objectType,
                        detail,
                        lifetimeMs,
                        record.CreationStage,
                        disposalStage,
                        LongLivedDisposableThresholdMs);
                }
            }
            else
            {
                _logger.LogInformation(
                    "AI17 Disposable Audit: {ObjectType} ({Detail}) disposed at '{DisposalStage}' — no matching creation record on this instance (created before AI17 forensics tracking began, or already disposed once).",
                    objectType,
                    detail,
                    disposalStage);
            }
        }
        catch (Exception ex)
        {
            SafeLogInternalFailure(ex);
        }
    }

    public void CheckFaceCropRetainedAfterEmbedding(string detail)
    {
        if (!_options.Enabled || !_active)
        {
            return;
        }

        try
        {
            var key = $"ImageSharp Image:{detail}";
            if (_openObjects.ContainsKey(key))
            {
                _logger.LogWarning(
                    "WARNING: Face crop retained. '{Detail}' is still open immediately after embedding generation.",
                    detail);
            }
        }
        catch (Exception ex)
        {
            SafeLogInternalFailure(ex);
        }
    }

    public void RecordStudentEmbeddingLoad(
        int studentCount,
        int embeddingCount,
        int totalEmbeddingFloats,
        bool asNoTracking,
        string navigationPropertiesLoaded,
        bool lazyLoadingEnabled)
    {
        if (!_options.Enabled || !_active)
        {
            return;
        }

        try
        {
            _studentEmbeddingLoadCount++;

            var totalEmbeddingBytes = (long)totalEmbeddingFloats * sizeof(float);
            var averageEmbeddingSize = embeddingCount > 0 ? totalEmbeddingFloats / (double)embeddingCount : 0d;
            // studentIds list + materialized StudentFaceEmbedding entities + mapped StudentEmbeddingMatchInput DTOs.
            var totalMaterializedObjects = studentCount + (embeddingCount * 2);
            // Rough estimate only: embedding payload bytes plus ~64 bytes/object for entity/DTO/list-node overhead.
            var collectionAllocationEstimateBytes = totalEmbeddingBytes + (totalMaterializedObjects * 64L);

            _logger.LogInformation("====================================================");
            _logger.LogInformation("AI17 STUDENT EMBEDDING LOAD AUDIT");
            _logger.LogInformation("  Number Of Students Loaded          : {StudentCount}", studentCount);
            _logger.LogInformation("  Number Of Embeddings                : {EmbeddingCount}", embeddingCount);
            _logger.LogInformation("  Average Embedding Size              : {AverageEmbeddingSize:F1} floats", averageEmbeddingSize);
            _logger.LogInformation(
                "  Total Embedding Bytes               : {TotalEmbeddingBytes} bytes ({TotalEmbeddingMB} MB)",
                totalEmbeddingBytes,
                Math.Round(totalEmbeddingBytes / (1024d * 1024d), 3));
            _logger.LogInformation("  EF Tracking Enabled                 : {TrackingEnabled}", !asNoTracking);
            _logger.LogInformation("  AsTracking                          : {AsTracking}", !asNoTracking);
            _logger.LogInformation("  AsNoTracking                        : {AsNoTracking}", asNoTracking);
            _logger.LogInformation("  Navigation Properties Loaded        : {NavigationProperties}", navigationPropertiesLoaded);
            _logger.LogInformation("  Lazy Loading                        : {LazyLoading}", lazyLoadingEnabled);
            _logger.LogInformation("  Total Materialized Objects           : {TotalMaterializedObjects}", totalMaterializedObjects);
            _logger.LogInformation(
                "  Collection Allocation Estimate      : {EstimateBytes} bytes ({EstimateMB} MB) [estimate]",
                collectionAllocationEstimateBytes,
                Math.Round(collectionAllocationEstimateBytes / (1024d * 1024d), 3));
            _logger.LogInformation("====================================================");

            if (_studentEmbeddingLoadCount > 1)
            {
                _logger.LogWarning(
                    "DUPLICATE LOAD DETECTED: Student embeddings have been loaded {LoadCount} times in this job (expected exactly once).",
                    _studentEmbeddingLoadCount);
            }
        }
        catch (Exception ex)
        {
            SafeLogInternalFailure(ex);
        }
    }

    public void RecordOnnxInference(
        string model,
        string inputTensorShape,
        string outputTensorShape,
        long inferenceDurationMs,
        RecognitionMemorySnapshot before,
        RecognitionMemorySnapshot after,
        bool inferenceSessionReused,
        bool tensorReused,
        int disposableOutputCount)
    {
        if (!_options.Enabled || !_active)
        {
            return;
        }

        try
        {
            var nativeDeltaBytes = after.NativeEstimateBytes - before.NativeEstimateBytes;
            var workingSetDeltaBytes = after.WorkingSetBytes - before.WorkingSetBytes;

            _logger.LogInformation("====================================================");
            _logger.LogInformation("AI17 ONNX RUNTIME INFERENCE AUDIT");
            _logger.LogInformation("  Model                               : {Model}", model);
            _logger.LogInformation("  Input Tensor Shape                  : {InputShape}", inputTensorShape);
            _logger.LogInformation("  Output Tensor Shape                 : {OutputShape}", outputTensorShape);
            _logger.LogInformation("  Inference Duration                  : {DurationMs} ms", inferenceDurationMs);
            _logger.LogInformation("  Native Memory Before                : {BeforeMB} MB", before.NativeEstimateMegabytes);
            _logger.LogInformation("  Native Memory After                 : {AfterMB} MB", after.NativeEstimateMegabytes);
            _logger.LogInformation("  Working Set Delta                   : {DeltaMB} MB", ToMb(workingSetDeltaBytes));
            _logger.LogInformation("  Disposable Outputs Count             : {OutputCount}", disposableOutputCount);
            // Always True under current code: every session.Run(...) call site audited here is wrapped
            // in `using var outputs = session.Run(inputs);`, so by the time this method is called (right
            // after that using scope ends) disposal has already unconditionally happened.
            _logger.LogInformation("  Disposed Outputs                    : {DisposedOutputs}", true);
            _logger.LogInformation("  Inference Session Reused             : {SessionReused}", inferenceSessionReused);
            _logger.LogInformation("  Tensor Reused                       : {TensorReused}", tensorReused);
            _logger.LogInformation("====================================================");

            if (nativeDeltaBytes > 0)
            {
                _logger.LogWarning(
                    "NATIVE MEMORY GROWTH DETECTED: {Model} inference — native estimate grew by {DeltaMB} MB (before={BeforeMB} MB, after={AfterMB} MB).",
                    model,
                    ToMb(nativeDeltaBytes),
                    before.NativeEstimateMegabytes,
                    after.NativeEstimateMegabytes);
            }
        }
        catch (Exception ex)
        {
            SafeLogInternalFailure(ex);
        }
    }

    public void RecordMatching(
        int detectedFaceCount,
        int candidateStudentCount,
        RecognitionMemorySnapshot before,
        RecognitionMemorySnapshot after)
    {
        if (!_options.Enabled || !_active)
        {
            return;
        }

        try
        {
            var comparisons = (long)detectedFaceCount * candidateStudentCount;
            var deltaBytes = after.WorkingSetBytes - before.WorkingSetBytes;
            // FaceMatcher.Match is one synchronous, non-yielding call — there is no intermediate point
            // to sample memory from without a concurrent polling thread (itself extra
            // complexity/overhead for a diagnostic), so "peak during" is approximated as the higher of
            // the before/after readings. Documented as an approximation, not measured mid-call.
            var peakDuringBytes = Math.Max(before.WorkingSetBytes, after.WorkingSetBytes);

            _logger.LogInformation("====================================================");
            _logger.LogInformation("AI17 CANDIDATE MATCHING MEMORY AUDIT");
            _logger.LogInformation("  Number Of Detected Faces            : {DetectedFaces}", detectedFaceCount);
            _logger.LogInformation("  Number Of Candidate Students        : {CandidateStudents}", candidateStudentCount);
            _logger.LogInformation("  Candidate Embeddings Loaded          : {CandidateStudents}", candidateStudentCount);
            _logger.LogInformation("  Embedding Comparisons Performed      : {Comparisons}", comparisons);
            _logger.LogInformation("  Maximum Comparison Buffer            : {MaxBufferBytes} bytes [estimate — see remarks]", detectedFaceCount * 8L);
            _logger.LogInformation("  Temporary Allocations                : {TempAllocBytes} bytes [estimate — one FaceMatchResultDto per face]", detectedFaceCount * 96L);
            _logger.LogInformation("  Working Set Before Matching          : {BeforeMB} MB", before.WorkingSetMegabytes);
            _logger.LogInformation("  Working Set After Matching           : {AfterMB} MB", after.WorkingSetMegabytes);
            _logger.LogInformation("  Peak During Matching (approx.)       : {PeakMB} MB", ToMb(peakDuringBytes));
            _logger.LogInformation("====================================================");

            if (deltaBytes > MatchingSpikeThresholdBytes)
            {
                _logger.LogWarning(
                    "MATCHING MEMORY SPIKE: Working Set grew by {DeltaMB} MB during matching (threshold 30 MB).",
                    ToMb(deltaBytes));
            }
        }
        catch (Exception ex)
        {
            SafeLogInternalFailure(ex);
        }
    }

    public void FinalizeAudit()
    {
        if (!_options.Enabled || !_active)
        {
            return;
        }

        try
        {
            _logger.LogInformation("====================================================");
            _logger.LogInformation("AI17 FORENSICS AUDIT SUMMARY");
            _logger.LogInformation("  Total Disposable Objects Created     : {Created}", _totalObjectsCreated);
            _logger.LogInformation("  Total Disposable Objects Disposed    : {Disposed}", _totalObjectsDisposed);
            _logger.LogInformation("  Objects Never Disposed              : {NeverDisposed}", _openObjects.Count);
            _logger.LogInformation("  Peak Concurrent ImageSharp Images     : {PeakImages}", _peakConcurrentImageCount);
            _logger.LogInformation("  Student Embedding Loads This Job      : {LoadCount}", _studentEmbeddingLoadCount);
            _logger.LogInformation("====================================================");

            foreach (var (key, record) in _openObjects)
            {
                var lifetimeMs = Math.Round((DateTime.UtcNow - record.CreatedUtc).TotalMilliseconds, 1);
                var isOnnxRelated = record.ObjectType.Contains("Onnx", StringComparison.OrdinalIgnoreCase);
                var label = isOnnxRelated ? "UNDISPOSED ONNX OUTPUT" : "LONG LIVED DISPOSABLE";

                _logger.LogWarning(
                    "{Label}: {ObjectKey} was created at stage '{CreationStage}' and never disposed by job completion (alive {LifetimeMs} ms).",
                    label,
                    key,
                    record.CreationStage,
                    lifetimeMs);
            }
        }
        catch (Exception ex)
        {
            SafeLogInternalFailure(ex);
        }
    }

    private static bool IsImageObject(string objectType) =>
        objectType.Contains("Image", StringComparison.OrdinalIgnoreCase);

    private static double ToMb(long bytes) => Math.Round(bytes / (1024d * 1024d), 1);

    private void SafeLogInternalFailure(Exception ex)
    {
        try
        {
            _logger.LogWarning(
                ex,
                "RecognitionForensicsAudit internal failure — diagnostics suppressed for this event; the recognition pipeline itself is unaffected.");
        }
        catch
        {
            // A logging failure must never propagate out of a diagnostics component.
        }
    }
}
