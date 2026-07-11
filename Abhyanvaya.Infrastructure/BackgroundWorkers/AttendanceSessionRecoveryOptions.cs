namespace Abhyanvaya.Infrastructure.BackgroundWorkers;

/// <summary>
/// Configuration for <see cref="StuckAttendanceSessionRecoveryService"/>.
/// </summary>
public sealed class AttendanceSessionRecoveryOptions
{
    public const string SectionName = "AttendanceSessionRecovery";

    /// <summary>Master on/off switch for the recovery sweep. Defaults to enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How long an <see cref="Domain.Entities.AttendanceSession"/> may remain in
    /// <see cref="Domain.Enums.AttendanceSessionStatus.Processing"/> before it is considered
    /// orphaned (e.g. the worker process crashed or was OOM-killed/restarted mid-job, and the
    /// in-memory recognition queue lost the message) and is automatically moved to
    /// <see cref="Domain.Enums.AttendanceSessionStatus.Failed"/> so the UI stops showing a
    /// permanently-stuck progress bar and the session becomes retryable.
    /// </summary>
    public int TimeoutMinutes { get; set; } = 10;

    /// <summary>How often the sweep scans for orphaned sessions.</summary>
    public int ScanIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Upper bound on how many orphaned sessions a single sweep execution will recover. Any
    /// additional candidates beyond this count are left untouched and picked up on a later scan —
    /// the sweep never attempts to process an unbounded number of rows in one pass.
    /// </summary>
    public int MaxRecoveriesPerRun { get; set; } = 100;
}
