using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.Diagnostics.MemoryAudit;

/// <summary>
/// Scoped implementation of <see cref="IRecognitionMemoryAudit"/> (AI18.MEMORY.1). See the interface
/// remarks for why this is a separate class from <see cref="RecognitionPipelineDiagnostics"/> (AI15/AI16)
/// and <see cref="RecognitionForensicsAudit"/> (AI17.RUNTIME). Registered Scoped — one instance per DI
/// scope (one recognition job) — so every field below is naturally isolated per job with no explicit
/// reset step required. No static mutable state anywhere in this class.
/// </summary>
public sealed class RecognitionMemoryAudit : IRecognitionMemoryAudit
{
    private readonly RecognitionDiagnosticsOptions _options;
    private readonly IRecognitionExecutionContext _executionContext;
    private readonly ILogger<RecognitionMemoryAudit> _logger;

    // Inert until Begin() is called, per the AI18.MEMORY.1 prompt ("completely inert until Begin()").
    private bool _active;
    private Stopwatch? _jobStopwatch;
    private string _traceId = string.Empty;
    private string _currentStage = "(none)";

    private MemoryAuditSnapshot? _beginSnapshot;
    private MemoryAuditSnapshot? _previousSnapshot;

    // Running "peak so far" state — the four independent peaks STEP 2 requires on every snapshot.
    private long _peakWorkingSetBytes;
    private long _peakPrivateMemoryBytes;
    private long _peakManagedHeapBytes;
    private long _peakNativeEstimateBytes;

    // STEP 10/12 — largest-observed trackers, keyed by category. A single dictionary avoids two dozen
    // near-identical (long Bytes, string Label) field pairs while still answering every "Largest ___"
    // question the final report (STEP 12) asks for.
    private readonly Dictionary<string, (long Bytes, string Label)> _largest = new();

    private sealed class TrackedObject
    {
        public required long Id { get; init; }
        public required string ObjectType { get; init; }
        public required long ApproximateBytes { get; init; }
        public required string CreatedStage { get; init; }
        public required DateTime CreatedUtc { get; init; }
        public string? DisposedStage { get; set; }
        public DateTime? DisposedUtc { get; set; }
    }

    private readonly Dictionary<long, TrackedObject> _trackedObjects = new();
    private long _nextObjectId;
    private int _totalObjectsRegistered;
    private int _totalObjectsDisposed;

    private int _efQueryCount;
    private int _studentEmbeddingLoadCount;
    private int _onnxInferenceCount;
    private int _matchingCallCount;

    public RecognitionMemoryAudit(
        IOptions<RecognitionDiagnosticsOptions> options,
        IRecognitionExecutionContext executionContext,
        ILogger<RecognitionMemoryAudit> logger)
    {
        _options = options.Value;
        _executionContext = executionContext;
        _logger = logger;
    }

    public void Begin()
    {
        if (!_options.Enabled)
        {
            return;
        }

        try
        {
            _active = true;
            _jobStopwatch = Stopwatch.StartNew();
            _traceId = ExecutionTraceLog.FormatTraceId(_executionContext);
            _currentStage = "Pipeline Begin";

            var snapshot = CaptureSnapshot(_currentStage);
            _beginSnapshot = snapshot;
            _previousSnapshot = snapshot;

            _logger.LogInformation("====================================================");
            _logger.LogInformation("AI18 MEMORY AUDIT — BEGIN");
            _logger.LogInformation("  Execution Trace Id                 : {TraceId}", _traceId);
            LogSnapshotBody(snapshot);
            _logger.LogInformation("====================================================");
        }
        catch (Exception ex)
        {
            SafeLogInternalFailure(ex);
        }
    }

