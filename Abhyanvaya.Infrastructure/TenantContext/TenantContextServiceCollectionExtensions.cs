using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.TenantContext;
using Abhyanvaya.Infrastructure.TenantContext.ContextPersistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Abhyanvaya.Infrastructure.TenantContext;

public static class TenantContextServiceCollectionExtensions
{
    public static IServiceCollection AddTenantContextPlatform(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ContextPlatformOptions>(configuration.GetSection(ContextPlatformOptions.SectionName));

        services.AddScoped<IContextPersistenceProvider, DistributedCacheContextPersistenceProvider>();
        services.AddScoped<ITenantContextStore, DistributedCacheTenantContextStore>();
        services.AddScoped<ITenantContextCollegeCatalog, TenantContextCollegeCatalog>();
        services.AddScoped<IRecentContextRepository, RecentContextRepository>();
        services.AddScoped<IRecentContextService, RecentContextService>();
        services.AddScoped<IContextExpirationService, ContextExpirationService>();
        services.AddScoped<IContextRefreshService, ContextRefreshService>();
        services.AddScoped<IContextCleanupWorker, ContextCleanupWorker>();
        services.AddScoped<IContextDiagnosticsService, ContextDiagnosticsService>();
        services.AddScoped<IContextArchitectureValidator, ContextArchitectureValidator>();
        services.AddScoped<IOperationalContextHierarchyResolver, CollegeOperationalContextHierarchyResolver>();
        services.AddSingleton<IContextOperationalMetricsCollector, ContextOperationalMetricsCollector>();
        services.AddSingleton<InMemoryContextEventPublisher>();
        services.AddSingleton<IContextEventPublisher>(sp => sp.GetRequiredService<InMemoryContextEventPublisher>());
        services.AddSingleton<IContextEventSubscriber>(sp => sp.GetRequiredService<InMemoryContextEventPublisher>());
        services.AddScoped<ITenantContextService, TenantContextService>();
        services.AddHostedService<ContextCleanupBackgroundService>();

        return services;
    }
}
