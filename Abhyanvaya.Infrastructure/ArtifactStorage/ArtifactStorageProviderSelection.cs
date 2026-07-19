using Abhyanvaya.Application.ArtifactStorage;
using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.ArtifactStorage;

public static class ArtifactStorageProviderSelection
{
    public static string ResolveProviderName(ArtifactStorageOptions options, IHostEnvironment environment)
    {
        var configured = options.Provider?.Trim();
        if (!string.IsNullOrEmpty(configured))
        {
            return configured.ToLowerInvariant();
        }

        return environment.IsDevelopment()
            ? LocalArtifactStorageProvider.ProviderId
            : R2StorageProvider.ProviderId;
    }

    public static IArtifactStorageProvider ResolveActiveProvider(IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptions<ArtifactStorageOptions>>().Value;
        var environment = services.GetRequiredService<IHostEnvironment>();
        var provider = ResolveProviderName(options, environment);

        return provider == LocalArtifactStorageProvider.ProviderId
            ? services.GetRequiredService<LocalArtifactStorageProvider>()
            : services.GetRequiredService<IR2StorageProvider>();
    }

    public static string ResolveDisplayName(string providerName) =>
        providerName.Equals(LocalArtifactStorageProvider.ProviderId, StringComparison.OrdinalIgnoreCase)
            ? "Local File System"
            : "Cloudflare R2";
}