    public void Snapshot(string stage, int? faceNumber = null)
    {
        if (!_options.Enabled || !_active)
        {
            return;
        }

        try
        {
            _currentStage = faceNumber.HasValue ? $"{stage} (Face {faceNumber})" : stage;
            var snapshot = CaptureSnapshot(_currentStage);

            _logger.LogInformation("====================================================");
            _logger.LogInformation("AI18 MEMORY SNAPSHOT");
            _logger.LogInformation("  Stage                               : {Stage}", _currentStage);
            LogSnapshotBody(snapshot);

            if (_previousSnapshot is { } prev)
            {
                var deltaSincePreviousBytes = snapshot.WorkingSetBytes - prev.WorkingSetBytes;
                var deltaSinceStartBytes = snapshot.WorkingSetBytes - (_beginSnapshot?.WorkingSetBytes ?? snapshot.WorkingSetBytes);

                _logger.LogInformation(
                    "  Increase Since Previous Stage       : {DeltaMB} MB (from '{PreviousStage}')",
                    ToMb(deltaSincePreviousBytes),
                    prev.Stage);
                _logger.LogInformation(
                    "  Increase Since Pipeline Start        : {DeltaMB} MB",
                    ToMb(deltaSinceStartBytes));

                UpdateLargest("StageIncrease", deltaSincePreviousBytes, $"{prev.Stage} -> {_currentStage}");

                if (_currentStage.Contains("Thumbnail", StringComparison.OrdinalIgnoreCase))
                {
                    UpdateLargest("ThumbnailAllocation", deltaSincePreviousBytes, $"{prev.Stage} -> {_currentStage}");
                }
            }

            _logger.LogInformation("====================================================");
            _previousSnapshot = snapshot;
        }
        catch (Exception ex)
        {
            SafeLogInternalFailure(ex);
        }
    }

    public long RegisterObject(string objectType, long approximateBytes, string stage, int? faceNumber = null)
    {
        if (!_options.Enabled || !_active)
        {
            return -1;
        }

        try
        {
            var id = ++_nextObjectId;
            var label = faceNumber.HasValue ? $"{stage} (Face {faceNumber})" : stage;

            _trackedObjects[id] = new TrackedObject
            {
                Id = id,
                ObjectType = objectType,
                ApproximateBytes = approximateBytes,
                CreatedStage = label,
                CreatedUtc = DateTime.UtcNow,
            };
            _totalObjectsRegistered++;

            ClassifyRegisteredObject(objectType, approximateBytes, label);

            _logger.LogInformation(
                "AI18 OBJECT REGISTERED: Id={Id} Type={ObjectType} ApproxBytes={ApproxBytes} Stage={Stage} ExecutionTraceId={TraceId}",
                id,
                objectType,
                approximateBytes,
                label,
                _traceId);

            return id;
        }
        catch (Exception ex)
        {
            SafeLogInternalFailure(ex);
            return -1;
        }
    }

    public void DisposeObject(long registrationId)
    {
        if (!_options.Enabled || !_active || registrationId < 0)
        {
            return;
        }

        try
        {
            if (!_trackedObjects.TryGetValue(registrationId, out var tracked) || tracked.DisposedUtc is not null)
            {
                return;
            }

            tracked.DisposedStage = _currentStage;
            tracked.DisposedUtc = DateTime.UtcNow;
            _totalObjectsDisposed++;

            var lifetimeMs = Math.Round((tracked.DisposedUtc.Value - tracked.CreatedUtc).TotalMilliseconds, 1);

            _logger.LogInformation(
                "AI18 OBJECT DISPOSED: Id={Id} Type={ObjectType} ApproxBytes={ApproxBytes} CreatedStage={CreatedStage} DisposedStage={DisposedStage} LifetimeMs={LifetimeMs} ExecutionTraceId={TraceId}",
                registrationId,
                tracked.ObjectType,
                tracked.ApproximateBytes,
                tracked.CreatedStage,
                tracked.DisposedStage,
                lifetimeMs,
                _traceId);
        }
        catch (Exception ex)
        {
            SafeLogInternalFailure(ex);
        }
    }

