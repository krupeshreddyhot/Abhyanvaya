namespace Abhyanvaya.API.Media;

/// <summary>Reads, writes, and deletes binary objects in local disk or S3-compatible storage.</summary>
public interface IStorageProvider
{
    /// <summary>Stable machine-readable provider identifier (e.g. <c>Local</c>, <c>S3</c>, <c>AzureBlob</c>).</summary>
    string ProviderName { get; }

    /// <summary>
    /// Human-readable name for logs/diagnostics/UI (e.g. <c>Local File System</c>, <c>Amazon S3</c>).
    /// Consumers (startup diagnostics, health endpoints) should log this instead of switching on <see cref="ProviderName"/>.
    /// </summary>
    string DisplayName { get; }

    /// <summary>Category of the provider (e.g. <c>FileSystem</c>, <c>Cloud Storage</c>).</summary>
    string ProviderType { get; }

    /// <summary>
    /// Persists content at a caller-defined relative key (e.g. <c>{guid}/sm.webp</c>, <c>students/{tenantId}/{studentId}/photo.webp</c>).
    /// Local provider maps keys under its configured root directory.
    /// </summary>
    Task WriteObjectAsync(string relativeKey, ReadOnlyMemory<byte> content, StorageWriteOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a readable stream for the object at <paramref name="relativeKey"/>.
    /// Caller must dispose the returned stream.
    /// </summary>
    /// <exception cref="FileNotFoundException">Object does not exist.</exception>
    Task<Stream> ReadObjectAsync(string relativeKey, CancellationToken cancellationToken);

    /// <summary>True when an object exists at <paramref name="relativeKey"/>.</summary>
    Task<bool> ExistsAsync(string relativeKey, CancellationToken cancellationToken);

    /// <summary>Removes the object at <paramref name="relativeKey"/>; no-op when the object is already absent.</summary>
    Task DeleteObjectAsync(string relativeKey, CancellationToken cancellationToken);

    Task<StorageHealthResult> CheckHealthAsync(CancellationToken cancellationToken);
}
