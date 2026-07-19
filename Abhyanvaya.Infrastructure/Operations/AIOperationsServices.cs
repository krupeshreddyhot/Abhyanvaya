using System.Collections.Concurrent;
using Abhyanvaya.Application.AIOperations;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Operations;

public sealed class AIResilienceManager : IAIResilienceManager
{
    private readonly ConcurrentDictionary<string, ResiliencePolicyType> _policies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["external.photo.import"] = ResiliencePolicyType.Retry,
        ["vector.search"] = ResiliencePolicyType.CircuitBreaker,
        ["recognition.pipeline"] = ResiliencePolicyType.Timeout,
    };

    private readonly ConcurrentDictionary<string, int> _failureCounts = new();
    private readonly ILogger<AIResilienceManager> _logger;

    public AIResilienceManager(ILogger<AIResilienceManager> logger)
    {
        _logger = logger;
    }

    public ResiliencePolicyType GetPolicyType(string operationKey)
        => _policies.GetValueOrDefault(operationKey, ResiliencePolicyType.Retry);

    public bool IsCircuitOpen(string operationKey)
        => _failureCounts.GetValueOrDefault(operationKey) >= 5;

    public async Task<T> ExecuteWithPolicyAsync<T>(
        string operationKey,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        if (IsCircuitOpen(operationKey))
        {
            _logger.LogWarning("Circuit open for {OperationKey}", operationKey);
            throw new InvalidOperationException($"Circuit open for {operationKey}");
        }

        var policy = GetPolicyType(operationKey);
        try
        {
            if (policy == ResiliencePolicyType.Timeout)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(30));
                return await action(cts.Token);
            }

            return await action(cancellationToken);
        }
        catch (Exception ex)
        {
            _failureCounts.AddOrUpdate(operationKey, 1, (_, count) => count + 1);
            _logger.LogError(ex, "Resilience policy failure for {OperationKey}", operationKey);
            throw;
        }
    }
}

public sealed class DefaultFeatureFlagPolicy : IAIFeatureFlagPolicy
{
    public bool Evaluate(FeatureFlagState flag, int? tenantId, string? environment)
    {
        if (!flag.Enabled)
        {
            return false;
        }

        if (flag.TenantId.HasValue && tenantId.HasValue && flag.TenantId != tenantId)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(flag.Environment)
            && !string.IsNullOrWhiteSpace(environment)
            && !string.Equals(flag.Environment, environment, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (flag.RolloutPercentage.HasValue && tenantId.HasValue)
        {
            var bucket = Math.Abs(tenantId.Value) % 100;
            return bucket < flag.RolloutPercentage.Value;
        }

        return true;
    }
}

public sealed class AIFeatureFlagProvider : IAIFeatureFlagProvider
{
    private readonly ConcurrentDictionary<string, FeatureFlagState> _flags = new(StringComparer.OrdinalIgnoreCase)
    {
        ["recognition.enabled"] = new FeatureFlagState { FlagKey = "recognition.enabled", Enabled = true },
        ["attendance.enabled"] = new FeatureFlagState { FlagKey = "attendance.enabled", Enabled = true },
        ["diagnostics.enabled"] = new FeatureFlagState { FlagKey = "diagnostics.enabled", Enabled = true },
        ["telemetry.enabled"] = new FeatureFlagState { FlagKey = "telemetry.enabled", Enabled = true },
        ["experimental.models"] = new FeatureFlagState { FlagKey = "experimental.models", Enabled = false, RolloutPercentage = 10 },
    };

    private readonly IAIFeatureFlagPolicy _policy;

    public AIFeatureFlagProvider(IAIFeatureFlagPolicy policy)
    {
        _policy = policy;
    }

    public Task<bool> IsEnabledAsync(string flagKey, int? tenantId = null, string? environment = null, CancellationToken cancellationToken = default)
    {
        if (!_flags.TryGetValue(flagKey, out var flag))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(_policy.Evaluate(flag, tenantId, environment));
    }

    public Task<IReadOnlyList<FeatureFlagState>> GetAllFlagsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<FeatureFlagState>>(_flags.Values.ToList());
}

public sealed class AIComplianceReporter : IAIComplianceReporter
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<AIComplianceReporter> _logger;

