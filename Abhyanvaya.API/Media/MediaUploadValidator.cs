namespace Abhyanvaya.API.Media;

/// <summary>Validates raster uploads: allowed types, blocked types, size limits, and safe file names.</summary>
public static class MediaUploadValidator
{
    public const long DefaultMaxBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/pjpeg",
        "image/png",
        "image/webp",
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
    };

    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp",
        ".gif",
        ".svg",
        ".exe",
        ".zip",
    };

    private static readonly HashSet<string> BlockedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/bmp",
        "image/x-ms-bmp",
        "image/gif",
        "image/svg+xml",
        "application/zip",
        "application/x-zip-compressed",
        "application/x-msdownload",
    };

    public static (bool Ok, string? Error) ValidateRasterUpload(IFormFile file, long maxBytes = DefaultMaxBytes)
    {
        if (file.Length == 0)
            return (false, "Choose a non-empty image file.");

        if (file.Length > maxBytes)
            return (false, $"File is too large. Maximum size is {maxBytes / (1024 * 1024)} MB.");

        var safeName = MediaFileNameHelper.SanitizeOriginalFileName(file.FileName);
        if (safeName is null)
            return (false, "Invalid file name.");

        var extension = Path.GetExtension(safeName);
        if (!string.IsNullOrEmpty(extension) && BlockedExtensions.Contains(extension))
            return (false, GetBlockedExtensionMessage(extension));

        var contentType = (file.ContentType ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(contentType) && IsBlockedMimeType(contentType, extension))
            return (false, GetBlockedMimeMessage(contentType, extension));

        var extensionAllowed = !string.IsNullOrEmpty(extension) && AllowedExtensions.Contains(extension);
        var contentTypeAllowed = !string.IsNullOrEmpty(contentType) && AllowedMimeTypes.Contains(contentType);

        if (!extensionAllowed && !contentTypeAllowed)
            return (false, "Allowed file types: JPG, JPEG, PNG, or WebP.");

        if (!string.IsNullOrEmpty(extension) && !extensionAllowed)
            return (false, "File extension is not allowed. Use JPG, JPEG, PNG, or WebP.");

        if (!string.IsNullOrEmpty(contentType)
            && !string.Equals(contentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase)
            && !contentTypeAllowed)
            return (false, "File type is not allowed. Use JPG, JPEG, PNG, or WebP.");

        return (true, null);
    }

    private static bool IsBlockedMimeType(string contentType, string extension)
    {
        if (BlockedMimeTypes.Contains(contentType))
            return true;

        if (string.Equals(contentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(extension)
            && BlockedExtensions.Contains(extension))
            return true;

        return false;
    }

    private static string GetBlockedExtensionMessage(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".bmp" => "BMP images are not allowed. Use JPG, JPEG, PNG, or WebP.",
            ".gif" => "GIF images are not allowed. Use JPG, JPEG, PNG, or WebP.",
            ".svg" => "SVG files are not allowed. Use JPG, JPEG, PNG, or WebP.",
            ".exe" => "Executable files are not allowed.",
            ".zip" => "ZIP archives are not allowed.",
            _ => "This file type is not allowed.",
        };

    private static string GetBlockedMimeMessage(string contentType, string extension)
    {
        if (!string.IsNullOrEmpty(extension) && BlockedExtensions.Contains(extension))
            return GetBlockedExtensionMessage(extension);

        return contentType.ToLowerInvariant() switch
        {
            "image/gif" => "GIF images are not allowed. Use JPG, JPEG, PNG, or WebP.",
            "image/svg+xml" => "SVG files are not allowed. Use JPG, JPEG, PNG, or WebP.",
            "image/bmp" or "image/x-ms-bmp" => "BMP images are not allowed. Use JPG, JPEG, PNG, or WebP.",
            "application/zip" or "application/x-zip-compressed" => "ZIP archives are not allowed.",
            _ => "This file type is not allowed.",
        };
    }
}
