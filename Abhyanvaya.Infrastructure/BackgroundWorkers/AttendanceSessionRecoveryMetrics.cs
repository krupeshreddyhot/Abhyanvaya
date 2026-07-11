using Abhyanvaya.Application.Common.Interfaces;

namespace Abhyanvaya.Infrastructure.BackgroundWorkers;

/// <summary>
/// Thread-safe in-process counters for <see cref="StuckAttendanceSessionRecoveryService"/>
/// observability (AI14.RUNTIME.4). Registered as a singleton so the same counters are shared between
/// the background service (writer) and the <c>/health</c> / <c>/health/ready</c> endpoints (readers).
/// </summary>
public sealed class AttendanceSessionRecoveryMetrics : IAttendanceSessionRecoveryMetrics
{
    private long _recoveryRuns;
    private long _recoveredSessions;

    // Stored as ticks/milliseconds so Interlocked can update them atomically without a lock;
    // 0 and -1 respectively are the "never happened yet" sentinels, translated to null in the snapshot.
    private long _lastRecoveryUtcTicks;
    private long _lastRecoveryDurationMs = -1;

    private int _pendingRecoveries;

    public void RecordRun(int recoveredCount, int pendingRemaining, long durationMs)
    {
        Interlocked.Increment(ref _recoveryRuns);
        Interlocked.Exchange(ref _pendingRecoveries, pendingRemaining);

        // "Last recovery" reflects the last run that actually recovered a session — not merely the
        // last time the sweep's timer ticked — so operators can see when intervention last actually
        // happened, not just that the background timer is alive.
        if (recoveredCount > 0)
        {
            Interlocked.Add(ref _recoveredSessions, recoveredCount);
            Interlocked.Exchange(ref _lastRecoveryUtcTicks, DateTime.UtcNow.Ticks);
            Interlocked.Exchange(ref _lastRecoveryDurationMs, durationMs);
        }
    }

    public AttendanceSessionRecoveryMetricsSnapshot GetSnapshot()
    {
        var runs = Interlocked.Read(ref _recoveryRuns);
        var recovered = Interlocked.Read(ref _recoveredSessions);
        var lastTicks = Interlocked.Read(ref _lastRecoveryUtcTicks);
        var lastDurationMs = Interlocked.Read(ref _lastRecoveryDurationMs);
        var pending = Volatile.Read(ref _pendingRecoveries);

        return new AttendanceSessionRecoveryMetricsSnapshot(
            RecoveryRuns: runs,
            RecoveredSessions: recovered,
            LastRecoveryUtc: lastTicks > 0 ? new DateTime(lastTicks, DateTimeKind.Utc) : null,
            LastRecoveryDurationMs: lastDurationMs >= 0 ? lastDurationMs : null,
            PendingRecoveries: pending);
    }
}
