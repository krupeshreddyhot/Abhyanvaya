using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Abhyanvaya.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IStudentService, StudentService>();
        return services;
    }
}
