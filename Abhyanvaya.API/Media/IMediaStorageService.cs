namespace Abhyanvaya.API.Media;

/// <summary>
/// Raster image processing and persistence for tenant media (logos, photos, certificates, etc.).
/// Uses <see cref="IStorageProvider"/> for local or S3-compatible storage.
/// </summary>
public interface IMediaStorageService
{
    /// <summary>Validates an uploaded raster file against size and allowed MIME/extension rules.</summary>
    (bool Ok, string? Error) ValidateRasterUpload(IFormFile file, long maxBytes);

    /// <summary>Builds WebP variants keyed by variant name (e.g. sm, md, lg) at the given max edge sizes.</summary>
    Task<IReadOnlyDictionary<string, byte[]>> BuildWebpVariantsAsync(
        Stream imageStream,
        IReadOnlyDictionary<string, int> variantMaxEdges,
        CancellationToken cancellationToken);

    /// <summary>Persists variant bytes under <c>{storageBasePath}/{variant}.webp</c> via the active storage provider.</summary>
    /// <param name="storageBasePath">Caller-defined prefix (e.g. <c>branding/{guid}</c>, <c>students/{tenantId}/{studentId}</c>).</param>
    Task SaveVariantsAsync(
        string storageBasePath,
        IReadOnlyDictionary<string, byte[]> variantBytes,
        CancellationToken cancellationToken);

    /// <summary>Deletes variant objects at <c>{storageBasePath}/{variant}.webp</c>. Missing objects are ignored.</summary>
    Task DeleteVariantsAsync(
        string storageBasePath,
        IEnumerable<string> variantNames,
        CancellationToken cancellationToken);

    /// <summary>Runs a health check on the active <see cref="IStorageProvider"/>.</summary>
    Task<(bool Ok, string Provider, string Message)> CheckStorageHealthAsync(CancellationToken cancellationToken);

    /// <summary>True when the exception (or inner chain) indicates storage or network failure.</summary>
    bool IsStorageOrNetworkFailure(Exception ex);
}
