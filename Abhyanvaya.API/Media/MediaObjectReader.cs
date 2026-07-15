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
        return await ReadAllBytesAsync(stream, cancellationToken);
    }

    public async Task<byte[]> ReadObjectAsync(string relativeKey, CancellationToken cancellationToken = default)
    {
        var provider = _providerFactory.GetActiveProvider();
        await using var stream = await provider.ReadObjectAsync(relativeKey.Trim('/'), cancellationToken);
        return await ReadAllBytesAsync(stream, cancellationToken);
    }

    // AI19.MEDIA.3.2: deliberate pass-through, no ReadAllBytesAsync buffering — MediaController hands
    // this Stream straight to a FileStreamResult so the object is copied to the HTTP response exactly
    // once (whatever buffering the active IStorageProvider itself already does internally, e.g.
    // S3StorageProvider's single MemoryStream copy of the R2 response, is unchanged and untouched by
    // this method). FileNotFoundException from the active provider propagates unmodified.
    public Task<Stream> OpenReadAsync(string relativeKey, CancellationToken cancellationToken = default)
    {
        var provider = _providerFactory.GetActiveProvider();
        return provider.ReadObjectAsync(relativeKey.Trim('/'), cancellationToken);
    }

    // AI16.RUNTIME.2: both current storage providers (LocalStorageProvider → FileStream,
    // S3StorageProvider → an already-fully-buffered MemoryStream) return a seekable stream with a
    // known Length. The previous implementation always copied that stream into a *second* growable
    // MemoryStream and then called ToArray() for a *third* full-size copy — for a multi-MB classroom
    // photo that is two entirely avoidable duplicate buffers of the same bytes. When the stream is
    // seekable, this reads directly into one exactly-sized array instead. Falls back to the original
    // MemoryStream/ToArray path, unchanged, for any future provider that returns a non-seekable
    // stream (e.g. unbuffered network responses without a known Content-Length) — byte content is
    // identical either way, this only changes how many intermediate buffers are allocated.
    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            var length = checked((int)stream.Length);
            var buffer = new byte[length];
            var totalRead = 0;
            while (totalRead < length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(totalRead), cancellationToken);
                if (read == 0)
                {
                    break; // Defensive only — a seekable stream reporting Length should not end early.
                }

                totalRead += read;
            }

            return buffer;
        }

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }
}
