using Abhyanvaya.Application.Common.Interfaces;

namespace Abhyanvaya.API.Media;

/// <summary>
/// Application-layer media storage adapter backed by <see cref="IStorageProvider"/>.
/// </summary>
public sealed class ApplicationMediaStorageService : Abhyanvaya.Application.Common.Interfaces.IMediaStorageService
{
    private readonly IStorageProviderFactory _providerFactory;

    public ApplicationMediaStorageService(IStorageProviderFactory providerFactory)
    {
        _providerFactory = providerFactory;
    }

    public async Task SaveOriginalObjectAsync(
        string relativeKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativeKey))
        {
            throw new ArgumentException("Storage key is required.", nameof(relativeKey));
        }

        await using var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken);
        var provider = _providerFactory.GetActiveProvider();
        var writeOptions = new StorageWriteOptions
        {
            ContentType = contentType,
            CacheControl = "public,max-age=86400",
        };

        await provider.WriteObjectAsync(relativeKey.Trim('/'), ms.ToArray(), writeOptions, cancellationToken);
    }

    public Task DeleteObjectAsync(string relativeKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativeKey))
        {
            return Task.CompletedTask;
        }

        var provider = _providerFactory.GetActiveProvider();
        return provider.DeleteObjectAsync(relativeKey.Trim('/'), cancellationToken);
    }
}
