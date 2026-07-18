using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.PhotoAcquisition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Abhyanvaya.Infrastructure.PhotoAcquisition;

public static class PhotoAcquisitionServiceCollectionExtensions
{
    public static IServiceCollection AddPhotoAcquisitionPlatform(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PhotoAcquisitionOptions>()
            .Bind(configuration.GetSection(PhotoAcquisitionOptions.SectionName));

        services.AddScoped<IStudentPhotoSource, StudentPhotoSourceAdapter>();
        services.AddScoped<IStudentPhotoDownloader, StudentPhotoDownloader>();
        services.AddScoped<IPhotoDownloadCoordinator, PhotoDownloadCoordinator>();
        services.AddSingleton<IPhotoValidationService, PhotoValidationService>();
        services.AddSingleton<IPhotoQualityAssessmentService, PhotoQualityAssessmentService>();
        services.AddSingleton<IPhotoRetryPolicy, PhotoRetryPolicy>();
        services.AddSingleton<IPhotoManifestGenerator, PhotoManifestGenerator>();
        services.AddScoped<IPhotoDownloadRepository, PhotoDownloadRepository>();
        services.AddSingleton<IPhotoDownloadQueue, PhotoDownloadQueue>();
        services.AddScoped<IPhotoAcquisitionReportService, PhotoAcquisitionReportService>();

        return services;
    }
}
