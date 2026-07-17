namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Computes content checksums for enrollment artifact integrity and duplicate detection.</summary>
public interface IChecksumService
{
    /// <summary>Returns lowercase SHA-256 hex digest (64 chars).</summary>
    string ComputeSha256Hex(ReadOnlySpan<byte> content);

    /// <summary>Computes SHA-256 while reading the stream; resets stream position to 0 when done.</summary>
    Task<string> ComputeSha256HexAsync(Stream content, CancellationToken cancellationToken = default);
}
