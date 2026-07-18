using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.FaceEnrollment;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Abhyanvaya.Infrastructure.FaceEnrollment;

public static class FaceEnrollmentServiceCollectionExtensions
{
    public static IServiceCollection AddFaceEnrollmentPipeline(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EnrollmentPolicyOptions>()
            .Bind(configuration.GetSection(EnrollmentPolicyOptions.SectionName));

        services.AddScoped<IEnrollmentPolicy, ConfigurableEnrollmentPolicy>();
        services.AddScoped<EnrollmentFaceAnalysisBridge>();
        services.AddScoped<IFaceDetectionEngine>(sp => sp.GetRequiredService<EnrollmentFaceAnalysisBridge>());
        services.AddScoped<IFaceAlignmentEngine>(sp => sp.GetRequiredService<EnrollmentFaceAnalysisBridge>());

        services.AddScoped<IEnrollmentCoordinator, EnrollmentCoordinator>();
        services.AddScoped<IEnrollmentBatchProcessor, EnrollmentBatchProcessor>();
        services.AddSingleton<IEnrollmentQualityEngine, EnrollmentQualityEngine>();
        services.AddSingleton<IEnrollmentArtifactBuilder, EnrollmentArtifactBuilder>();
        services.AddScoped<IEnrollmentProgressTracker, EnrollmentProgressTracker>();
        services.AddScoped<IEnrollmentFailureHandler, EnrollmentFailureHandler>();
        services.AddScoped<IEnrollmentDuplicateDetectorService, EnrollmentDuplicateDetectorService>();
        services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
        services.AddSingleton<IEnrollmentManifestGenerator, EnrollmentManifestGenerator>();
        services.AddScoped<IEnrollmentReportService, EnrollmentReportService>();
        services.AddSingleton<IArtifactUploadQueue, ArtifactUploadQueue>();
        services.AddScoped<IFaceEnrollmentRecoveryService, FaceEnrollmentRecoveryService>();

        return services;
    }
}
