namespace Abhyanvaya.Domain.Enums;

public enum ProductionReadinessState
{
    NotStarted = 0,
    EnvironmentValidated = 1,
    SmokeTestsPassed = 2,
    PerformanceValidated = 3,
    SecurityValidated = 4,
    BackupValidated = 5,
    RecoveryValidated = 6,
    GoLiveReady = 7,
    ProductionCertified = 8,
    Failed = 9,
}