    public void RecordEntityFrameworkQuery(
        string queryName,
        bool asNoTracking,
        int entitiesMaterialized,
        string navigationPropertiesLoaded,
        bool studentPhotosLoaded,
        bool navigationCollectionsLoaded,
        long estimatedGraphBytes)
    {
        if (!_options.Enabled || !_active)
        {
            return;
        }

        try
        {
            _efQueryCount++;
            UpdateLargest("EfGraph", estimatedGraphBytes, queryName);

            _logger.LogInformation("====================================================");
            _logger.LogInformation("AI18 ENTITY FRAMEWORK AUDIT");
            _logger.LogInformation("  Query                               : {QueryName}", queryName);
            _logger.LogInformation("  Tracking Enabled                    : {TrackingEnabled}", !asNoTracking);
            _logger.LogInformation("  AsNoTracking                        : {AsNoTracking}", asNoTracking);
            _logger.LogInformation("  Entities Materialized                : {EntitiesMaterialized}", entitiesMaterialized);
            _logger.LogInformation("  Navigation Properties Loaded        : {NavigationProperties}", navigationPropertiesLoaded);
            _logger.LogInformation("  Student Photos Loaded                : {StudentPhotosLoaded}", studentPhotosLoaded);
            _logger.LogInformation("  Navigation Collections Loaded        : {NavigationCollectionsLoaded}", navigationCollectionsLoaded);
            _logger.LogInformation(
                "  Estimated Object Graph Size          : {GraphBytes} bytes ({GraphMB} MB) [estimate]",
                estimatedGraphBytes,
                ToMb(estimatedGraphBytes));
            _logger.LogInformation("====================================================");

            if (studentPhotosLoaded || navigationCollectionsLoaded)
            {
                _logger.LogWarning(
                    "EF OBJECT GRAPH INFLATION SUSPECTED: Query '{QueryName}' materialized {EntitiesMaterialized} entities with StudentPhotosLoaded={StudentPhotosLoaded} NavigationCollectionsLoaded={NavigationCollectionsLoaded} — verify the projection only selects embedding-vector fields.",
                    queryName,
                    entitiesMaterialized,
                    studentPhotosLoaded,
                    navigationCollectionsLoaded);
            }
        }
        catch (Exception ex)
        {
            SafeLogInternalFailure(ex);
        }
    }

