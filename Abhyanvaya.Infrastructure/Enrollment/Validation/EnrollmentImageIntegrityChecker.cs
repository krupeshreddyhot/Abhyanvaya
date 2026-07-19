using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace Abhyanvaya.Infrastructure.Enrollment.Validation;

internal static class EnrollmentImageIntegrityChecker
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    internal sealed record IntegrityCheckResult(
        bool IsValid,
        string? FailureMessage,
        int? Width,
        int? Height,
        bool IsCorrupt,
        bool IsUnsupportedFormat);

    internal static IntegrityCheckResult ValidateFormat(string fileName, long byteSize, long maxBytes)
    {
        if (byteSize <= 0)
        {
            return new IntegrityCheckResult(false, "Image file is required.", null, null, false, false);
        }

        if (byteSize > maxBytes)
        {
            return new IntegrityCheckResult(false, "Enrollment photo must be 15 MB or smaller.", null, null, false, false);
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || !SupportedExtensions.Contains(extension))
        {
            return new IntegrityCheckResult(
                false,
                "Supported formats: JPG, JPEG, PNG, WebP.",
                null,
                null,
                false,
                true);
        }

        return new IntegrityCheckResult(true, null, null, null, false, false);
    }

    internal static async Task<IntegrityCheckResult> ValidateDecodeAsync(
        Stream imageStream,
        CancellationToken cancellationToken)
    {
        try
        {
            if (imageStream.CanSeek)
            {
                imageStream.Position = 0;
            }

            var info = await Image.IdentifyAsync(imageStream, cancellationToken);
            if (info is null)
            {
                return new IntegrityCheckResult(
                    false,
                    "The image file appears to be corrupt or unreadable.",
                    null,
                    null,
                    true,
                    false);
            }

            if (imageStream.CanSeek)
            {
                imageStream.Position = 0;
            }

            return new IntegrityCheckResult(true, null, info.Width, info.Height, false, false);
        }
        catch (UnknownImageFormatException)
        {
            return new IntegrityCheckResult(
                false,
                "The image file appears to be corrupt or uses an unsupported format.",
                null,
                null,
                true,
                true);
        }
        catch (InvalidImageContentException)
        {
            return new IntegrityCheckResult(
                false,
                "The image file appears to be corrupt or unreadable.",
                null,
                null,
                true,
                false);
        }
    }

    internal static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        if (stream is MemoryStream ms && ms.TryGetBuffer(out var segment) && segment.Count == stream.Length)
        {
            return segment.ToArray();
        }

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }
}
