namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Persists binary media objects via the platform storage provider (local or S3).
/// </summary>
public interface IMediaStorageService
{
    Task SaveOriginalObjectAsync(
        string relativeKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task DeleteObjectAsync(string relativeKey, CancellationToken cancellationToken = default);
}
