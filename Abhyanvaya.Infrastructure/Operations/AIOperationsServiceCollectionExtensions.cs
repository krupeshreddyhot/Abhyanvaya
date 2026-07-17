using Abhyanvaya.Application.AIOperations;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Infrastructure.Operations.HealthProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Operations;

/// <summary>
/// Discovers all registered <see cref="IAIHealthCheckProvider"/> implementations at startup
/// and registers them with <see cref="IAIHealthRegistry"/> (AI20.PHASE2.6).
/// </summary>
public sealed class AIOperationsHealthBootstrapService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AIHealthRegistry _registry;
    private readonly ILogger<AIOperationsHealthBootstrapService> _logger;

    public AIOperationsHealthBootstrapService(
        IServiceScopeFactory scopeFactory,
        AIHealthRegistry registry,
        ILogger<AIOperationsHealthBootstrapService> logger)
    {
        _scopeFactory = scopeFactory;
        _registry = registry;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var providers = scope.ServiceProvider.GetServices<IAIHealthCheckProvider>();
        foreach (var provider in providers)
        {
            _registry.Register(provider);
            _logger.LogInformation("Registered health provider {Component}", provider.ComponentName);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public static class AIOperationsServiceCollectionExtensions
{
    public static IServiceCollection AddAIOperationsPlatform(this IServiceCollection services)
    {
        services.AddSingleton<AIHealthRegistry>();
        services.AddSingleton<IAIHealthRegistry>(sp => sp.GetRequiredService<AIHealthRegistry>());
        services.AddScoped<IAIHealthService, AIHealthService>();

        services.AddScoped<IAIHealthCheckProvider, DatabaseHealthCheckProvider>();
        services.AddScoped<IAIHealthCheckProvider, StorageHealthCheckProvider>();
        services.AddScoped<IAIHealthCheckProvider, RecognitionHealthCheckProvider>();
        services.AddScoped<IAIHealthCheckProvider, AttendanceHealthCheckProvider>();
        services.AddScoped<IAIHealthCheckProvider, EnrollmentHealthCheckProvider>();
        services.AddScoped<IAIHealthCheckProvider, WorkerHealthCheckProvider>();
        services.AddScoped<IAIHealthCheckProvider, ModelRegistryHealthCheckProvider>();
        services.AddScoped<IAIHealthCheckProvider, VectorProviderHealthCheckProvider>();
        services.AddScoped<IAIHealthCheckProvider, BackgroundJobsHealthCheckProvider>();
        services.AddScoped<IAIHealthCheckProvider, GovernanceHealthCheckProvider>();

        services.AddHostedService<AIOperationsHealthBootstrapService>();

        services.AddSingleton<IAITelemetryService, AITelemetryService>();
        services.AddScoped<IAIMetricsCollector, AIMetricsCollector>();
        services.AddSingleton<IAITracingService, AITracingService>();

        services.AddSingleton<IAlertPolicy, DefaultAlertPolicy>();
        services.AddSingleton<IAIAlertManager, AIAlertManager>();
        services.AddScoped<IAIDiagnosticsService, AIDiagnosticsService>();
        services.AddScoped<IAICapacityPlanner, AICapacityPlanner>();

        services.AddSingleton<IAIResilienceManager, AIResilienceManager>();
        services.AddSingleton<IAIFeatureFlagPolicy, DefaultFeatureFlagPolicy>();
        services.AddSingleton<IAIFeatureFlagProvider, AIFeatureFlagProvider>();

        services.AddScoped<IAIComplianceReporter, AIComplianceReporter>();
        services.AddScoped<IAIOperationalDashboardService, AIOperationalDashboardService>();
        services.AddScoped<IAIProductionVerificationService, AIProductionVerificationService>();
        services.AddSingleton<IOperationalRunbookService, OperationalRunbookService>();
        services.AddScoped<ITenantOperationalSummaryService, TenantOperationalSummaryService>();

        return services;
    }
}
