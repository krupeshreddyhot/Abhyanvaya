namespace Abhyanvaya.Infrastructure.Diagnostics;

/// <summary>
/// Opaque handle returned by <see cref="IRecognitionPipelineDiagnostics.StageStart"/> and passed back
/// into <see cref="IRecognitionPipelineDiagnostics.StageEnd"/>. Callers never read its fields — it
/// only carries what's needed to compute stage duration and label the matching "Finished" log line.
/// </summary>
public readonly struct RecognitionStageHandle
{
    internal RecognitionStageHandle(string stageName, int? faceNumber, int? faceCount, long startElapsedMs, DateTime startUtc)
    {
        StageName = stageName;
        FaceNumber = faceNumber;
        FaceCount = faceCount;
        StartElapsedMs = startElapsedMs;
        StartUtc = startUtc;
    }

    internal string StageName { get; }
    internal int? FaceNumber { get; }
    internal int? FaceCount { get; }
    internal long StartElapsedMs { get; }
    internal DateTime StartUtc { get; }

    /// <summary>A handle representing "diagnostics inactive" — <see cref="IRecognitionPipelineDiagnostics.StageEnd"/> no-ops on it.</summary>
    public static RecognitionStageHandle Inactive { get; } = default;

    internal bool IsActive => StageName is not null;
}

/// <summary>
/// Point-in-time peak memory reading, captured whenever a new maximum Working Set is observed
/// (AI15.DIAGNOSTICS.1 Task 5).
/// </summary>
public sealed record RecognitionPeakMemory(
    long ManagedHeapBytes,
    long WorkingSetBytes,
    long PrivateBytes,
    string Stage,
    int? FaceNumber,
    DateTime TimestampUtc);

/// <summary>
/// Terminal summary for one classroom recognition job, produced by
/// <see cref="IRecognitionPipelineDiagnostics.Complete"/> or
/// <see cref="IRecognitionPipelineDiagnostics.Fail"/> and handed to
/// <see cref="IRecognitionDiagnosticsStore"/> for <c>/health</c> and <c>/health/ready</c> exposure.
/// </summary>
public sealed record RecognitionDiagnosticsSummary(
    Guid AttendanceSessionId,
    int TenantId,
    DateTime StartedUtc,
    DateTime CompletedUtc,
    long DurationMs,
    bool Completed,
    bool Failed,
    string LastStage,
    int? LastFace,
    long PeakManagedHeapBytes,
    long PeakWorkingSetBytes,
    long PeakPrivateBytes,
    string PeakStage,
    int? PeakFace,
    DateTime PeakTimestampUtc,
    IReadOnlyDictionary<string, long> StageTotalDurationsMs,
    // AI15.DIAGNOSTICS.2B/2C: sourced from the already-bound InsightFaceOptions.PipelineVersion and
    // the per-job IRecognitionExecutionContext — never hardcoded, never re-read from configuration.
    string PipelineVersion,
    string ExecutionTraceId,
    int RecognitionAttempt,
    // AI16.RUNTIME.4: native-memory estimate and largest single-step Working Set jump observed
    // during this job — see RecognitionMemorySnapshot.NativeEstimateBytes for the estimation method.
    long PeakNativeEstimateBytes,
    long PeakWorkingSetDeltaBytes);
