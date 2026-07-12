using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Constants;
using Abhyanvaya.Infrastructure.InsightFace;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.Diagnostics;

/// <summary>
/// Scoped implementation of <see cref="IRecognitionPipelineDiagnostics"/> (AI15.DIAGNOSTICS.1).
/// Registered as Scoped so exactly one instance exists per classroom recognition job (the same DI
/// scope <see cref="Abhyanvaya.Infrastructure.BackgroundWorkers.ClassroomRecognitionBackgroundService"/>
/// creates per dequeued message) — no locking is required because a single job is always processed
/// end-to-end on one logical call chain, never concurrently with itself.
/// </summary>
/// <remarks>
/// Every public method is wrapped in try/catch that swallows and logs a single internal warning
/// (<see cref="SafeLogInternalFailure"/>) rather than ever propagating — this guarantees a bug in
/// diagnostics code can never change recognition pipeline behavior, timing, or exceptions.
/// Every public method also no-ops entirely (no snapshot capture, no allocation) when
/// <see cref="RecognitionDiagnosticsOptions.Enabled"/> is <c>false</c> or before <see cref="Begin"/>
/// has been called — the latter is what makes it safe to instrument <c>InsightFaceEngine</c>'s shared
/// private helpers (used by both classroom recognition and student face embedding) without any
/// observable effect on the embedding pipeline, which never calls <see cref="Begin"/>.
/// </remarks>
public sealed class RecognitionPipelineDiagnostics : IRecognitionPipelineDiagnostics
{
    private static readonly string[] TimingSummaryCategories =
        ["Load Image", "Detection", "Cropping", "Embedding", "Matching", "Saving"];

    private readonly RecognitionDiagnosticsOptions _options;
    private readonly IRecognitionDiagnosticsStore _store;
    private readonly IRecognitionExecutionContext _executionContext;
    private readonly string _pipelineVersion;
    private readonly ILogger<RecognitionPipelineDiagnostics> _logger;

    private bool _active;
    private Guid _sessionId;
    private int _tenantId;
    private Stopwatch _stopwatch = new();
    private DateTime _beginUtc;

    private RecognitionMemorySnapshot? _lastSnapshot;
    private string? _lastStageLabel;
    private int? _lastFace;

    private long _peakManagedHeapBytes;
    private long _peakWorkingSetBytes;
    private long _peakPrivateBytes;
    private string? _peakStage;
    private int? _peakFace;
    private DateTime _peakTimestampUtc;

    // AI16.RUNTIME.4: tracked independently of the Working-Set-driven peak fields above — native
    // memory (Private minus Managed Heap) and a single Working-Set jump can each peak at a different
    // stage than the overall Working Set does.
    private long _peakNativeEstimateBytes;
    private long _peakWorkingSetDeltaBytes;
    private string? _peakWorkingSetDeltaStage;

    private readonly Dictionary<string, long> _stageTotalsMs = new();

    public RecognitionPipelineDiagnostics(
        IOptions<RecognitionDiagnosticsOptions> options,
        IOptions<InsightFaceOptions> insightFaceOptions,
        IRecognitionDiagnosticsStore store,
        IRecognitionExecutionContext executionContext,
        ILogger<RecognitionPipelineDiagnostics> logger)
    {
        _options = options.Value;
        // AI15.DIAGNOSTICS.2B: reuse the existing InsightFaceOptions.PipelineVersion binding — never
        // re-read the "InsightFace" configuration section, never hardcode the version string.
        _pipelineVersion = insightFaceOptions.Value.PipelineVersion;
        _store = store;
        _executionContext = executionContext;
        _logger = logger;
    }

    public void Begin(Guid attendanceSessionId, int tenantId)
    {
        if (!_options.Enabled)
        {
            return;
        }

        try
        {
            _sessionId = attendanceSessionId;
            _tenantId = tenantId;
            _stopwatch = Stopwatch.StartNew();
            _beginUtc = DateTime.UtcNow;
            _lastSnapshot = null;
            _peakWorkingSetBytes = 0;
            _stageTotalsMs.Clear();
            _active = true;

            RecordSnapshotAndLog("Recognition Started", faceNumber: null, faceCount: null, stageDurationMs: null, timingCategory: null);
            LogExecutionTraceBlock();
        }
        catch (Exception ex)
        {
            SafeLogInternalFailure(ex);
        }
    }

