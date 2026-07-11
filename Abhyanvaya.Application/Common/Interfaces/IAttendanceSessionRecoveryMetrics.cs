namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// In-process, runtime-only counters for <c>StuckAttendanceSessionRecoveryService</c>
/// (AI14.RUNTIME.4). No persistence — counters reset when the process restarts, exactly like the
/// in-memory recognition queues they help diagnose.
/// </summary>
public interface IAttendanceSessionRecoveryMetrics
{
    /// <summary>
    /// Records the outcome of one completed sweep execution. Called exactly once per run, including
    /// runs that found zero orphaned sessions.
    /// </summary>
    /// <param name="recoveredCount">Sessions successfully recovered (moved to Failed) in this run.</param>
    /// <param name="pendingRemaining">Orphaned sessions found but left for a later run because of the <c>MaxRecoveriesPerRun</c> cap.</param>
    /// <param name="durationMs">Wall-clock duration of this run.</param>
    void RecordRun(int recoveredCount, int pendingRemaining, long durationMs);

    /// <summary>Point-in-time snapshot of all counters.</summary>
    AttendanceSessionRecoveryMetricsSnapshot GetSnapshot();
}

/// <summary>Point-in-time recovery sweep metrics, exposed via <c>/health</c> and <c>/health/ready</c>.</summary>
public sealed record AttendanceSessionRecoveryMetricsSnapshot(
    long RecoveryRuns,
    long RecoveredSessions,
    DateTime? LastRecoveryUtc,
    long? LastRecoveryDurationMs,
    int PendingRecoveries);
