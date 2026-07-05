namespace Abhyanvaya.API.Media;

/// <summary>
/// Platform media storage settings. Bound from <c>Media:*</c> with fallback to legacy <c>Branding:*</c> keys.
/// </summary>
public sealed class MediaOptions
{
    public const string SectionName = "Media";

    public string Provider { get; set; } = LocalStorageProvider.Id;

    /// <summary>Local provider root directory. When empty, defaults to <c>wwwroot/branding</c>.</summary>
    public string? PhysicalRoot { get; set; }

    /// <summary>Public CDN/base URL for externally served media (used by feature services).</summary>
    public string? PublicBaseUrl { get; set; }

    public S3Options S3 { get; set; } = new();

    /// <summary>Normalized active provider name: <c>local</c> or <c>s3</c>.</summary>
    public string GetActiveProviderName()
    {
        var name = (Provider ?? LocalStorageProvider.Id).Trim().ToLowerInvariant();
        return name == S3StorageProvider.Id
            ? S3StorageProvider.Id
            : LocalStorageProvider.Id;
    }
}

/// <summary>S3-compatible object storage settings (AWS S3, Cloudflare R2, etc.).</summary>
public sealed class S3Options
{
    public string? Bucket { get; set; }

    public string? Region { get; set; }

    public string? Endpoint { get; set; }

    public string? AccessKeyId { get; set; }

    public string? SecretAccessKey { get; set; }

    public bool ForcePathStyle { get; set; }
}
