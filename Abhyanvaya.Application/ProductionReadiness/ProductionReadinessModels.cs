using Abhyanvaya.Application.AIOperations;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.ProductionReadiness;

public sealed record DeploymentContext
{
    public required Guid DeploymentId { get; init; }
    public required string Environment { get; init; }
    public required string Version { get; init; }
    public required string BuildNumber { get; init; }
    public required string GitCommit { get; init; }
    public required DateTime DeploymentTime { get; init; }
    public required string Operator { get; init; }
    public required Guid CorrelationId { get; init; }
    public required Guid TraceId { get; init; }
    public required DateTime CreatedUtc { get; init; }
}

public sealed record ProductionChecklist
{
    public ProductionCheckStatus Database { get; init; }
    public ProductionCheckStatus Storage { get; init; }
    public ProductionCheckStatus Recognition { get; init; }
    public ProductionCheckStatus Enrollment { get; init; }
    public ProductionCheckStatus Attendance { get; init; }
    public ProductionCheckStatus Operations { get; init; }
    public ProductionCheckStatus Governance { get; init; }
    public ProductionCheckStatus Workers { get; init; }
    public ProductionCheckStatus Security { get; init; }
    public ProductionCheckStatus Performance { get; init; }
    public ProductionCheckStatus Backup { get; init; }
    public ProductionCheckStatus Recovery { get; init; }
    public required ProductionCheckStatus OverallStatus { get; init; }
}

public sealed record ProductionStatistics
{
    public decimal RequestsPerSecond { get; init; }
    public decimal EnrollmentsPerHour { get; init; }
    public decimal RecognitionsPerHour { get; init; }
    public decimal UploadsPerHour { get; init; }
    public TimeSpan AverageLatency { get; init; }
    public TimeSpan PeakLatency { get; init; }
    public decimal ErrorRate { get; init; }
    public decimal Availability { get; init; }
    public long StorageUsage { get; init; }
    public int QueueDepth { get; init; }
    public decimal WorkerUtilization { get; init; }
}

public sealed record CapacityForecast
{
    public required decimal CpuForecastPercent { get; init; }
    public required decimal MemoryForecastPercent { get; init; }
    public required long StorageForecastBytes { get; init; }
    public required int WorkerForecast { get; init; }
    public required int QueueForecast { get; init; }
    public required decimal GrowthForecastPercent { get; init; }
    public DateTime GeneratedUtc { get; init; }
}

public sealed record ValidationCheckResult
{
    public required string Name { get; init; }
    public required ProductionCheckStatus Status { get; init; }
    public string? Detail { get; init; }
}

public sealed record DeploymentVerificationReport
{
    public required DeploymentContext Context { get; init; }
    public required IReadOnlyList<ValidationCheckResult> Checks { get; init; }
    public required ProductionCheckStatus OverallStatus { get; init; }
    public DateTime GeneratedUtc { get; init; }
}

public sealed record SmokeTestReport
{
    public required DeploymentContext Context { get; init; }
    public required IReadOnlyList<ValidationCheckResult> Checks { get; init; }
    public required ProductionCheckStatus OverallStatus { get; init; }
    public DateTime GeneratedUtc { get; init; }
}

public sealed record LoadTestReport
{
    public required DeploymentContext Context { get; init; }
    public required ProductionStatistics Statistics { get; init; }
    public required IReadOnlyList<ValidationCheckResult> Checks { get; init; }
    public required ProductionCheckStatus OverallStatus { get; init; }
    public DateTime GeneratedUtc { get; init; }
}

public sealed record PerformanceValidationReport
{
    public required DeploymentContext Context { get; init; }
    public required ProductionStatistics Statistics { get; init; }
    public required IReadOnlyList<ValidationCheckResult> Checks { get; init; }
    public required ProductionCheckStatus OverallStatus { get; init; }
    public DateTime GeneratedUtc { get; init; }
}

public sealed record SecurityValidationReport
{
    public required DeploymentContext Context { get; init; }
    public required IReadOnlyList<ValidationCheckResult> Checks { get; init; }
    public required ProductionCheckStatus OverallStatus { get; init; }
    public DateTime GeneratedUtc { get; init; }
}

public sealed record BackupVerificationReport
{
    public required DeploymentContext Context { get; init; }
    public required IReadOnlyList<ValidationCheckResult> Checks { get; init; }
    public required ProductionCheckStatus OverallStatus { get; init; }
    public DateTime GeneratedUtc { get; init; }
}