    public void RecordStudentEmbeddingLoad(
        int studentsLoaded,
        int embeddingsLoaded,
        int embeddingDimensions,
        long totalFloatCount,
        int duplicateStudentIds,
        int nullEmbeddings,
        bool imageBytesLoaded,
        bool photoLoaded,
        bool navigationLoaded)
    {
        if (!_options.Enabled || !_active)
        {
            return;
        }

        try
        {
            _studentEmbeddingLoadCount++;

            var totalFloatMemoryBytes = totalFloatCount * (long)sizeof(float);
            // Rough per-object estimate: payload bytes plus ~64 bytes/object CLR overhead for the
            // entity + mapped match-input DTO the embedding survives as (same methodology as AI17.RUNTIME.3).
            var estimatedManagedBytes = totalFloatMemoryBytes + (embeddingsLoaded * 2L * 64);
            // float[] backing arrays are pure managed memory — this audit does not claim any native
            // allocation is caused by student-embedding loading (ONNX/ImageSharp native use is audited
            // separately via RecordOnnxInference / ImageSharp object registration).
            const long estimatedNativeBytes = 0;

            UpdateLargest("StudentGraph", estimatedManagedBytes, $"Student embedding load #{_studentEmbeddingLoadCount} ({studentsLoaded} students, {embeddingsLoaded} embeddings)");

            _logger.LogInformation("====================================================");
            _logger.LogInformation("AI18 STUDENT EMBEDDING AUDIT");
            _logger.LogInformation("  Students Loaded                     : {StudentsLoaded}", studentsLoaded);
            _logger.LogInformation("  Embeddings Loaded                    : {EmbeddingsLoaded}", embeddingsLoaded);
            _logger.LogInformation("  Embedding Dimensions                : {EmbeddingDimensions}", embeddingDimensions);
            _logger.LogInformation("  Float Count                          : {FloatCount}", totalFloatCount);
            _logger.LogInformation(
                "  Total Float Memory                   : {FloatBytes} bytes ({FloatMB} MB)",
                totalFloatMemoryBytes,
                ToMb(totalFloatMemoryBytes));
            _logger.LogInformation(
                "  Estimated Managed Memory              : {ManagedBytes} bytes ({ManagedMB} MB) [estimate]",
                estimatedManagedBytes,
                ToMb(estimatedManagedBytes));
            _logger.LogInformation("  Estimated Native Memory              : {NativeBytes} bytes (embeddings are pure managed float[])", estimatedNativeBytes);
            _logger.LogInformation("  Duplicate Student Ids                : {DuplicateStudentIds}", duplicateStudentIds);
            _logger.LogInformation("  Null Embeddings                      : {NullEmbeddings}", nullEmbeddings);
            _logger.LogInformation("  Image Bytes Loaded                   : {ImageBytesLoaded}", imageBytesLoaded);
            _logger.LogInformation("  Photo Loaded                         : {PhotoLoaded}", photoLoaded);
            _logger.LogInformation("  Navigation Loaded                    : {NavigationLoaded}", navigationLoaded);
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
        long inputBytesApprox,
        long outputBytesApprox,
        MemoryAuditSnapshot before,
        MemoryAuditSnapshot after,
        long inferenceDurationMs,
        int disposableOutputCount,
        bool outputsDisposed)
    {
        if (!_options.Enabled || !_active)
        {
            return;
        }

        try
        {
            _onnxInferenceCount++;
            var nativeDeltaBytes = after.NativeEstimateBytes - before.NativeEstimateBytes;
            var totalTensorBytes = inputBytesApprox + outputBytesApprox;

            UpdateLargest("OnnxAllocation", Math.Max(totalTensorBytes, Math.Max(0, nativeDeltaBytes)), model);

            _logger.LogInformation("====================================================");
            _logger.LogInformation("AI18 ONNX RUNTIME AUDIT");
            _logger.LogInformation("  Model                               : {Model}", model);
            _logger.LogInformation("  Input Tensor Shape                  : {InputShape}", inputTensorShape);
            _logger.LogInformation("  Output Tensor Shape                 : {OutputShape}", outputTensorShape);
            _logger.LogInformation(
                "  Tensor Bytes (in+out, approx.)       : {TensorBytes} bytes ({TensorMB} MB)",
                totalTensorBytes,
                ToMb(totalTensorBytes));
            _logger.LogInformation("  Disposable Output Count             : {OutputCount}", disposableOutputCount);
            _logger.LogInformation("  Native Estimate Before               : {BeforeMB} MB", before.NativeEstimateMB);
            _logger.LogInformation("  Native Estimate After                : {AfterMB} MB", after.NativeEstimateMB);
            _logger.LogInformation("  Peak Native Increase                 : {DeltaMB} MB", ToMb(Math.Max(0, nativeDeltaBytes)));
            _logger.LogInformation("  Inference Duration                   : {DurationMs} ms", inferenceDurationMs);
            _logger.LogInformation("  Outputs Disposed                     : {OutputsDisposed}", outputsDisposed);
            _logger.LogInformation("====================================================");

            if (!outputsDisposed)
            {
                _logger.LogWarning(
                    "UNDISPOSED ONNX OUTPUT: {Model} inference outputs ({OutputCount}) were not disposed by the time this audit ran.",
                    model,
                    disposableOutputCount);
            }
        }
        catch (Exception ex)
        {
            SafeLogInternalFailure(ex);
        }
    }

    public void RecordMatchingMemory(
        int detectedFaceCount,
        int candidateStudentCount,
        MemoryAuditSnapshot before,
        MemoryAuditSnapshot after)
    {
        if (!_options.Enabled || !_active)
        {
            return;
        }

        try
        {
            _matchingCallCount++;
            var deltaBytes = after.WorkingSetBytes - before.WorkingSetBytes;
            UpdateLargest("MatchingAllocation", Math.Max(0, deltaBytes), $"{detectedFaceCount} faces x {candidateStudentCount} candidates");

            _logger.LogInformation("====================================================");
            _logger.LogInformation("AI18 MATCHING MEMORY AUDIT");
            _logger.LogInformation("  Detected Faces                       : {DetectedFaces}", detectedFaceCount);
            _logger.LogInformation("  Candidate Students                   : {CandidateStudents}", candidateStudentCount);
            _logger.LogInformation("  Comparisons Performed                : {Comparisons}", (long)detectedFaceCount * candidateStudentCount);
            _logger.LogInformation("  Working Set Before                   : {BeforeMB} MB", before.WorkingSetMB);
            _logger.LogInformation("  Working Set After                    : {AfterMB} MB", after.WorkingSetMB);
            _logger.LogInformation("  Peak During Matching (approx.)       : {PeakMB} MB", Math.Max(before.WorkingSetMB, after.WorkingSetMB));
            _logger.LogInformation("====================================================");
        }
        catch (Exception ex)
        {
            SafeLogInternalFailure(ex);
        }
    }

    public void RecordDatabaseSave(
        string phase,
        int pendingEntityCount,
        int attendanceRecognitionCount,
        int attendanceSessionCount,
        long estimatedGraphBytes)
    {
        if (!_options.Enabled || !_active)
        {
            return;
        }

        try
        {
            UpdateLargest("DatabaseAllocation", estimatedGraphBytes, $"SaveChanges ({phase}): {pendingEntityCount} pending entities");

            _logger.LogInformation("====================================================");
            _logger.LogInformation("AI18 DATABASE SAVE AUDIT — {Phase}", phase);
            _logger.LogInformation("  Pending Entity Count                 : {PendingEntityCount}", pendingEntityCount);
            _logger.LogInformation("  AttendanceRecognition Count           : {AttendanceRecognitionCount}", attendanceRecognitionCount);
            _logger.LogInformation("  AttendanceSession Count               : {AttendanceSessionCount}", attendanceSessionCount);
            _logger.LogInformation(
                "  Estimated Graph Size                 : {GraphBytes} bytes ({GraphMB} MB) [estimate — see remarks]",
                estimatedGraphBytes,
                ToMb(estimatedGraphBytes));
            _logger.LogInformation("====================================================");
        }
        catch (Exception ex)
        {
            SafeLogInternalFailure(ex);
        }
    }

    public void Complete()
    {
        if (!_options.Enabled || !_active)
        {
            return;
        }

        try
        {
            _currentStage = "Pipeline Completed";
            var finalSnapshot = CaptureSnapshot(_currentStage);

            _logger.LogInformation("====================================================");
            _logger.LogInformation("AI18 MEMORY SNAPSHOT");
            _logger.LogInformation("  Stage                               : {Stage}", _currentStage);
            LogSnapshotBody(finalSnapshot);
            _logger.LogInformation("====================================================");

            LogTop20Consumers();
            LogFinalReport(finalSnapshot);

            foreach (var tracked in _trackedObjects.Values.Where(t => t.DisposedUtc is null))
            {
                var lifetimeMs = Math.Round((DateTime.UtcNow - tracked.CreatedUtc).TotalMilliseconds, 1);
                _logger.LogWarning(
                    "AI18 OBJECT STILL ALIVE AT COMPLETION: Id={Id} Type={ObjectType} ApproxBytes={ApproxBytes} CreatedStage={CreatedStage} AliveMs={AliveMs} ExecutionTraceId={TraceId}",
                    tracked.Id,
                    tracked.ObjectType,
                    tracked.ApproximateBytes,
                    tracked.CreatedStage,
                    lifetimeMs,
                    _traceId);
            }
        }
        catch (Exception ex)
        {
            SafeLogInternalFailure(ex);
        }
    }

    private void LogTop20Consumers()
    {
        var top20 = _trackedObjects.Values
            .OrderByDescending(t => t.ApproximateBytes)
            .Take(20)
            .ToList();

        _logger.LogInformation("====================================================");
        _logger.LogInformation("AI18 TOP 20 MEMORY CONSUMERS");
        _logger.LogInformation("  Total Objects Registered             : {Registered}", _totalObjectsRegistered);
        _logger.LogInformation("  Total Objects Disposed               : {Disposed}", _totalObjectsDisposed);

        var rank = 1;
        foreach (var obj in top20)
        {
            var lifetimeMs = obj.DisposedUtc.HasValue
                ? Math.Round((obj.DisposedUtc.Value - obj.CreatedUtc).TotalMilliseconds, 1)
                : Math.Round((DateTime.UtcNow - obj.CreatedUtc).TotalMilliseconds, 1);

            _logger.LogInformation(
                "  #{Rank,-2} Type={Type,-28} Bytes={Bytes,10} LifetimeMs={LifetimeMs,8} Disposed={Disposed,-5} CreatedStage={CreatedStage} DisposedStage={DisposedStage} TraceId={TraceId}",
                rank,
                obj.ObjectType,
                obj.ApproximateBytes,
                lifetimeMs,
                obj.DisposedUtc.HasValue,
                obj.CreatedStage,
                obj.DisposedStage ?? "(not disposed)",
                _traceId);
            rank++;
        }

        _logger.LogInformation("====================================================");
    }

    private void LogFinalReport(MemoryAuditSnapshot finalSnapshot)
    {
        _logger.LogInformation("====================================================");
        _logger.LogInformation("MEMORY FORENSICS REPORT");
        _logger.LogInformation("  ExecutionTraceId                     : {TraceId}", _traceId);
        _logger.LogInformation("  Peak Working Set                     : {PeakMB} MB", finalSnapshot.PeakWorkingSetMB);
        _logger.LogInformation("  Peak Private Memory                  : {PeakMB} MB", finalSnapshot.PeakPrivateMemoryMB);
        _logger.LogInformation("  Peak Managed Heap                    : {PeakMB} MB", finalSnapshot.PeakManagedHeapMB);
        _logger.LogInformation("  Peak Native Estimate                 : {PeakMB} MB", finalSnapshot.PeakNativeEstimateMB);
        LogLargest("Largest Stage Increase", "StageIncrease");
        LogLargest("Largest Object", "Object");
        LogLargest("Largest Collection", "Collection");
        LogLargest("Largest Disposable", "Disposable");
        LogLargest("Largest Image", "Image");
        LogLargest("Largest Tensor", "Tensor");
        LogLargest("Largest Float Array", "FloatArray");
        LogLargest("Largest Byte Array", "ByteArray");
        LogLargest("Largest Student Graph", "StudentGraph");
        LogLargest("Largest EF Graph", "EfGraph");
        LogLargest("Largest ImageSharp Allocation", "ImageSharpAllocation");
        LogLargest("Largest ONNX Allocation", "OnnxAllocation");
        LogLargest("Largest Matching Allocation", "MatchingAllocation");
        LogLargest("Largest Thumbnail Allocation", "ThumbnailAllocation");
        LogLargest("Largest Database Allocation", "DatabaseAllocation");
        _logger.LogInformation("====================================================");
    }

    private void LogLargest(string displayName, string category)
    {
        var (bytes, label) = _largest.TryGetValue(category, out var value) ? value : (0, "(none observed)");
        _logger.LogInformation(
            "  {DisplayName,-32} : {Bytes} bytes ({MB} MB) — {Label}",
            displayName,
            bytes,
            ToMb(bytes),
            label);
    }

    private MemoryAuditSnapshot CaptureSnapshot(string stage)
    {
        var elapsedMs = _jobStopwatch?.Elapsed.TotalMilliseconds ?? 0;
        var snapshot = MemoryAuditSnapshot.Capture(
            _traceId,
            stage,
            elapsedMs,
            _peakWorkingSetBytes,
            _peakPrivateMemoryBytes,
            _peakManagedHeapBytes,
            _peakNativeEstimateBytes);

        _peakWorkingSetBytes = snapshot.PeakWorkingSetBytes;
        _peakPrivateMemoryBytes = snapshot.PeakPrivateMemoryBytes;
        _peakManagedHeapBytes = snapshot.PeakManagedHeapBytes;
        _peakNativeEstimateBytes = snapshot.PeakNativeEstimateBytes;

        return snapshot;
    }

    private void LogSnapshotBody(MemoryAuditSnapshot s)
    {
        _logger.LogInformation("  Timestamp                            : {Timestamp:O}", s.TimestampUtc);
        _logger.LogInformation("  Execution Trace Id                   : {TraceId}", s.ExecutionTraceId);
        _logger.LogInformation("  Elapsed                               : {ElapsedMs} ms", Math.Round(s.ElapsedMs, 1));
        _logger.LogInformation("  Working Set                           : {WorkingSetMB} MB", s.WorkingSetMB);
        _logger.LogInformation("  Private Memory                       : {PrivateMemoryMB} MB", s.PrivateMemoryMB);
        _logger.LogInformation("  Managed Heap                         : {ManagedHeapMB} MB", s.ManagedHeapMB);
        _logger.LogInformation("  GC Heap Fragmentation                : {FragMB} MB", s.GcFragmentedMB);
        _logger.LogInformation("  GC Memory Load                       : {LoadMB} MB", s.GcMemoryLoadMB);
        _logger.LogInformation("  Native Estimate                      : {NativeMB} MB", s.NativeEstimateMB);
        _logger.LogInformation("  Gen0 / Gen1 / Gen2                    : {Gen0} / {Gen1} / {Gen2}", s.Gen0Collections, s.Gen1Collections, s.Gen2Collections);
        _logger.LogInformation("  Thread Count                          : {ThreadCount}", s.ThreadCount);
        _logger.LogInformation("  Handle Count                         : {HandleCount}", s.HandleCount);
        _logger.LogInformation("  Processor Time                       : {ProcessorTimeMs} ms", Math.Round(s.ProcessorTimeMs, 1));
        _logger.LogInformation("  Peak Working Set                     : {PeakWorkingSetMB} MB", s.PeakWorkingSetMB);
        _logger.LogInformation("  Peak Private Memory                  : {PeakPrivateMemoryMB} MB", s.PeakPrivateMemoryMB);
        _logger.LogInformation("  Peak Managed Heap                    : {PeakManagedHeapMB} MB", s.PeakManagedHeapMB);
        _logger.LogInformation("  Peak Native Estimate                 : {PeakNativeEstimateMB} MB", s.PeakNativeEstimateMB);
    }

    private void ClassifyRegisteredObject(string objectType, long approximateBytes, string label)
    {
        UpdateLargest("Object", approximateBytes, $"{objectType} @ {label}");

        if (objectType.Contains("Collection", StringComparison.OrdinalIgnoreCase))
        {
            UpdateLargest("Collection", approximateBytes, $"{objectType} @ {label}");
        }

        if (IsDisposableObjectType(objectType))
        {
            UpdateLargest("Disposable", approximateBytes, $"{objectType} @ {label}");
        }

        if (objectType.Contains("Image", StringComparison.OrdinalIgnoreCase) || objectType.Contains("Crop", StringComparison.OrdinalIgnoreCase))
        {
            UpdateLargest("Image", approximateBytes, $"{objectType} @ {label}");
            UpdateLargest("ImageSharpAllocation", approximateBytes, $"{objectType} @ {label}");
        }

        if (objectType.Contains("Tensor", StringComparison.OrdinalIgnoreCase) || objectType.Contains("NamedOnnxValue", StringComparison.OrdinalIgnoreCase))
        {
            UpdateLargest("Tensor", approximateBytes, $"{objectType} @ {label}");
            UpdateLargest("OnnxAllocation", approximateBytes, $"{objectType} @ {label}");
        }

        if (objectType.Equals("Float Array", StringComparison.OrdinalIgnoreCase) || objectType.Equals("Embedding Array", StringComparison.OrdinalIgnoreCase))
        {
            UpdateLargest("FloatArray", approximateBytes, $"{objectType} @ {label}");
        }

        if (objectType.Equals("Byte Array", StringComparison.OrdinalIgnoreCase))
        {
            UpdateLargest("ByteArray", approximateBytes, $"{objectType} @ {label}");
        }

        if (objectType.Contains("Student", StringComparison.OrdinalIgnoreCase))
        {
            UpdateLargest("StudentGraph", approximateBytes, $"{objectType} @ {label}");
        }

        if (objectType.Contains("AttendanceRecognition", StringComparison.OrdinalIgnoreCase) ||
            objectType.Contains("ChangeTracker", StringComparison.OrdinalIgnoreCase) ||
            objectType.Contains("AttendanceSession", StringComparison.OrdinalIgnoreCase))
        {
            UpdateLargest("DatabaseAllocation", approximateBytes, $"{objectType} @ {label}");
        }

        if (label.Contains("Thumbnail", StringComparison.OrdinalIgnoreCase))
        {
            UpdateLargest("ThumbnailAllocation", approximateBytes, $"{objectType} @ {label}");
        }
    }

    private static bool IsDisposableObjectType(string objectType) =>
        objectType.Contains("Image", StringComparison.OrdinalIgnoreCase) ||
        objectType.Contains("Stream", StringComparison.OrdinalIgnoreCase) ||
        objectType.Contains("Tensor", StringComparison.OrdinalIgnoreCase) ||
        objectType.Contains("NamedOnnxValue", StringComparison.OrdinalIgnoreCase) ||
        objectType.Contains("DisposableCollection", StringComparison.OrdinalIgnoreCase) ||
        objectType.Contains("Crop", StringComparison.OrdinalIgnoreCase);

    private void UpdateLargest(string category, long bytes, string label)
    {
        if (!_largest.TryGetValue(category, out var current) || bytes > current.Bytes)
        {
            _largest[category] = (bytes, label);
        }
    }

    private static double ToMb(long bytes) => Math.Round(bytes / (1024d * 1024d), 2);

    private void SafeLogInternalFailure(Exception ex)
    {
        try
        {
            _logger.LogWarning(
                ex,
                "RecognitionMemoryAudit internal failure — diagnostics suppressed for this event; the recognition pipeline itself is unaffected.");
        }
        catch
        {
            // A logging failure must never propagate out of a diagnostics component.
        }
    }
}
