using Microsoft.Extensions.Options;

namespace Abhyanvaya.API.Media;

/// <summary>Registers storage providers and <see cref="IMediaStorageService"/> for tenant media.</summary>
public static class MediaStorageServiceCollectionExtensions
{
    /// <summary>Adds local/S3 storage providers, factory, and scoped <see cref="MediaStorageService"/>.</summary>
    public static IServiceCollection AddMediaStorage(this IServiceCollection services)
    {
        services.ConfigureOptions<ConfigureMediaOptions>();
        services.AddOptions<MediaOptions>()
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<MediaOptions>, MediaOptionsValidator>();

        services.AddSingleton<LocalStorageProvider>();
        services.AddSingleton<S3StorageProvider>();
        services.AddSingleton<IStorageProviderFactory, StorageProviderFactory>();
        services.AddScoped<IMediaStorageService, MediaStorageService>();
        return services;
    }
}
