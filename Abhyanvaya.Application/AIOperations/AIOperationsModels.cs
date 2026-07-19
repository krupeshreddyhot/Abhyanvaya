using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.AIOperations;

public enum AlertSeverity
{
    Information = 0,
    Warning = 1,
    Critical = 2,
}

public enum ProductionCheckStatus
{
    Pass = 0,
    Fail = 1,
    Skipped = 2,
    Warning = 3,
}

public enum ResiliencePolicyType
{
    Retry = 0,
    Timeout = 1,
    CircuitBreaker = 2,
    Bulkhead = 3,
    Fallback = 4,
}

public sealed record AITraceContext
{
    public required Guid TraceId { get; init; }
    public required Guid CorrelationId { get; init; }
    public Guid? SessionId { get; init; }
    public string? WorkerId { get; init; }
    public int? TenantId { get; init; }
    public string? PipelineId { get; init; }
    public Guid? ParentSpanId { get; init; }
    public required Guid CurrentSpanId { get; init; }
}

public sealed record AISpan
{
    public required Guid SpanId { get; init; }
    public required Guid TraceId { get; init; }
    public required string OperationName { get; init; }
    public required string Component { get; init; }
    public DateTime StartedUtc { get; init; }
    public DateTime? EndedUtc { get; init; }
    public TimeSpan? Duration { get; init; }
    public bool Success { get; init; }
    public Guid? ParentSpanId { get; init; }
}

public sealed record AIHealthCheckResult
{
    public required string ComponentName { get; init; }
    public required AIHealthStatus Status { get; init; }
    public required string Version { get; init; }
    public IReadOnlyDictionary<string, string>? Dependencies { get; init; }
    public string? Message { get; init; }
    public TimeSpan Duration { get; init; }
}

public sealed record AIPlatformHealthReport
{
    public required AIHealthStatus OverallStatus { get; init; }
    public required IReadOnlyList<AIHealthCheckResult> Checks { get; init; }
    public DateTime GeneratedUtc { get; init; }
}

public sealed record TelemetrySnapshot
{
    public TimeSpan RecognitionDuration { get; init; }
    public TimeSpan AttendanceDuration { get; init; }
    public TimeSpan EmbeddingDuration { get; init; }
    public TimeSpan VectorSearchDuration { get; init; }
    public TimeSpan ModelLoadTime { get; init; }
    public int QueueLength { get; init; }
    public decimal WorkerLoadPercent { get; init; }
    public long MemoryBytes { get; init; }
    public decimal CpuPercent { get; init; }
    public decimal? GpuPercent { get; init; }
}

public sealed record OperationalMetricsSnapshot
{
    public long RecognitionRequests { get; init; }
    public long AttendanceSessions { get; init; }
    public long Failures { get; init; }
    public long Retries { get; init; }
    public long UnknownFaces { get; init; }
    public long ManualReviews { get; init; }
    public TimeSpan AverageLatency { get; init; }
    public int ThroughputPerMinute { get; init; }
    public decimal CpuPercent { get; init; }
    public long MemoryBytes { get; init; }
    public int QueueDepth { get; init; }
    public TimeSpan AverageDatabaseTime { get; init; }
}

public sealed record OperationalAlert
{
    public required string AlertId { get; init; }
    public required AlertSeverity Severity { get; init; }
    public required string Component { get; init; }
    public required string Message { get; init; }
    public DateTime CreatedUtc { get; init; }
    public bool Suppressed { get; init; }
}

public sealed record DiagnosticsReport
{
    public required IReadOnlyList<string> DependencyGraph { get; init; }
    public IReadOnlyDictionary<string, string>? PipelineDiagnostics { get; init; }
    public IReadOnlyDictionary<string, string>? PerformanceDiagnostics { get; init; }
    public IReadOnlyDictionary<string, string>? FailureDiagnostics { get; init; }
    public IReadOnlyDictionary<string, string>? ConfigurationDiagnostics { get; init; }
    public IReadOnlyDictionary<string, string>? VersionDiagnostics { get; init; }
}

public sealed record CapacityPlanningReport
{
    public required DateTime PeriodStartUtc { get; init; }
    public required DateTime PeriodEndUtc { get; init; }
    public int PeakRecognition { get; init; }
    public int PeakAttendance { get; init; }
    public double AverageRecognition { get; init; }
    public double AverageAttendance { get; init; }
    public decimal CpuTrendPercent { get; init; }
    public decimal MemoryTrendPercent { get; init; }
    public long EmbeddingGrowthBytes { get; init; }
    public long StorageGrowthBytes { get; init; }
    public int QueueGrowth { get; init; }
    public string? ForecastNote { get; init; }
}

public sealed record OperationalDashboardModel
{
    public required AIHealthStatus CurrentHealth { get; init; }
    public required IReadOnlyList<OperationalAlert> CurrentAlerts { get; init; }
    public int ActiveWorkers { get; init; }
    public int RecognitionTps { get; init; }
    public int AttendanceTps { get; init; }
    public int QueueDepth { get; init; }
    public decimal CpuPercent { get; init; }
    public long MemoryBytes { get; init; }
    public long StorageBytes { get; init; }
    public string? ModelVersion { get; init; }
    public string? RecognitionVersion { get; init; }
    public DateTime GeneratedUtc { get; init; }
}

public sealed record ProductionReadinessCheck
{
    public required string Name { get; init; }
    public required ProductionCheckStatus Status { get; init; }
    public string? Detail { get; init; }
}

public sealed record ProductionReadinessReport
{
    public required bool Passed { get; init; }
    public required string OverallStatus { get; init; }
    public required IReadOnlyList<ProductionReadinessCheck> Checks { get; init; }
    public IReadOnlyList<string>? Recommendations { get; init; }
    public DateTime GeneratedUtc { get; init; }
}

public sealed record TenantOperationalSummary
{
    public required int TenantId { get; init; }
    public long RecognitionCount { get; init; }
    public long AttendanceSessionCount { get; init; }
    public AIHealthStatus Health { get; init; }
    public TimeSpan AverageLatency { get; init; }
    public long FailureCount { get; init; }
    public int ActiveAlerts { get; init; }
    public decimal CapacityUtilizationPercent { get; init; }
    public long StorageBytes { get; init; }
}

public sealed record ComplianceAuditReport
{
    public required DateTime GeneratedUtc { get; init; }
    public required string ReportType { get; init; }
    public IReadOnlyDictionary<string, object>? ModelUsage { get; init; }
    public IReadOnlyDictionary<string, object>? OperationalAudit { get; init; }
    public IReadOnlyDictionary<string, object>? ConfigurationAudit { get; init; }
}

public sealed record OperationalRunbook
{
    public required string Scenario { get; init; }
    public required IReadOnlyList<string> RecoverySteps { get; init; }
    public string? ReferenceComponent { get; init; }
}

public sealed record FeatureFlagState
{
    public required string FlagKey { get; init; }
    public required bool Enabled { get; init; }
    public int? TenantId { get; init; }
    public string? Environment { get; init; }
    public decimal? RolloutPercentage { get; init; }
}

public interface IAlertPolicy
{
    AlertSeverity MinimumSeverity { get; }
    bool ShouldEscalate(OperationalAlert alert);
    bool ShouldSuppress(OperationalAlert alert, DateTime utcNow);
    bool IsInMaintenanceWindow(DateTime utcNow);
}

public interface IAIFeatureFlagPolicy
{
    bool Evaluate(FeatureFlagState flag, int? tenantId, string? environment);
}
