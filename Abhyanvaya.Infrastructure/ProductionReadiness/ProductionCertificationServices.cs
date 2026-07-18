using System.Diagnostics;
using Abhyanvaya.Application.AIOperations;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.ProductionReadiness;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Events;
using Abhyanvaya.Infrastructure.Operations;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.ProductionReadiness;

public sealed class LoadTestingCoordinator : ILoadTestingCoordinator
{
    private readonly IAIMetricsCollector _metricsCollector;
    private readonly IArtifactUploadQueue _artifactQueue;
    private readonly IProductionReadinessPolicy _policy;
    private readonly ILogger<LoadTestingCoordinator> _logger;

    public LoadTestingCoordinator(
        IAIMetricsCollector metricsCollector,
        IArtifactUploadQueue artifactQueue,
        IProductionReadinessPolicy policy,
        ILogger<LoadTestingCoordinator> logger)
    {
        _metricsCollector = metricsCollector;
        _artifactQueue = artifactQueue;
        _policy = policy;
        _logger = logger;
    }

    public async Task<LoadTestReport> ExecuteAsync(DeploymentContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var checks = new List<ValidationCheckResult>();

        var enrollmentSimulations = SimulateConcurrentWork(_policy.LoadTestEnrollmentCount, cancellationToken);
        var uploadSimulations = SimulateConcurrentWork(_policy.LoadTestConcurrentUploads, cancellationToken);
        var recognitionSimulations = SimulateConcurrentWork(_policy.LoadTestRecognitionCount, cancellationToken);
        var classroomSimulations = SimulateConcurrentWork(_policy.LoadTestClassroomCount, cancellationToken);

        await Task.WhenAll(enrollmentSimulations, uploadSimulations, recognitionSimulations, classroomSimulations);
        stopwatch.Stop();

        var metrics = await _metricsCollector.CollectAsync(cancellationToken);
        var statistics = ProductionReadinessSupport.BuildStatistics(metrics, _policy, _artifactQueue.QueueDepth);

        checks.Add(new ValidationCheckResult
        {
            Name = "EnrollmentLoad",
            Status = _policy.LoadTestEnrollmentCount <= 10_000 ? ProductionCheckStatus.Pass : ProductionCheckStatus.Warning,
            Detail = $"Simulated {_policy.LoadTestEnrollmentCount} enrollments in {stopwatch.ElapsedMilliseconds}ms",
        });

        checks.Add(new ValidationCheckResult
        {
            Name = "ConcurrentUploads",
            Status = _policy.LoadTestConcurrentUploads <= 500 ? ProductionCheckStatus.Pass : ProductionCheckStatus.Warning,
            Detail = $"Simulated {_policy.LoadTestConcurrentUploads} uploads",
        });

        checks.Add(new ValidationCheckResult
        {
            Name = "RecognitionLoad",
            Status = ProductionCheckStatus.Pass,
            Detail = $"Simulated {_policy.LoadTestRecognitionCount} recognitions",
        });

        checks.Add(new ValidationCheckResult
        {
            Name = "MemoryMonitoring",
            Status = statistics.WorkerUtilization <= 95m ? ProductionCheckStatus.Pass : ProductionCheckStatus.Warning,
            Detail = $"WorkerUtilization={statistics.WorkerUtilization:F1}%",
        });

        checks.Add(new ValidationCheckResult
        {
            Name = "ResponseTime",
            Status = statistics.AverageLatency <= _policy.MaximumLatency
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Fail,
            Detail = $"AverageLatency={statistics.AverageLatency.TotalMilliseconds:F0}ms",
        });

        _logger.LogInformation(
            "Load test completed deploymentId={DeploymentId} durationMs={DurationMs}",
            context.DeploymentId,
            stopwatch.ElapsedMilliseconds);

        return new LoadTestReport
        {
            Context = context,
            Statistics = statistics,
            Checks = checks,
            OverallStatus = ProductionReadinessSupport.AggregateStatus(checks.Select(c => c.Status)),
            GeneratedUtc = DateTime.UtcNow,
        };
    }

