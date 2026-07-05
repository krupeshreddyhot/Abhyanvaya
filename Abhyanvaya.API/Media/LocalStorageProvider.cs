using Microsoft.Extensions.Options;

namespace Abhyanvaya.API.Media;

/// <summary>Branding files under <see cref="ResolveRootDirectory"/> (default wwwroot/branding).</summary>
public sealed class LocalStorageProvider : IStorageProvider
{
    /// <summary>Machine-readable id used in <c>Media:Provider</c> configuration and provider lookups.</summary>
    public const string Id = "local";

    private readonly IWebHostEnvironment _env;
    private readonly MediaOptions _mediaOptions;
    private readonly ILogger<LocalStorageProvider> _logger;

    public LocalStorageProvider(
        IWebHostEnvironment env,
        IOptions<MediaOptions> mediaOptions,
        ILogger<LocalStorageProvider> logger)
    {
        _env = env;
        _mediaOptions = mediaOptions.Value;
        _logger = logger;
    }

    public string ProviderName => Id;

    public string DisplayName => "Local File System";

    public string ProviderType => "FileSystem";

    public string ResolveRootDirectory()
    {
        var configured = _mediaOptions.PhysicalRoot?.Trim();
        if (!string.IsNullOrEmpty(configured))
            return configured;

        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        return Path.Combine(webRoot, "branding");
    }

    public async Task WriteObjectAsync(
        string relativeKey,
        ReadOnlyMemory<byte> content,
        StorageWriteOptions options,
        CancellationToken cancellationToken)
    {
        var fullPath = ResolveFullPath(relativeKey);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllBytesAsync(fullPath, content.ToArray(), cancellationToken);
    }

    public Task<Stream> ReadObjectAsync(string relativeKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = ResolveFullPath(relativeKey);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Object not found: {relativeKey}", fullPath);

        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(string relativeKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(ResolveFullPath(relativeKey)));
    }

    public Task DeleteObjectAsync(string relativeKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = ResolveFullPath(relativeKey);
        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    public Task<StorageHealthResult> CheckHealthAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var root = ResolveRootDirectory();
            Directory.CreateDirectory(root);
            return Task.FromResult(new StorageHealthResult(true, $"Local branding directory is accessible: {root}"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local branding directory health check failed.");
            return Task.FromResult(new StorageHealthResult(false, "Local branding directory is not accessible."));
        }
    }

    private string ResolveFullPath(string relativeKey)
    {
        var root = Path.GetFullPath(ResolveRootDirectory());
        Directory.CreateDirectory(root);

        var normalized = StorageKeyHelper.NormalizeRelativeKey(relativeKey);
        var fullPath = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(root, fullPath);
        if (relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || string.Equals(relative, "..", StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid storage key.");

        return fullPath;
    }
}
