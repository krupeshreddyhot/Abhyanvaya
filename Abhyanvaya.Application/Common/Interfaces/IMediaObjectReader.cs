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
}
