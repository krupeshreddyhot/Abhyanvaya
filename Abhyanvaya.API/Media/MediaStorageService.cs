using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Abhyanvaya.API.Media;

/// <inheritdoc cref="IMediaStorageService" />
public sealed class MediaStorageService : IMediaStorageService
{
    private readonly IStorageProviderFactory _providerFactory;

    public MediaStorageService(IStorageProviderFactory providerFactory)
    {
        _providerFactory = providerFactory;
    }

    /// <inheritdoc />
    public (bool Ok, string? Error) ValidateRasterUpload(IFormFile file, long maxBytes) =>
        MediaUploadValidator.ValidateRasterUpload(file, maxBytes);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, byte[]>> BuildWebpVariantsAsync(
        Stream imageStream,
        IReadOnlyDictionary<string, int> variantMaxEdges,
        CancellationToken cancellationToken)
    {
        using var image = await Image.LoadAsync(imageStream, cancellationToken);
        var result = new Dictionary<string, byte[]>(variantMaxEdges.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (variant, maxEdge) in variantMaxEdges)
        {
            result[variant] = await BuildVariantBytesAsync(image, maxEdge, cancellationToken);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task SaveVariantsAsync(
        string storageBasePath,
        IReadOnlyDictionary<string, byte[]> variantBytes,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storageBasePath))
            throw new ArgumentException("Storage base path is required.", nameof(storageBasePath));

        var basePath = StorageKeyHelper.NormalizeBasePath(storageBasePath);
        var provider = _providerFactory.GetActiveProvider();
        var writeOptions = new StorageWriteOptions
        {
            ContentType = "image/webp",
            CacheControl = "public,max-age=86400",
        };

        foreach (var (variant, bytes) in variantBytes)
        {
            var relativeKey = $"{basePath}/{variant}.webp";
            await provider.WriteObjectAsync(relativeKey, bytes, writeOptions, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task DeleteVariantsAsync(
        string storageBasePath,
        IEnumerable<string> variantNames,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storageBasePath))
            throw new ArgumentException("Storage base path is required.", nameof(storageBasePath));

        var basePath = StorageKeyHelper.NormalizeBasePath(storageBasePath);
        var provider = _providerFactory.GetActiveProvider();

        foreach (var variant in variantNames)
        {
            if (string.IsNullOrWhiteSpace(variant))
                continue;

            var relativeKey = $"{basePath}/{variant.Trim()}.webp";
            await provider.DeleteObjectAsync(relativeKey, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<(bool Ok, string Provider, string Message)> CheckStorageHealthAsync(CancellationToken cancellationToken)
    {
        var providerName = _providerFactory.GetActiveProviderName();
        var provider = _providerFactory.GetActiveProvider();
        var result = await provider.CheckHealthAsync(cancellationToken).ConfigureAwait(false);
        return (result.Ok, providerName, result.Message);
    }

    /// <inheritdoc />
    public bool IsStorageOrNetworkFailure(Exception ex) => StorageFailureHelper.IsStorageOrNetworkFailure(ex);

    private static async Task<byte[]> BuildVariantBytesAsync(Image source, int maxEdge, CancellationToken cancellationToken)
    {
        using var clone = source.Clone(ctx =>
        {
            ctx.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(maxEdge, maxEdge),
            });
        });

        await using var ms = new MemoryStream();
        await clone.SaveAsWebpAsync(ms, cancellationToken);
        return ms.ToArray();
    }
}