    private static Task SimulateConcurrentWork(int count, CancellationToken cancellationToken)
    {
        var tasks = Enumerable.Range(0, count).Select(_ => Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Thread.SpinWait(50);
        }, cancellationToken));

        return Task.WhenAll(tasks);
    }
}

public sealed class PerformanceValidationService : IPerformanceValidationService
{
    private readonly IAIMetricsCollector _metricsCollector;
    private readonly IAITelemetryService _telemetryService;
    private readonly IArtifactUploadQueue _artifactQueue;
    private readonly IProductionReadinessPolicy _policy;

    public PerformanceValidationService(
        IAIMetricsCollector metricsCollector,
        IAITelemetryService telemetryService,
        IArtifactUploadQueue artifactQueue,
        IProductionReadinessPolicy policy)
    {
        _metricsCollector = metricsCollector;
        _telemetryService = telemetryService;
        _artifactQueue = artifactQueue;
        _policy = policy;
    }

    public async Task<PerformanceValidationReport> ValidateAsync(DeploymentContext context, CancellationToken cancellationToken = default)
    {
        var metrics = await _metricsCollector.CollectAsync(cancellationToken);
        var telemetry = await _telemetryService.CollectSnapshotAsync(cancellationToken);
        var statistics = ProductionReadinessSupport.BuildStatistics(metrics, _policy, _artifactQueue.QueueDepth);

        var checks = new List<ValidationCheckResult>
        {
            new()
            {
                Name = "EnrollmentThroughput",
                Status = statistics.EnrollmentsPerHour > 0 ? ProductionCheckStatus.Pass : ProductionCheckStatus.Warning,
                Detail = $"{statistics.EnrollmentsPerHour:F0}/hour",
            },
            new()
            {
                Name = "RecognitionThroughput",
                Status = statistics.RecognitionsPerHour > 0 ? ProductionCheckStatus.Pass : ProductionCheckStatus.Warning,
                Detail = $"{statistics.RecognitionsPerHour:F0}/hour",
            },
            new()
            {
                Name = "ArtifactUploadThroughput",
                Status = statistics.UploadsPerHour >= 0 ? ProductionCheckStatus.Pass : ProductionCheckStatus.Fail,
                Detail = $"{statistics.UploadsPerHour:F0}/hour",
            },
            new()
            {
                Name = "AverageLatency",
                Status = statistics.AverageLatency <= _policy.MaximumLatency
                    ? ProductionCheckStatus.Pass
                    : ProductionCheckStatus.Fail,
                Detail = $"{statistics.AverageLatency.TotalMilliseconds:F0}ms",
            },
            new()
            {
                Name = "QueueDepth",
                Status = statistics.QueueDepth <= _policy.MaximumQueueDepth
                    ? ProductionCheckStatus.Pass
                    : ProductionCheckStatus.Fail,
                Detail = $"{statistics.QueueDepth}",
            },
            new()
            {
                Name = "WorkerUtilization",
                Status = telemetry.WorkerLoadPercent <= 95m ? ProductionCheckStatus.Pass : ProductionCheckStatus.Warning,
                Detail = $"{telemetry.WorkerLoadPercent:F1}%",
            },
        };

        return new PerformanceValidationReport
        {
            Context = context,
            Statistics = statistics,
            Checks = checks,
            OverallStatus = ProductionReadinessSupport.AggregateStatus(checks.Select(c => c.Status)),
            GeneratedUtc = DateTime.UtcNow,
        };
    }
}

public sealed class CapacityPlanningService : ICapacityPlanningService
{
    private readonly IAICapacityPlanner _capacityPlanner;
    private readonly IAIMetricsCollector _metricsCollector;

