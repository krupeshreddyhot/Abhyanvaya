namespace Abhyanvaya.API.Media;

/// <summary>Produces safe file names for uploads (no path segments or traversal).</summary>
public static class MediaFileNameHelper
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    /// <summary>
    /// Returns a safe file name without directory segments, or null when the input is invalid or traversal is detected.
    /// </summary>
    public static string? SanitizeOriginalFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var normalized = fileName.Replace('\\', '/').Trim();
        if (normalized.Contains('/', StringComparison.Ordinal)
            || normalized.Contains("..", StringComparison.Ordinal))
            return null;

        var baseName = Path.GetFileName(normalized);
        if (string.IsNullOrWhiteSpace(baseName))
            return null;

        var trimmed = baseName.Trim().Trim('.');
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;

        if (trimmed.IndexOfAny(InvalidFileNameChars) >= 0)
            return null;

        return trimmed;
    }

    /// <summary>Generates a safe storage file name while preserving the extension when valid.</summary>
    public static string GenerateSafeFileName(string? originalFileName, string fallbackExtension = ".webp")
    {
        var sanitized = SanitizeOriginalFileName(originalFileName);
        var extension = Path.GetExtension(sanitized ?? string.Empty);
        if (string.IsNullOrWhiteSpace(extension) || extension.Length > 10)
            extension = fallbackExtension;

        return $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
    }
}