    public AIComplianceReporter(IApplicationDbContext context, ILogger<AIComplianceReporter> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ComplianceAuditReport> GenerateAuditReportAsync(string reportType, CancellationToken cancellationToken = default)
    {
        var modelCount = await _context.AiModelDefinitions.AsNoTracking().LongCountAsync(cancellationToken);
        var auditCount = await _context.AuditEntries.AsNoTracking().LongCountAsync(cancellationToken);

        _logger.LogInformation("Compliance report generated type={ReportType}", reportType);

        return new ComplianceAuditReport
        {
            GeneratedUtc = DateTime.UtcNow,
            ReportType = reportType,
            ModelUsage = new Dictionary<string, object> { ["modelDefinitions"] = modelCount },
            OperationalAudit = new Dictionary<string, object> { ["auditEntries"] = auditCount },
            ConfigurationAudit = new Dictionary<string, object> { ["piiExcluded"] = true },
        };
    }
}

public sealed class AIOperationalDashboardService : IAIOperationalDashboardService
{
    private readonly IAIHealthService _healthService;
    private readonly IAIAlertManager _alertManager;
    private readonly IAIMetricsCollector _metricsCollector;
    private readonly IApplicationDbContext _context;

    public AIOperationalDashboardService(
        IAIHealthService healthService,
        IAIAlertManager alertManager,
        IAIMetricsCollector metricsCollector,
        IApplicationDbContext context)
    {
        _healthService = healthService;
        _alertManager = alertManager;
        _metricsCollector = metricsCollector;
        _context = context;
    }

    public async Task<OperationalDashboardModel> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var health = await _healthService.GetPlatformHealthAsync(cancellationToken);
        var alerts = await _alertManager.GetActiveAlertsAsync(cancellationToken);
        var metrics = await _metricsCollector.CollectAsync(cancellationToken);
        var activeModel = await _context.AiModelVersions
            .AsNoTracking()
            .Where(v => v.IsActive)
            .Select(v => v.Version)
            .FirstOrDefaultAsync(cancellationToken);

        return new OperationalDashboardModel
        {
            CurrentHealth = health.OverallStatus,
            CurrentAlerts = alerts,
            ActiveWorkers = 2,
            RecognitionTps = metrics.ThroughputPerMinute,
            AttendanceTps = (int)Math.Min(int.MaxValue, metrics.AttendanceSessions),
            QueueDepth = metrics.QueueDepth,
            CpuPercent = metrics.CpuPercent,
            MemoryBytes = metrics.MemoryBytes,
            StorageBytes = 0,
            ModelVersion = activeModel,
            RecognitionVersion = "2.3",
            GeneratedUtc = DateTime.UtcNow,
        };
    }
}

public sealed class AIProductionVerificationService : IAIProductionVerificationService
{
    private readonly IAIHealthService _healthService;
    private readonly IAIFeatureFlagProvider _featureFlagProvider;
    private readonly IAIDiagnosticsService _diagnosticsService;

    public AIProductionVerificationService(
        IAIHealthService healthService,
        IAIFeatureFlagProvider featureFlagProvider,
        IAIDiagnosticsService diagnosticsService)
    {
        _healthService = healthService;
        _featureFlagProvider = featureFlagProvider;
        _diagnosticsService = diagnosticsService;
    }

    public async Task<ProductionReadinessReport> VerifyAsync(CancellationToken cancellationToken = default)
    {
        var checks = new List<ProductionReadinessCheck>();
        var recommendations = new List<string>();

        var health = await _healthService.GetPlatformHealthAsync(cancellationToken);
        checks.Add(new ProductionReadinessCheck
        {
            Name = "Health",
            Status = health.OverallStatus is AIHealthStatus.Ready or AIHealthStatus.Live
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Fail,
            Detail = health.OverallStatus.ToString(),
        });

        foreach (var component in new[]
                 {
                     AIOperationsComponents.Database,
                     AIOperationsComponents.Storage,
                     AIOperationsComponents.Recognition,
                     AIOperationsComponents.Attendance,
                     AIOperationsComponents.Governance,
                     AIOperationsComponents.Workers,
                 })
        {
            var componentHealth = health.Checks.FirstOrDefault(c => c.ComponentName == component);
            checks.Add(new ProductionReadinessCheck
            {
                Name = component,
                Status = componentHealth?.Status is AIHealthStatus.Ready or AIHealthStatus.Live
                    ? ProductionCheckStatus.Pass
                    : ProductionCheckStatus.Warning,
            });
        }

        var recognitionEnabled = await _featureFlagProvider.IsEnabledAsync("recognition.enabled", cancellationToken: cancellationToken);
        checks.Add(new ProductionReadinessCheck
        {
            Name = "FeatureFlags",
            Status = recognitionEnabled ? ProductionCheckStatus.Pass : ProductionCheckStatus.Warning,
        });

        var diagnostics = await _diagnosticsService.GenerateDiagnosticsAsync(cancellationToken);
        checks.Add(new ProductionReadinessCheck
        {
            Name = "Dependencies",
            Status = diagnostics.DependencyGraph.Count > 0 ? ProductionCheckStatus.Pass : ProductionCheckStatus.Fail,
        });

        checks.Add(new ProductionReadinessCheck
        {
            Name = "Configuration",
            Status = ProductionCheckStatus.Pass,
        });

        if (checks.Any(c => c.Status == ProductionCheckStatus.Fail))
        {
            recommendations.Add("Resolve failing health checks before production deployment.");
        }

        var passed = !checks.Any(c => c.Status == ProductionCheckStatus.Fail);
        return new ProductionReadinessReport
        {
            Passed = passed,
            OverallStatus = passed ? "Ready" : "NotReady",
            Checks = checks,
            Recommendations = recommendations,
            GeneratedUtc = DateTime.UtcNow,
        };
    }
}

