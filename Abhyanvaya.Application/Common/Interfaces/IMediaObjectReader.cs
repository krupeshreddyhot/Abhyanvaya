namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Reads persisted media variant bytes from tenant storage (local or S3).
/// </summary>
public interface IMediaObjectReader
{
    Task<byte[]> ReadVariantAsync(
        string storageBasePath,
        string variant = "original",
        CancellationToken cancellationToken = default);

    Task<byte[]> ReadObjectAsync(string relativeKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// AI19.MEDIA.3.2 — opens a read stream for <paramref name="relativeKey"/> from the active storage
    /// provider without buffering the object into a byte array. Intended for HTTP retrieval
    /// (<c>MediaController</c>), where the caller hands the returned <see cref="Stream"/> straight to a
    /// <see cref="Microsoft.AspNetCore.Mvc.FileStreamResult"/> instead of materializing the whole object.
    /// Throws <see cref="FileNotFoundException"/> when the object does not exist on the active provider —
    /// the same contract <see cref="Abhyanvaya.Application"/>'s existing callers already rely on implicitly
    /// via the underlying storage provider.
    /// </summary>
    Task<Stream> OpenReadAsync(string relativeKey, CancellationToken cancellationToken = default);
}