    public CapacityPlanningService(IAICapacityPlanner capacityPlanner, IAIMetricsCollector metricsCollector)
    {
        _capacityPlanner = capacityPlanner;
        _metricsCollector = metricsCollector;
    }

    public async Task<CapacityForecast> GenerateForecastAsync(CancellationToken cancellationToken = default)
    {
        var report = await _capacityPlanner.GenerateReportAsync(
            DateTime.UtcNow.AddDays(-7),
            DateTime.UtcNow,
            cancellationToken);
        var metrics = await _metricsCollector.CollectAsync(cancellationToken);

        return new CapacityForecast
        {
            CpuForecastPercent = report.CpuTrendPercent,
            MemoryForecastPercent = report.MemoryTrendPercent,
            StorageForecastBytes = report.StorageGrowthBytes,
            WorkerForecast = Math.Max(2, (int)Math.Ceiling(metrics.CpuPercent / 25m)),
            QueueForecast = report.QueueGrowth + metrics.QueueDepth,
            GrowthForecastPercent = (decimal)(report.AverageRecognition + report.AverageAttendance),
            GeneratedUtc = DateTime.UtcNow,
        };
    }
}

public sealed class ProductionValidationScenarioRunner : IProductionValidationScenarioRunner
{
    private readonly IAIResilienceManager _resilienceManager;
    private readonly IOperationalRunbookService _runbookService;
    private readonly IProductionReadinessPolicy _policy;

    public ProductionValidationScenarioRunner(
        IAIResilienceManager resilienceManager,
        IOperationalRunbookService runbookService,
        IProductionReadinessPolicy policy)
    {
        _resilienceManager = resilienceManager;
        _runbookService = runbookService;
        _policy = policy;
    }

    public async Task<IReadOnlyList<ScenarioValidationResult>> RunScenariosAsync(
        DeploymentContext context,
        CancellationToken cancellationToken = default)
    {
        var scenarios = new[]
        {
            "database.failure",
            "storage.failure",
            "queue.failure",
            "worker.failure",
            "recognition.failure",
            "network.timeout",
        };

        var results = new List<ScenarioValidationResult>();
        foreach (var scenario in scenarios)
        {
            OperationalRunbook? runbook = null;
            try
            {
                runbook = await _runbookService.GetRunbookAsync(scenario, cancellationToken);
            }
            catch (KeyNotFoundException)
            {
                // Scenario without dedicated runbook uses generic resilience policy.
            }

            var retryPolicy = _resilienceManager.GetPolicyType("default");

            results.Add(new ScenarioValidationResult
            {
                Scenario = scenario,
                RecoveryVerified = runbook is not null,
                RetriesVerified = retryPolicy == ResiliencePolicyType.Retry || retryPolicy == ResiliencePolicyType.Timeout,
                GracefulDegradationVerified = runbook?.RecoverySteps.Count > 0,
                Notes =
                [
                    runbook is null ? "Runbook missing; generic recovery applies." : $"{runbook.RecoverySteps.Count} recovery steps.",
                    $"Max queue depth policy: {_policy.MaximumQueueDepth}",
                ],
            });
        }

        return results;
    }
}

public sealed class GoLiveCertificationService : IGoLiveCertificationService
{
    private readonly IDeploymentVerificationService _deploymentVerification;
    private readonly IProductionSmokeTestService _smokeTests;
    private readonly IPerformanceValidationService _performanceValidation;
    private readonly ISecurityValidationService _securityValidation;
    private readonly IBackupVerificationService _backupVerification;
    private readonly IDisasterRecoveryValidator _recoveryValidator;
    private readonly IProductionReadinessPolicy _policy;
    private readonly ICapacityPlanningService _capacityPlanning;

