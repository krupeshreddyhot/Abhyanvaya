using System.Collections.Concurrent;
using Abhyanvaya.Application.AIOperations;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Operations;

public sealed class DefaultAlertPolicy : IAlertPolicy
{
    public AlertSeverity MinimumSeverity => AlertSeverity.Information;

    public bool ShouldEscalate(OperationalAlert alert) => alert.Severity == AlertSeverity.Critical;

    public bool ShouldSuppress(OperationalAlert alert, DateTime utcNow) => alert.Suppressed;

    public bool IsInMaintenanceWindow(DateTime utcNow) => false;
}

public sealed class AIAlertManager : IAIAlertManager
{
    private readonly ConcurrentDictionary<string, OperationalAlert> _alerts = new();
    private readonly IAlertPolicy _policy;
    private readonly ILogger<AIAlertManager> _logger;

    public AIAlertManager(IAlertPolicy policy, ILogger<AIAlertManager> logger)
    {
        _policy = policy;
        _logger = logger;
    }

    public Task<IReadOnlyList<OperationalAlert>> GetActiveAlertsAsync(CancellationToken cancellationToken = default)
    {
        var active = _alerts.Values
            .Where(a => !_policy.ShouldSuppress(a, DateTime.UtcNow))
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.CreatedUtc)
            .ToList();

        return Task.FromResult<IReadOnlyList<OperationalAlert>>(active);
    }

    public Task RaiseAlertAsync(OperationalAlert alert, CancellationToken cancellationToken = default)
    {
        if (_policy.IsInMaintenanceWindow(DateTime.UtcNow))
        {
            return Task.CompletedTask;
        }

        _alerts[alert.AlertId] = alert;
        _logger.LogWarning(
            "Alert raised id={AlertId} severity={Severity} component={Component} message={Message}",
            alert.AlertId,
            alert.Severity,
            alert.Component,
            alert.Message);

        if (_policy.ShouldEscalate(alert))
        {
            _logger.LogCritical("Alert escalated id={AlertId}", alert.AlertId);
        }

        return Task.CompletedTask;
    }

    public async Task EvaluateHealthAlertsAsync(AIPlatformHealthReport health, CancellationToken cancellationToken = default)
    {
        foreach (var check in health.Checks.Where(c => c.Status is AIHealthStatus.Offline or AIHealthStatus.Degraded))
        {
            await RaiseAlertAsync(new OperationalAlert
            {
                AlertId = $"health-{check.ComponentName}-{DateTime.UtcNow:yyyyMMddHHmmss}",
                Severity = check.Status == AIHealthStatus.Offline ? AlertSeverity.Critical : AlertSeverity.Warning,
                Component = check.ComponentName,
                Message = check.Message ?? $"Health status {check.Status}",
                CreatedUtc = DateTime.UtcNow,
            }, cancellationToken);
        }
    }
}

public sealed class AIDiagnosticsService : IAIDiagnosticsService
{
    private readonly IAIHealthRegistry _registry;

    public AIDiagnosticsService(IAIHealthRegistry registry)
    {
        _registry = registry;
    }

    public Task<DiagnosticsReport> GenerateDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        var graph = _registry.RegisteredComponents
            .Select(c => $"Platform -> {c}")
            .ToList();

        return Task.FromResult(new DiagnosticsReport
        {
            DependencyGraph = graph,
            PipelineDiagnostics = new Dictionary<string, string>
            {
                ["enrollment"] = "observer-only",
                ["recognition"] = "observer-only",
                ["attendance"] = "observer-only",
            },
            PerformanceDiagnostics = new Dictionary<string, string>
            {
                ["telemetry"] = "no-pii",
            },
            FailureDiagnostics = new Dictionary<string, string>
            {
                ["mode"] = "read-only",
            },
            ConfigurationDiagnostics = new Dictionary<string, string>
            {
                ["featureFlags"] = "injectable",
            },
            VersionDiagnostics = new Dictionary<string, string>
            {
                ["operations"] = "2.6",
            },
        });
    }
}

public sealed class AICapacityPlanner : IAICapacityPlanner
{
    private readonly IAIMetricsCollector _metricsCollector;
    private readonly ILogger<AICapacityPlanner> _logger;

    public AICapacityPlanner(IAIMetricsCollector metricsCollector, ILogger<AICapacityPlanner> logger)
    {
        _metricsCollector = metricsCollector;
        _logger = logger;
    }

    public async Task<CapacityPlanningReport> GenerateReportAsync(DateTime periodStartUtc, DateTime periodEndUtc, CancellationToken cancellationToken = default)
    {
        var metrics = await _metricsCollector.CollectAsync(cancellationToken);
        _logger.LogInformation("Capacity report generated from {Start} to {End}", periodStartUtc, periodEndUtc);

        return new CapacityPlanningReport
        {
            PeriodStartUtc = periodStartUtc,
            PeriodEndUtc = periodEndUtc,
            PeakRecognition = (int)Math.Min(int.MaxValue, metrics.RecognitionRequests),
            PeakAttendance = (int)Math.Min(int.MaxValue, metrics.AttendanceSessions),
            AverageRecognition = metrics.RecognitionRequests,
            AverageAttendance = metrics.AttendanceSessions,
            CpuTrendPercent = metrics.CpuPercent,
            MemoryTrendPercent = metrics.MemoryBytes / (1024m * 1024m),
            EmbeddingGrowthBytes = 0,
            StorageGrowthBytes = 0,
            QueueGrowth = metrics.QueueDepth,
            ForecastNote = "Architecture-only placeholder; forecasting algorithm not implemented.",
        };
    }
}
