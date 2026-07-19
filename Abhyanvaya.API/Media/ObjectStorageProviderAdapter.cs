using Abhyanvaya.Application.Common.Interfaces;

namespace Abhyanvaya.API.Media;

/// <summary>
/// Application-layer object storage adapter over the existing AI19 <see cref="IStorageProvider"/> stack.
/// </summary>
public sealed class ObjectStorageProviderAdapter : IObjectStorageProvider
{
    private readonly IStorageProviderFactory _providerFactory;

    public ObjectStorageProviderAdapter(IStorageProviderFactory providerFactory)
    {
        _providerFactory = providerFactory;
    }

    public string ProviderName => _providerFactory.GetActiveProviderName();

    public async Task WriteObjectAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        var provider = _providerFactory.GetActiveProvider();
        await provider.WriteObjectAsync(
            objectKey.Trim('/'),
            buffer.ToArray(),
            new StorageWriteOptions
            {
                ContentType = contentType,
                CacheControl = "private,max-age=31536000,immutable",
            },
            cancellationToken);
    }

    public Task<Stream?> ReadObjectAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var provider = _providerFactory.GetActiveProvider();
        return ReadInternalAsync(provider, objectKey, cancellationToken);
    }

    public Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return Task.CompletedTask;
        }

        var provider = _providerFactory.GetActiveProvider();
        return provider.DeleteObjectAsync(objectKey.Trim('/'), cancellationToken);
    }

    public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var provider = _providerFactory.GetActiveProvider();
        return provider.ExistsAsync(objectKey.Trim('/'), cancellationToken);
    }

    private static async Task<Stream?> ReadInternalAsync(
        IStorageProvider provider,
        string objectKey,
        CancellationToken cancellationToken)
    {
        try
        {
            return await provider.ReadObjectAsync(objectKey.Trim('/'), cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }
}