    public RecognitionStageHandle StageStart(string stageName, int? faceNumber = null, int? faceCount = null)
    {
        if (!_options.Enabled || !_active)
        {
            return RecognitionStageHandle.Inactive;
        }

        try
        {
            var label = BuildLabel(stageName, faceNumber, "Started");
            RecordSnapshotAndLog(label, faceNumber, faceCount, stageDurationMs: null, timingCategory: null, rawStage: stageName);
            return new RecognitionStageHandle(stageName, faceNumber, faceCount, _stopwatch.ElapsedMilliseconds, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            SafeLogInternalFailure(ex);
            return RecognitionStageHandle.Inactive;
        }
    }

    public void StageEnd(RecognitionStageHandle handle)
    {
        if (!_options.Enabled || !_active || !handle.IsActive)
        {
            return;
        }

        try
        {
            var durationMs = _stopwatch.ElapsedMilliseconds - handle.StartElapsedMs;
            var label = BuildLabel(handle.StageName, handle.FaceNumber, "Finished");
            RecordSnapshotAndLog(label, handle.FaceNumber, handle.FaceCount, durationMs, CanonicalTimingCategory(handle.StageName), rawStage: handle.StageName);
        }
        catch (Exception ex)
        {
            SafeLogInternalFailure(ex);
        }
    }

    public void FaceEvent(string label, int faceNumber, int faceCount)
    {
        if (!_options.Enabled || !_active)
        {
            return;
        }

        try
        {
            RecordSnapshotAndLog($"{label} Face {faceNumber}", faceNumber, faceCount, stageDurationMs: null, timingCategory: null, rawStage: label);
        }
        catch (Exception ex)
        {
            SafeLogInternalFailure(ex);
        }
    }

    public void ObjectCreated(string typeName, string? detail = null)
    {
        if (!_options.Enabled || !_active)
        {
            return;
        }

        try
        {
            // Deliberately lightweight (Task 12): no memory snapshot here — object lifecycle events
            // fire far more often than stage boundaries, so keeping this to a single log line with no
            // Process/GC reads keeps total diagnostics overhead bounded regardless of face count.
            _logger.LogInformation("{TypeName} Created{DetailSuffix}", typeName, FormatDetailSuffix(detail));
        }
        catch (Exception ex)
        {
            SafeLogInternalFailure(ex);
        }
    }

    public void ObjectDisposed(string typeName, string? detail = null)
    {
        if (!_options.Enabled || !_active)
        {
            return;
        }

        try
        {
            _logger.LogInformation("{TypeName} Disposed{DetailSuffix}", typeName, FormatDetailSuffix(detail));
        }
        catch (Exception ex)
        {
            SafeLogInternalFailure(ex);
        }
    }

    public void Fail(Exception exception)
    {
        if (!_options.Enabled || !_active)
        {
            return;
        }

        try
        {
            var snapshot = RecognitionMemorySnapshot.Capture();

            _logger.LogError(
                exception,
                "Recognition Pipeline Failure\n" +
                "  Exception                          : {ExceptionType}: {ExceptionMessage}\n" +
                "  Current Stage                       : {CurrentStage}\n" +
                "  Current Face                        : {CurrentFace}\n" +
                "  Peak Managed Heap                   : {PeakManagedHeapMB} MB\n" +
                "  Peak Working Set                    : {PeakWorkingSetMB} MB\n" +
                "  Peak Private Bytes                  : {PeakPrivateMB} MB\n" +
                "  Elapsed                             : {ElapsedMs} ms\n" +
                "  Stack Trace                         : {StackTrace}",
                exception.GetType().Name,
                exception.Message,
                _lastStageLabel ?? "(none)",
                _lastFace,
                ToMb(_peakManagedHeapBytes),
                ToMb(_peakWorkingSetBytes),
                ToMb(_peakPrivateBytes),
                _stopwatch.ElapsedMilliseconds,
                exception.StackTrace ?? "(no stack trace)");
            LogExecutionTraceBlock();

            _store.RecordCompleted(BuildSummary(completed: false, failed: true, lastSnapshotForCurrentStage: snapshot));
            _active = false;
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
            RecordSnapshotAndLog("Recognition Completed", faceNumber: null, faceCount: null, stageDurationMs: null, timingCategory: null);
            LogExecutionTraceBlock();
            LogMemorySummary();
            LogExecutionTraceBlock();
            LogTimingSummary();
            LogForcedGcValidation(); // AI16.RUNTIME.5 — no-ops unless explicitly enabled.

            _store.RecordCompleted(BuildSummary(completed: true, failed: false, lastSnapshotForCurrentStage: null));
            _active = false;
        }
        catch (Exception ex)
        {
            SafeLogInternalFailure(ex);
        }
    }

    // ---- internals ----

    private void RecordSnapshotAndLog(
        string stageLabel,
        int? faceNumber,
        int? faceCount,
        long? stageDurationMs,
        string? timingCategory,
        string? rawStage = null)
    {
        var snapshot = RecognitionMemorySnapshot.Capture();
        var elapsedMs = _stopwatch.ElapsedMilliseconds;

        var deltaWorkingSetBytes = _lastSnapshot.HasValue ? snapshot.WorkingSetBytes - _lastSnapshot.Value.WorkingSetBytes : 0;
        _lastSnapshot = snapshot;
        _lastStageLabel = stageLabel;
        _lastFace = faceNumber;

        if (snapshot.WorkingSetBytes > _peakWorkingSetBytes)
        {
            _peakWorkingSetBytes = snapshot.WorkingSetBytes;
            _peakManagedHeapBytes = snapshot.ManagedHeapBytes;
            _peakPrivateBytes = snapshot.PrivateBytes;
            _peakStage = stageLabel;
            _peakFace = faceNumber;
            _peakTimestampUtc = snapshot.TimestampUtc;
        }

        // AI16.RUNTIME.4: independent peaks — the largest native estimate or single-step Working Set
        // jump does not necessarily land on the same stage as the overall Working Set peak above.
        if (snapshot.NativeEstimateBytes > _peakNativeEstimateBytes)
        {
            _peakNativeEstimateBytes = snapshot.NativeEstimateBytes;
        }

        if (deltaWorkingSetBytes > _peakWorkingSetDeltaBytes)
        {
            _peakWorkingSetDeltaBytes = deltaWorkingSetBytes;
            _peakWorkingSetDeltaStage = stageLabel;
        }

        if (stageDurationMs.HasValue && timingCategory is not null)
        {
            _stageTotalsMs.TryGetValue(timingCategory, out var existing);
            _stageTotalsMs[timingCategory] = existing + stageDurationMs.Value;
        }

        LogDiagnosticsBox(stageLabel, rawStage, faceNumber, faceCount, snapshot, deltaWorkingSetBytes, elapsedMs);

        // Task 8: OOM prediction — advisory only, never throttles or changes behavior.
        var thresholdBytes = (long)_options.WorkingSetWarningThresholdMB * 1024 * 1024;
        if (snapshot.WorkingSetBytes > thresholdBytes)
        {
            _logger.LogWarning(
                "WARNING: Memory approaching Render Starter limit. Current Working Set: {WorkingSetMB} MB. Stage: {Stage}",
                snapshot.WorkingSetMegabytes,
                stageLabel);
        }
    }

    // Task 10: structured box format, following the existing startup diagnostics "Label : Value" style.
    private void LogDiagnosticsBox(
        string stageLabel,
        string? rawStage,
        int? faceNumber,
        int? faceCount,
        RecognitionMemorySnapshot snapshot,
        long deltaWorkingSetBytes,
        long elapsedMs)
    {
        _logger.LogInformation("====================================================");
        _logger.LogInformation("Recognition Pipeline Diagnostics");
        _logger.LogInformation("  Stage                              : {Stage}", stageLabel);
        if (rawStage is not null && rawStage != stageLabel)
        {
            _logger.LogInformation("  Raw Stage                          : {RawStage}", rawStage);
        }

        if (faceNumber.HasValue)
        {
            _logger.LogInformation("  Face                               : {FaceNumber} of {FaceCount}", faceNumber, faceCount);
        }

        _logger.LogInformation("  Managed Heap                       : {ManagedHeapMB} MB", snapshot.ManagedHeapMegabytes);
        _logger.LogInformation("  Working Set                        : {WorkingSetMB} MB", snapshot.WorkingSetMegabytes);
        _logger.LogInformation("  Private Memory                     : {PrivateMemoryMB} MB", snapshot.PrivateMegabytes);
        _logger.LogInformation("  Delta                              : {Delta}", FormatDelta(deltaWorkingSetBytes));
        _logger.LogInformation("  Elapsed                            : {ElapsedMs} ms", elapsedMs);
        _logger.LogInformation("  Thread Id                          : {ThreadId}", snapshot.ThreadId);
        _logger.LogInformation("  GC Gen0/Gen1/Gen2                  : {Gen0}/{Gen1}/{Gen2}", snapshot.Gen0Collections, snapshot.Gen1Collections, snapshot.Gen2Collections);
        _logger.LogInformation("====================================================");
    }

    // AI15.DIAGNOSTICS.2B/2C: appended after the small set of per-job summary logs (Recognition
    // Started/Completed, Recognition Memory Summary, Failure Diagnostics) — never after the
    // high-frequency per-stage/per-face boxes, which keep their AI15.DIAGNOSTICS.1 format unchanged.
    private void LogExecutionTraceBlock() =>
        ExecutionTraceLog.LogBlock(_logger, _executionContext, _pipelineVersion, EmbeddingProviders.InsightFace);

    private void LogMemorySummary()
    {
        _logger.LogInformation("----------------------------------------------------------");
        _logger.LogInformation("Recognition Memory Summary");
        _logger.LogInformation("----------------------------------------------------------");
        _logger.LogInformation("  Peak Managed Heap                   : {PeakManagedHeapMB} MB", ToMb(_peakManagedHeapBytes));
        _logger.LogInformation("  Peak Working Set                    : {PeakWorkingSetMB} MB", ToMb(_peakWorkingSetBytes));
        _logger.LogInformation("  Peak Private Memory                 : {PeakPrivateMB} MB", ToMb(_peakPrivateBytes));
        // AI16.RUNTIME.4: finer visibility beyond the three peaks above (native/unmanaged estimate,
        // and the single largest Working Set jump between two consecutive stage boundaries).
        _logger.LogInformation("  Peak Native Estimate                : {PeakNativeEstimateMB} MB", ToMb(_peakNativeEstimateBytes));
        _logger.LogInformation("  Peak Working Set Delta              : {PeakWorkingSetDeltaMB} MB (at {PeakDeltaStage})", ToMb(_peakWorkingSetDeltaBytes), _peakWorkingSetDeltaStage ?? "(none)");
        _logger.LogInformation("  Highest Memory Stage                : {PeakStage}", _peakStage ?? "(none)");
        _logger.LogInformation("  Highest Memory Face                 : {PeakFace}", _peakFace);
        _logger.LogInformation("  Recognition Duration                : {DurationMs} ms", _stopwatch.ElapsedMilliseconds);
        _logger.LogInformation("----------------------------------------------------------");
    }

    // AI16.RUNTIME.5: diagnostics-only, gated behind RecognitionDiagnosticsOptions.ForceGcValidation
    // (default false). Never called from anywhere except Complete() below, and only when that flag is
    // explicitly enabled — a full blocking GC pass on every job is not something this method will
    // ever do unless an operator has deliberately turned it on to investigate memory behavior.
    private void LogForcedGcValidation()
    {
        if (!_options.ForceGcValidation)
        {
            return;
        }

        try
        {
            var before = RecognitionMemorySnapshot.Capture();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var after = RecognitionMemorySnapshot.Capture();

            _logger.LogInformation("----------------------------------------------------------");
            _logger.LogInformation("Forced GC Validation (diagnostics only — RecognitionDiagnostics:ForceGcValidation)");
            _logger.LogInformation("----------------------------------------------------------");
            _logger.LogInformation("  Managed Heap Before                 : {BeforeMB} MB", before.ManagedHeapMegabytes);
            _logger.LogInformation("  Managed Heap After                  : {AfterMB} MB", after.ManagedHeapMegabytes);
            _logger.LogInformation("  Managed Heap Reclaimed               : {ReclaimedMB} MB", ToMb(before.ManagedHeapBytes - after.ManagedHeapBytes));
            _logger.LogInformation("  Working Set Before                  : {BeforeMB} MB", before.WorkingSetMegabytes);
            _logger.LogInformation("  Working Set After                   : {AfterMB} MB", after.WorkingSetMegabytes);
            _logger.LogInformation("  Working Set Reclaimed                : {ReclaimedMB} MB", ToMb(before.WorkingSetBytes - after.WorkingSetBytes));
            _logger.LogInformation("  Private Memory Before               : {BeforeMB} MB", before.PrivateMegabytes);
            _logger.LogInformation("  Private Memory After                : {AfterMB} MB", after.PrivateMegabytes);
            _logger.LogInformation("  Private Memory Reclaimed             : {ReclaimedMB} MB", ToMb(before.PrivateBytes - after.PrivateBytes));
            _logger.LogInformation(
                "  Interpretation                      : Working Set/Private drops after a forced GC ⇒ that memory was collectible managed garbage. Memory that stays elevated after this forced GC is native/unmanaged (ONNX Runtime arena, OS-level allocator fragmentation, etc.) and a GC pass cannot reclaim it.");
            _logger.LogInformation("----------------------------------------------------------");
        }
        catch (Exception ex)
        {
            SafeLogInternalFailure(ex);
        }
    }

    private void LogTimingSummary()
    {
        _logger.LogInformation("----------------------------------------------------------");
        _logger.LogInformation("Recognition Timing Summary");
        _logger.LogInformation("----------------------------------------------------------");
        foreach (var category in TimingSummaryCategories)
        {
            _stageTotalsMs.TryGetValue(category, out var totalMs);
            _logger.LogInformation("  {Category,-20}                : {TotalMs} ms", category, totalMs);
        }

        _logger.LogInformation("  {Category,-20}                : {TotalMs} ms", "Entire Recognition", _stopwatch.ElapsedMilliseconds);
        _logger.LogInformation("----------------------------------------------------------");
    }

    private RecognitionDiagnosticsSummary BuildSummary(bool completed, bool failed, RecognitionMemorySnapshot? lastSnapshotForCurrentStage)
    {
        _ = lastSnapshotForCurrentStage; // reserved for future extension; current fields are sufficient today.

        return new RecognitionDiagnosticsSummary(
            AttendanceSessionId: _sessionId,
            TenantId: _tenantId,
            StartedUtc: _beginUtc,
            CompletedUtc: DateTime.UtcNow,
            DurationMs: _stopwatch.ElapsedMilliseconds,
            Completed: completed,
            Failed: failed,
            LastStage: _lastStageLabel ?? "(none)",
            LastFace: _lastFace,
            PeakManagedHeapBytes: _peakManagedHeapBytes,
            PeakWorkingSetBytes: _peakWorkingSetBytes,
            PeakPrivateBytes: _peakPrivateBytes,
            PeakStage: _peakStage ?? "(none)",
            PeakFace: _peakFace,
            PeakTimestampUtc: _peakTimestampUtc,
            StageTotalDurationsMs: new Dictionary<string, long>(_stageTotalsMs),
            PipelineVersion: _pipelineVersion,
            ExecutionTraceId: ExecutionTraceLog.FormatTraceId(_executionContext),
            RecognitionAttempt: _executionContext.RecognitionAttempt,
            PeakNativeEstimateBytes: _peakNativeEstimateBytes,
            PeakWorkingSetDeltaBytes: _peakWorkingSetDeltaBytes);
    }

    private static string BuildLabel(string stageName, int? faceNumber, string boundary)
    {
        if (faceNumber.HasValue)
        {
            var shortName = stageName switch
            {
                "Face Cropping" => "Cropping",
                "Embedding Generation" => "Embedding",
                _ => stageName,
            };

            return $"{shortName} Face {faceNumber} {boundary}";
        }

        return $"{stageName} {boundary}";
    }

    private static string? CanonicalTimingCategory(string stageName) => stageName switch
    {
        "Load Image" => "Load Image",
        "Face Detection" => "Detection",
        "Face Cropping" => "Cropping",
        "Embedding Generation" => "Embedding",
        "Matching" => "Matching",
        "Database Save" => "Saving",
        _ => null,
    };

    private static string FormatDetailSuffix(string? detail) => detail is null ? string.Empty : $" ({detail})";

    private static string FormatDelta(long deltaBytes)
    {
        var mb = deltaBytes / (1024d * 1024d);
        return mb >= 0 ? $"+{mb:F0} MB" : $"{mb:F0} MB";
    }

    private static double ToMb(long bytes) => Math.Round(bytes / (1024d * 1024d), 1);

    private void SafeLogInternalFailure(Exception ex)
    {
        try
        {
            _logger.LogWarning(
                ex,
                "RecognitionPipelineDiagnostics internal failure — diagnostics suppressed for this event; the recognition pipeline itself is unaffected.");
        }
        catch
        {
            // A logging failure must never propagate out of a diagnostics component.
        }
    }
}
