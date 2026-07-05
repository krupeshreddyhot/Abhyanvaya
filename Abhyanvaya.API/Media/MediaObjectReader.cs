using Abhyanvaya.API.Media;
using Abhyanvaya.Application.Common.Interfaces;

namespace Abhyanvaya.API.Media;

/// <summary>Reads media variant bytes via the active storage provider.</summary>
public sealed class MediaObjectReader : IMediaObjectReader
{
    private readonly IStorageProviderFactory _providerFactory;

    public MediaObjectReader(IStorageProviderFactory providerFactory)
    {
        _providerFactory = providerFactory;
    }

    public async Task<byte[]> ReadVariantAsync(
        string storageBasePath,
        string variant = "original",
        CancellationToken cancellationToken = default)
    {
        var provider = _providerFactory.GetActiveProvider();
        var key = $"{storageBasePath.Trim('/')}/{variant.TrimStart('.')}.webp";
        await using var stream = await provider.ReadObjectAsync(key, cancellationToken);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }

    public async Task<byte[]> ReadObjectAsync(string relativeKey, CancellationToken cancellationToken = default)
    {
        var provider = _providerFactory.GetActiveProvider();
        await using var stream = await provider.ReadObjectAsync(relativeKey.Trim('/'), cancellationToken);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }
}
