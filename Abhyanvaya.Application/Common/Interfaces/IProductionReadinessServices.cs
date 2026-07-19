using Abhyanvaya.Application.ProductionReadiness;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.Common.Interfaces;

public interface IDeploymentVerificationService
{
    Task<DeploymentVerificationReport> VerifyAsync(DeploymentContext context, CancellationToken cancellationToken = default);
}

public interface IProductionSmokeTestService
{
    Task<SmokeTestReport> RunAsync(DeploymentContext context, CancellationToken cancellationToken = default);
}

public interface ILoadTestingCoordinator
{
    Task<LoadTestReport> ExecuteAsync(DeploymentContext context, CancellationToken cancellationToken = default);
}

public interface IPerformanceValidationService
{
    Task<PerformanceValidationReport> ValidateAsync(DeploymentContext context, CancellationToken cancellationToken = default);
}

public interface IBackupVerificationService
{
    Task<BackupVerificationReport> VerifyAsync(DeploymentContext context, CancellationToken cancellationToken = default);
}

public interface IDisasterRecoveryValidator
{
    Task<DisasterRecoveryReport> ValidateAsync(DeploymentContext context, CancellationToken cancellationToken = default);
}

public interface ISecurityValidationService
{
    Task<SecurityValidationReport> ValidateAsync(DeploymentContext context, CancellationToken cancellationToken = default);
}

public interface IProductionReadinessService
{
    Task<ProductionReadinessReportBundle> EvaluateAsync(DeploymentContext context, CancellationToken cancellationToken = default);
    Task<ProductionReadinessState> GetCurrentStateAsync(DeploymentContext context, CancellationToken cancellationToken = default);
}

public interface IGoLiveCertificationService
{
    Task<GoLiveCertificationReport> CertifyAsync(DeploymentContext context, CancellationToken cancellationToken = default);
}

public interface IProductionReportService
{
    Task<ProductionReadinessReportBundle> GenerateFullReportAsync(DeploymentContext context, CancellationToken cancellationToken = default);
}

public interface ICapacityPlanningService
{
    Task<CapacityForecast> GenerateForecastAsync(CancellationToken cancellationToken = default);
}

public interface IProductionValidationScenarioRunner
{
    Task<IReadOnlyList<ScenarioValidationResult>> RunScenariosAsync(DeploymentContext context, CancellationToken cancellationToken = default);
}