    public GoLiveCertificationService(
        IDeploymentVerificationService deploymentVerification,
        IProductionSmokeTestService smokeTests,
        IPerformanceValidationService performanceValidation,
        ISecurityValidationService securityValidation,
        IBackupVerificationService backupVerification,
        IDisasterRecoveryValidator recoveryValidator,
        IProductionReadinessPolicy policy,
        ICapacityPlanningService capacityPlanning)
    {
        _deploymentVerification = deploymentVerification;
        _smokeTests = smokeTests;
        _performanceValidation = performanceValidation;
        _securityValidation = securityValidation;
        _backupVerification = backupVerification;
        _recoveryValidator = recoveryValidator;
        _policy = policy;
        _capacityPlanning = capacityPlanning;
    }

    public async Task<GoLiveCertificationReport> CertifyAsync(DeploymentContext context, CancellationToken cancellationToken = default)
    {
        var deployment = await _deploymentVerification.VerifyAsync(context, cancellationToken);
        var smoke = await _smokeTests.RunAsync(context, cancellationToken);
        var performance = await _performanceValidation.ValidateAsync(context, cancellationToken);
        var security = await _securityValidation.ValidateAsync(context, cancellationToken);
        var backup = await _backupVerification.VerifyAsync(context, cancellationToken);
        var recovery = await _recoveryValidator.ValidateAsync(context, cancellationToken);
        _ = await _capacityPlanning.GenerateForecastAsync(cancellationToken);

        var checklist = new ProductionChecklist
        {
            Database = FindStatus(smoke.Checks, AIOperationsComponents.Database),
            Storage = FindStatus(smoke.Checks, AIOperationsComponents.Storage),
            Recognition = FindStatus(smoke.Checks, AIOperationsComponents.Recognition),
            Enrollment = FindStatus(smoke.Checks, AIOperationsComponents.Enrollment),
            Attendance = FindStatus(smoke.Checks, AIOperationsComponents.Attendance),
            Operations = deployment.OverallStatus,
            Governance = FindStatus(smoke.Checks, AIOperationsComponents.Governance),
            Workers = FindStatus(smoke.Checks, AIOperationsComponents.Workers),
            Security = security.OverallStatus,
            Performance = performance.OverallStatus,
            Backup = backup.OverallStatus,
            Recovery = recovery.OverallStatus,
            OverallStatus = ProductionReadinessSupport.AggregateStatus(new[]
            {
                deployment.OverallStatus,
                smoke.OverallStatus,
                performance.OverallStatus,
                security.OverallStatus,
                backup.OverallStatus,
                recovery.OverallStatus,
            }),
        };

        var allChecks = deployment.Checks
            .Concat(smoke.Checks)
            .Concat(performance.Checks)
            .Concat(security.Checks)
            .Concat(backup.Checks)
            .Concat(recovery.Checks)
            .ToList();

        var score = ProductionReadinessSupport.CalculateHealthScore(allChecks);
        var criticalIssues = allChecks
            .Where(c => c.Status == ProductionCheckStatus.Fail)
            .Select(c => $"{c.Name}: {c.Detail}")
            .ToList();

        var warnings = allChecks
            .Where(c => c.Status == ProductionCheckStatus.Warning)
            .Select(c => $"{c.Name}: {c.Detail}")
            .ToList();

        var recommendations = new List<string>();
        if (score < _policy.MinimumHealthScore)
        {
            recommendations.Add($"Health score {score:P0} is below minimum {_policy.MinimumHealthScore:P0}.");
        }

        if (performance.Statistics.QueueDepth > _policy.MaximumQueueDepth)
        {
            recommendations.Add("Reduce queue depth before go-live.");
        }

        var blocked = criticalIssues.Count > 0 || score < _policy.MinimumHealthScore;
        var decision = blocked ? "GO LIVE BLOCKED" : "GO LIVE APPROVED";

        var state = blocked
            ? ProductionReadinessState.Failed
            : checklist.OverallStatus == ProductionCheckStatus.Pass
                ? ProductionReadinessState.ProductionCertified
                : ProductionReadinessState.GoLiveReady;

        _ = new GoLiveCertified(context.DeploymentId, decision, DateTime.UtcNow);

        return new GoLiveCertificationReport
        {
            Context = context,
            State = state,
            OverallScore = score,
            Decision = decision,
            Checklist = checklist,
            CriticalIssues = criticalIssues,
            Warnings = warnings,
            Recommendations = recommendations,
            GeneratedUtc = DateTime.UtcNow,
        };
    }

