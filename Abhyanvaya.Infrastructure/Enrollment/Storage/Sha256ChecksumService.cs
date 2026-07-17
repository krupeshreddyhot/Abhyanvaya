using System.Security.Cryptography;
using Abhyanvaya.Application.Common.Interfaces;

namespace Abhyanvaya.Infrastructure.Enrollment.Storage;

internal sealed class Sha256ChecksumService : IChecksumService
{
    public string ComputeSha256Hex(ReadOnlySpan<byte> content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    public async Task<string> ComputeSha256HexAsync(Stream content, CancellationToken cancellationToken)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        var hash = await SHA256.HashDataAsync(content, cancellationToken);
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
