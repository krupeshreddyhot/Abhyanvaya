namespace Abhyanvaya.Application;

/// <summary>Storage path helpers for attendance session classroom images.</summary>
public static class AttendanceSessionStoragePaths
{
    public static string BuildClassroomImageKey(int tenantId, Guid sessionId, string extension) =>
        BuildClassroomImageKey(tenantId, sessionId, imageSequence: 1, extension);

    /// <summary>AI22.7A Phase 2 — sequenced classroom image object key.</summary>
    public static string BuildClassroomImageKey(int tenantId, Guid sessionId, short imageSequence, string extension)
    {
        var normalizedExtension = NormalizeExtension(extension);
        var sequence = imageSequence < 1 ? (short)1 : imageSequence;
        return sequence == 1
            ? $"attendance/{tenantId}/sessions/{sessionId}/classroom{normalizedExtension}"
            : $"attendance/{tenantId}/sessions/{sessionId}/classroom-{sequence}{normalizedExtension}";
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
