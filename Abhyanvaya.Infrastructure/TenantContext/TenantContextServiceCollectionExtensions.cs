using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Abhyanvaya.Infrastructure.TenantContext;

public static class TenantContextServiceCollectionExtensions
{
    public static IServiceCollection AddTenantContextPlatform(this IServiceCollection services)
    {
        services.AddScoped<ITenantContextStore, DistributedCacheTenantContextStore>();
        services.AddScoped<ITenantContextCollegeCatalog, TenantContextCollegeCatalog>();
        services.AddScoped<ITenantContextService, TenantContextService>();
        return services;
    }
}