public sealed record DisasterRecoveryReport
{
    public required DeploymentContext Context { get; init; }
    public required IReadOnlyList<ValidationCheckResult> Checks { get; init; }
    public required ProductionCheckStatus OverallStatus { get; init; }
    public DateTime GeneratedUtc { get; init; }
}

public sealed record GoLiveCertificationReport
{
    public required DeploymentContext Context { get; init; }
    public required ProductionReadinessState State { get; init; }
    public required decimal OverallScore { get; init; }
    public required string Decision { get; init; }
    public required ProductionChecklist Checklist { get; init; }
    public IReadOnlyList<string>? CriticalIssues { get; init; }
    public IReadOnlyList<string>? Warnings { get; init; }
    public IReadOnlyList<string>? Recommendations { get; init; }
    public DateTime GeneratedUtc { get; init; }
}

public sealed record ProductionReadinessReportBundle
{
    public required DeploymentVerificationReport Deployment { get; init; }
    public required SmokeTestReport SmokeTests { get; init; }
    public required PerformanceValidationReport Performance { get; init; }
    public required SecurityValidationReport Security { get; init; }
    public required BackupVerificationReport Backup { get; init; }
    public required DisasterRecoveryReport Recovery { get; init; }
    public required GoLiveCertificationReport Certification { get; init; }
    public DateTime GeneratedUtc { get; init; }
}

public sealed record ScenarioValidationResult
{
    public required string Scenario { get; init; }
    public required bool RecoveryVerified { get; init; }
    public required bool RetriesVerified { get; init; }
    public required bool GracefulDegradationVerified { get; init; }
    public IReadOnlyList<string>? Notes { get; init; }
}

public sealed class ProductionReadinessPolicyOptions
{
    public const string SectionName = "ProductionReadinessPolicy";

    public decimal MinimumHealthScore { get; set; } = 0.8m;
    public int MaximumQueueDepth { get; set; } = 10_000;
    public decimal MinimumStorageAvailability { get; set; } = 0.99m;
    public int MaximumLatencyMilliseconds { get; set; } = 5_000;
    public int MinimumBackupAgeHours { get; set; } = 24;
    public string[] RequiredServices { get; set; } =
    [
        "Database",
        "Storage",
        "Recognition",
        "Attendance",
        "Enrollment",
        "Governance",
        "Operations",
        "Workers",
    ];

    public int LoadTestEnrollmentCount { get; set; } = 10_000;
    public int LoadTestConcurrentUploads { get; set; } = 500;
    public int LoadTestRecognitionCount { get; set; } = 50_000;
    public int LoadTestClassroomCount { get; set; } = 100;
}

public interface IProductionReadinessPolicy
{
    decimal MinimumHealthScore { get; }
    int MaximumQueueDepth { get; }
    decimal MinimumStorageAvailability { get; }
    TimeSpan MaximumLatency { get; }
    TimeSpan MinimumBackupAge { get; }
    IReadOnlyList<string> RequiredServices { get; }
    int LoadTestEnrollmentCount { get; }
    int LoadTestConcurrentUploads { get; }
    int LoadTestRecognitionCount { get; }
    int LoadTestClassroomCount { get; }
}

public sealed class ConfigurableProductionReadinessPolicy : IProductionReadinessPolicy
{
    private readonly ProductionReadinessPolicyOptions _options;

    public ConfigurableProductionReadinessPolicy(Microsoft.Extensions.Options.IOptions<ProductionReadinessPolicyOptions> options)
    {
        _options = options.Value;
    }

    public decimal MinimumHealthScore => _options.MinimumHealthScore;
    public int MaximumQueueDepth => _options.MaximumQueueDepth;
    public decimal MinimumStorageAvailability => _options.MinimumStorageAvailability;
    public TimeSpan MaximumLatency => TimeSpan.FromMilliseconds(_options.MaximumLatencyMilliseconds);
    public TimeSpan MinimumBackupAge => TimeSpan.FromHours(_options.MinimumBackupAgeHours);
    public IReadOnlyList<string> RequiredServices => _options.RequiredServices;
    public int LoadTestEnrollmentCount => _options.LoadTestEnrollmentCount;
    public int LoadTestConcurrentUploads => _options.LoadTestConcurrentUploads;
    public int LoadTestRecognitionCount => _options.LoadTestRecognitionCount;
    public int LoadTestClassroomCount => _options.LoadTestClassroomCount;
}
