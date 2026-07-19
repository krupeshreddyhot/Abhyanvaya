using Abhyanvaya.Application.ArtifactStorage;
using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.ArtifactStorage;

public sealed class LocalArtifactStorageProvider : IArtifactStorageProvider
{
    public const string ProviderId = "local";
    private const string DefaultRelativeRoot = "App_Data/artifacts";

    private readonly IHostEnvironment _environment;
    private readonly ArtifactStorageOptions _options;
    private readonly ILogger<LocalArtifactStorageProvider> _logger;

    public LocalArtifactStorageProvider(
        IHostEnvironment environment,
        IOptions<ArtifactStorageOptions> options,
        ILogger<LocalArtifactStorageProvider> logger)
    {
        _environment = environment;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => ProviderId;

    public string Bucket => "local";

    public async Task UploadAsync(
        string storageKey,
        Stream content,
        ArtifactMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveFullPath(storageKey);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, cancellationToken);
    }

    public Task<bool> VerifyExistsAsync(string storageKey, long expectedLength, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = ResolveFullPath(storageKey);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult(false);
        }

        var info = new FileInfo(fullPath);
        return Task.FromResult(info.Length == expectedLength);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = ResolveFullPath(storageKey);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public Task ArchiveAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Artifact archived storageKey={StorageKey}", storageKey);
        return Task.CompletedTask;
    }

    public string ResolveRootDirectory() =>
        ResolveRootDirectory(_environment, _options);

    public static string ResolveRootDirectory(IHostEnvironment environment, ArtifactStorageOptions options)
    {
        var configured = options.PhysicalRoot?.Trim();
        if (string.IsNullOrEmpty(configured))
        {
            configured = DefaultRelativeRoot;
        }

        return Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(environment.ContentRootPath, configured);
    }

    private string ResolveFullPath(string storageKey)
    {
        var root = Path.GetFullPath(ResolveRootDirectory());
        Directory.CreateDirectory(root);

        var normalized = storageKey.Replace('\\', '/').TrimStart('/');
        var fullPath = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(root, fullPath);
        if (relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || string.Equals(relative, "..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid storage key.");
        }

        return fullPath;
    }
}
