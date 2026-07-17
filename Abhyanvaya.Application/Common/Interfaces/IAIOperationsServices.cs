using Abhyanvaya.Application.AIOperations;

namespace Abhyanvaya.Application.Common.Interfaces;

public interface IAIHealthCheckProvider
{
    string ComponentName { get; }
    Task<AIHealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);
}

public interface IAIHealthRegistry
{
    IReadOnlyList<string> RegisteredComponents { get; }
    void Register(IAIHealthCheckProvider provider);
}

public interface IAIHealthService
{
    Task<AIPlatformHealthReport> GetPlatformHealthAsync(CancellationToken cancellationToken = default);
    Task<AIHealthCheckResult> GetComponentHealthAsync(string componentName, CancellationToken cancellationToken = default);
}

public interface IAITelemetryService
{
    Task<TelemetrySnapshot> CollectSnapshotAsync(CancellationToken cancellationToken = default);
    void RecordDuration(string metricName, TimeSpan duration);
}

public interface IAIMetricsCollector
{
    Task<OperationalMetricsSnapshot> CollectAsync(CancellationToken cancellationToken = default);
    void Increment(string metricName, long delta = 1);
}

public interface IAITracingService
{
    AITraceContext CreateContext(Guid? correlationId = null, int? tenantId = null, string? pipelineId = null);
    AITraceContext StartSpan(AITraceContext parent, string operationName, string component);
    void EndSpan(AISpan span, bool success);
    IReadOnlyList<AISpan> GetActiveSpans(Guid traceId);
}

public interface IAIAlertManager
{
    Task<IReadOnlyList<OperationalAlert>> GetActiveAlertsAsync(CancellationToken cancellationToken = default);
    Task RaiseAlertAsync(OperationalAlert alert, CancellationToken cancellationToken = default);
    Task EvaluateHealthAlertsAsync(AIPlatformHealthReport health, CancellationToken cancellationToken = default);
}

public interface IAIDiagnosticsService
{
    Task<DiagnosticsReport> GenerateDiagnosticsAsync(CancellationToken cancellationToken = default);
}

public interface IAICapacityPlanner
{
    Task<CapacityPlanningReport> GenerateReportAsync(DateTime periodStartUtc, DateTime periodEndUtc, CancellationToken cancellationToken = default);
}

public interface IAIResilienceManager
{
    ResiliencePolicyType GetPolicyType(string operationKey);
    bool IsCircuitOpen(string operationKey);
    Task<T> ExecuteWithPolicyAsync<T>(string operationKey, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
}

public interface IAIFeatureFlagProvider
{
    Task<bool> IsEnabledAsync(string flagKey, int? tenantId = null, string? environment = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FeatureFlagState>> GetAllFlagsAsync(CancellationToken cancellationToken = default);
}

public interface IAIComplianceReporter
{
    Task<ComplianceAuditReport> GenerateAuditReportAsync(string reportType, CancellationToken cancellationToken = default);
}

public interface IAIOperationalDashboardService
{
    Task<OperationalDashboardModel> GetDashboardAsync(CancellationToken cancellationToken = default);
}

public interface IAIProductionVerificationService
{
    Task<ProductionReadinessReport> VerifyAsync(CancellationToken cancellationToken = default);
}

public interface IOperationalRunbookService
{
    Task<OperationalRunbook> GetRunbookAsync(string scenario, CancellationToken cancellationToken = default);
    IReadOnlyList<string> SupportedScenarios { get; }
}

public interface ITenantOperationalSummaryService
{
    Task<TenantOperationalSummary> GetSummaryAsync(int tenantId, CancellationToken cancellationToken = default);
}
