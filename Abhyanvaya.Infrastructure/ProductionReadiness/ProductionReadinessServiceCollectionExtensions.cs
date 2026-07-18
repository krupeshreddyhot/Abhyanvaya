using Abhyanvaya.Application.ProductionReadiness;
using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Abhyanvaya.Infrastructure.ProductionReadiness;

public static class ProductionReadinessServiceCollectionExtensions
{
    public static IServiceCollection AddProductionReadinessPlatform(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ProductionReadinessPolicyOptions>()
            .Bind(configuration.GetSection(ProductionReadinessPolicyOptions.SectionName));

        services.AddSingleton<IProductionReadinessPolicy, ConfigurableProductionReadinessPolicy>();

        services.AddScoped<IDeploymentVerificationService, DeploymentVerificationService>();
        services.AddScoped<IProductionSmokeTestService, ProductionSmokeTestService>();
        services.AddScoped<ILoadTestingCoordinator, LoadTestingCoordinator>();
        services.AddScoped<IPerformanceValidationService, PerformanceValidationService>();
        services.AddScoped<IBackupVerificationService, BackupVerificationService>();
        services.AddScoped<IDisasterRecoveryValidator, DisasterRecoveryValidator>();
        services.AddScoped<ISecurityValidationService, SecurityValidationService>();
        services.AddScoped<IGoLiveCertificationService, GoLiveCertificationService>();
        services.AddScoped<IProductionReadinessService, ProductionReadinessService>();
        services.AddScoped<IProductionReportService, ProductionReportService>();
        services.AddScoped<ICapacityPlanningService, CapacityPlanningService>();
        services.AddScoped<IProductionValidationScenarioRunner, ProductionValidationScenarioRunner>();

        return services;
    }
}