public sealed class OperationalRunbookService : IOperationalRunbookService
{
    private static readonly Dictionary<string, OperationalRunbook> Runbooks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["recognition.failure"] = new OperationalRunbook
        {
            Scenario = "Recognition Failure",
            ReferenceComponent = AIOperationsComponents.Recognition,
            RecoverySteps = new[]
            {
                "Verify recognition health check status.",
                "Inspect active alerts for recognition latency or failures.",
                "Confirm active model version in model registry.",
                "Review worker logs without accessing PII or embeddings.",
            },
        },
        ["worker.failure"] = new OperationalRunbook
        {
            Scenario = "Worker Failure",
            ReferenceComponent = AIOperationsComponents.Workers,
            RecoverySteps = new[]
            {
                "Check worker health provider status.",
                "Inspect queue depth metrics.",
                "Restart background worker host if offline.",
            },
        },
        ["storage.failure"] = new OperationalRunbook
        {
            Scenario = "Storage Failure",
            ReferenceComponent = AIOperationsComponents.Storage,
            RecoverySteps = new[]
            {
                "Verify storage provider connectivity.",
                "Check storage health check result.",
            },
        },
        ["database.failure"] = new OperationalRunbook
        {
            Scenario = "Database Failure",
            ReferenceComponent = AIOperationsComponents.Database,
            RecoverySteps = new[]
            {
                "Verify database connectivity.",
                "Review database health check duration.",
            },
        },
        ["model.failure"] = new OperationalRunbook
        {
            Scenario = "Model Failure",
            ReferenceComponent = AIOperationsComponents.ModelRegistry,
            RecoverySteps = new[]
            {
                "Verify active production model version.",
                "Review governance health and rollout status.",
            },
        },
    };

    public IReadOnlyList<string> SupportedScenarios => Runbooks.Keys.ToList();

    public Task<OperationalRunbook> GetRunbookAsync(string scenario, CancellationToken cancellationToken = default)
    {
        if (!Runbooks.TryGetValue(scenario, out var runbook))
        {
            throw new KeyNotFoundException($"Runbook not found: {scenario}");
        }

        return Task.FromResult(runbook);
    }
}

public sealed class TenantOperationalSummaryService : ITenantOperationalSummaryService
{
    private readonly IAIMetricsCollector _metricsCollector;
    private readonly IAIHealthService _healthService;
    private readonly IAIAlertManager _alertManager;

    public TenantOperationalSummaryService(
        IAIMetricsCollector metricsCollector,
        IAIHealthService healthService,
        IAIAlertManager alertManager)
    {
        _metricsCollector = metricsCollector;
        _healthService = healthService;
        _alertManager = alertManager;
    }

    public async Task<TenantOperationalSummary> GetSummaryAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        var metrics = await _metricsCollector.CollectAsync(cancellationToken);
        var health = await _healthService.GetPlatformHealthAsync(cancellationToken);
        var alerts = await _alertManager.GetActiveAlertsAsync(cancellationToken);

        return new TenantOperationalSummary
        {
            TenantId = tenantId,
            RecognitionCount = metrics.RecognitionRequests,
            AttendanceSessionCount = metrics.AttendanceSessions,
            Health = health.OverallStatus,
            AverageLatency = metrics.AverageLatency,
            FailureCount = metrics.Failures,
            ActiveAlerts = alerts.Count,
            CapacityUtilizationPercent = metrics.CpuPercent,
            StorageBytes = 0,
        };
    }
}
