using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Abhyanvaya.Infrastructure.EnrollmentApi;

public static class EnrollmentApiServiceCollectionExtensions
{
    public static IServiceCollection AddEnrollmentApiPlatform(this IServiceCollection services)
    {
        services.AddScoped<IEnrollmentDashboardService, EnrollmentDashboardService>();
        services.AddScoped<IEnrollmentReadinessService, EnrollmentReadinessService>();
        services.AddScoped<IEnrollmentHistoryService, EnrollmentHistoryService>();
        services.AddScoped<IBatchCancellationService, BatchCancellationService>();
        services.AddScoped<IBatchRetryService, BatchRetryService>();

        return services;
    }
}
