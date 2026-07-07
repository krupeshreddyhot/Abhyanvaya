namespace Abhyanvaya.Application;

/// <summary>Storage path helpers for attendance session classroom images.</summary>
public static class AttendanceSessionStoragePaths
{
    public static string BuildClassroomImageKey(int tenantId, Guid sessionId, string extension)
    {
        var normalizedExtension = NormalizeExtension(extension);
        return $"attendance/{tenantId}/sessions/{sessionId}/classroom{normalizedExtension}";
    }

    public static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ".jpg";
        }

        var trimmed = extension.Trim().ToLowerInvariant();
        return trimmed.StartsWith('.') ? trimmed : $".{trimmed}";
    }

    public static string GetContentType(string extension) =>
        NormalizeExtension(extension) switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream",
        };
}
