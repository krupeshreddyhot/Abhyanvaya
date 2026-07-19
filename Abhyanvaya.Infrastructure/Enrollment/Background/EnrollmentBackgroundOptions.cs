namespace Abhyanvaya.Infrastructure.Enrollment.Background;

public sealed class EnrollmentBackgroundOptions
{
    public const string SectionName = "EnrollmentBackground";

    public bool Enabled { get; set; } = true;

    public int WorkerCount { get; set; } = 2;

    public int PollIntervalSeconds { get; set; } = 5;

    public int LeaseDurationSeconds { get; set; } = 120;

    public int HeartbeatIntervalSeconds { get; set; } = 30;

    public int ClaimBatchSize { get; set; } = 1;
}

public sealed class EnrollmentRecoveryOptions
{
    public const string SectionName = "EnrollmentRecovery";

    public bool Enabled { get; set; } = true;

    public int TimeoutMinutes { get; set; } = 15;

    public int ScanIntervalSeconds { get; set; } = 60;

    public int MaxRecoveriesPerRun { get; set; } = 50;

    public int MaxRetryCount { get; set; } = 5;
}
