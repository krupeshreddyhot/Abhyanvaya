namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Application-layer object storage seam (AI20.PHASE2.1.5). Implemented in the API layer over
/// <c>IStorageProvider</c>; never references provider-specific SDKs from Application/Infrastructure.
/// </summary>
public interface IObjectStorageProvider
{
    string ProviderName { get; }

    Task WriteObjectAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Stream?> ReadObjectAsync(string objectKey, CancellationToken cancellationToken = default);

    Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default);
}
