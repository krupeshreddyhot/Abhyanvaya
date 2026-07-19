using Abhyanvaya.Application.ArtifactStorage;
using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.ArtifactStorage;

public static class ArtifactStorageServiceCollectionExtensions
{
    public static IServiceCollection AddArtifactStoragePlatform(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ArtifactStorageOptions>()
            .Bind(configuration.GetSection(ArtifactStorageOptions.SectionName));
        services.AddOptions<R2StorageOptions>()
            .Bind(configuration.GetSection(R2StorageOptions.SectionName));
        services.AddOptions<ArtifactVerificationPolicyOptions>()
            .Bind(configuration.GetSection(ArtifactVerificationPolicyOptions.SectionName));
        services.AddOptions<ArtifactRetryPolicyOptions>()
            .Bind(configuration.GetSection(ArtifactRetryPolicyOptions.SectionName));
        services.AddOptions<ArtifactRetentionPolicyOptions>()
            .Bind(configuration.GetSection(ArtifactRetentionPolicyOptions.SectionName));

        services.AddSingleton<IArtifactVerificationPolicy, ConfigurableArtifactVerificationPolicy>();
        services.AddSingleton<IArtifactRetryPolicy, ConfigurableArtifactRetryPolicy>();
        services.AddSingleton<IArtifactRetentionPolicy, ConfigurableArtifactRetentionPolicy>();
        services.AddSingleton<IArtifactIntegrityService, ArtifactIntegrityService>();
        services.AddSingleton<IArtifactVersionManager, ArtifactVersionManager>();

        services.AddScoped<LocalArtifactStorageProvider>();
        services.AddScoped<IR2StorageProvider, R2StorageProvider>();
        services.AddScoped<IArtifactStorageProvider>(ArtifactStorageProviderSelection.ResolveActiveProvider);
        services.AddScoped<IArtifactUploadService, ArtifactUploadService>();
        services.AddScoped<IArtifactVerificationService, ArtifactVerificationService>();
        services.AddScoped<IArtifactRegistryRepository, ArtifactRegistryRepository>();
        services.AddScoped<IArtifactManifestRepository, ArtifactManifestRepository>();
        services.AddScoped<IArtifactLifecycleManager, ArtifactLifecycleManager>();
        services.AddScoped<IArtifactReportService, ArtifactReportService>();
        services.AddScoped<IArtifactUploadCoordinator, ArtifactUploadCoordinator>();

        services.AddHostedService<ArtifactUploadBackgroundService>();

        return services;
    }
}