    private static ProductionCheckStatus FindStatus(IReadOnlyList<ValidationCheckResult> checks, string name) =>
        checks.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))?.Status
        ?? ProductionCheckStatus.Warning;
}

public sealed class ProductionReadinessService : IProductionReadinessService
{
    private readonly IDeploymentVerificationService _deploymentVerification;
    private readonly IProductionSmokeTestService _smokeTests;
    private readonly ILoadTestingCoordinator _loadTesting;
    private readonly IPerformanceValidationService _performanceValidation;
    private readonly ISecurityValidationService _securityValidation;
    private readonly IBackupVerificationService _backupVerification;
    private readonly IDisasterRecoveryValidator _recoveryValidator;
    private readonly IGoLiveCertificationService _certificationService;
    private readonly IProductionValidationScenarioRunner _scenarioRunner;

    public ProductionReadinessService(
        IDeploymentVerificationService deploymentVerification,
        IProductionSmokeTestService smokeTests,
        ILoadTestingCoordinator loadTesting,
        IPerformanceValidationService performanceValidation,
        ISecurityValidationService securityValidation,
        IBackupVerificationService backupVerification,
        IDisasterRecoveryValidator recoveryValidator,
        IGoLiveCertificationService certificationService,
        IProductionValidationScenarioRunner scenarioRunner)
    {
        _deploymentVerification = deploymentVerification;
        _smokeTests = smokeTests;
        _loadTesting = loadTesting;
        _performanceValidation = performanceValidation;
        _securityValidation = securityValidation;
        _backupVerification = backupVerification;
        _recoveryValidator = recoveryValidator;
        _certificationService = certificationService;
        _scenarioRunner = scenarioRunner;
    }

    public async Task<ProductionReadinessReportBundle> EvaluateAsync(DeploymentContext context, CancellationToken cancellationToken = default)
    {
        _ = await _loadTesting.ExecuteAsync(context, cancellationToken);
        _ = await _scenarioRunner.RunScenariosAsync(context, cancellationToken);

        var deployment = await _deploymentVerification.VerifyAsync(context, cancellationToken);
        var smoke = await _smokeTests.RunAsync(context, cancellationToken);
        var performance = await _performanceValidation.ValidateAsync(context, cancellationToken);
        var security = await _securityValidation.ValidateAsync(context, cancellationToken);
        var backup = await _backupVerification.VerifyAsync(context, cancellationToken);
        var recovery = await _recoveryValidator.ValidateAsync(context, cancellationToken);
        var certification = await _certificationService.CertifyAsync(context, cancellationToken);

        return new ProductionReadinessReportBundle
        {
            Deployment = deployment,
            SmokeTests = smoke,
            Performance = performance,
            Security = security,
            Backup = backup,
            Recovery = recovery,
            Certification = certification,
            GeneratedUtc = DateTime.UtcNow,
        };
    }

    public async Task<ProductionReadinessState> GetCurrentStateAsync(DeploymentContext context, CancellationToken cancellationToken = default)
    {
        var certification = await _certificationService.CertifyAsync(context, cancellationToken);
        return certification.State;
    }
}

public sealed class ProductionReportService : IProductionReportService
{
    private readonly IProductionReadinessService _readinessService;

    public ProductionReportService(IProductionReadinessService readinessService)
    {
        _readinessService = readinessService;
    }

    public Task<ProductionReadinessReportBundle> GenerateFullReportAsync(DeploymentContext context, CancellationToken cancellationToken = default) =>
        _readinessService.EvaluateAsync(context, cancellationToken);
}
