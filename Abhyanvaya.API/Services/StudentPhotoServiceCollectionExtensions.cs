namespace Abhyanvaya.API.Services;

/// <summary>Registers student photo upload services.</summary>
public static class StudentPhotoServiceCollectionExtensions
{
    /// <summary>Adds scoped <see cref="IStudentPhotoService"/>.</summary>
    public static IServiceCollection AddStudentPhotoServices(this IServiceCollection services)
    {
        services.AddScoped<IStudentPhotoService, StudentPhotoService>();
        return services;
    }
}
