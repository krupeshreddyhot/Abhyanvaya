namespace Abhyanvaya.Application;

/// <summary>Public URL helpers for attendance session classroom images.</summary>
public static class AttendanceSessionMediaPaths
{
    public static string? BuildMediaUrl(string? relativeKey, DateTime cacheUtc)
    {
        if (string.IsNullOrWhiteSpace(relativeKey))
        {
            return null;
        }

        var v = new DateTimeOffset(DateTime.SpecifyKind(cacheUtc, DateTimeKind.Utc)).ToUnixTimeSeconds();
        return $"/media/{relativeKey.Trim('/')}?v={v}";
    }

    public static string? BuildImageUrl(string? imageKey, DateTime createdUtc, string variant = "annotated")
    {
        if (string.IsNullOrWhiteSpace(imageKey))
        {
            return null;
        }

        if (imageKey.Contains('.', StringComparison.Ordinal))
        {
            return BuildMediaUrl(imageKey, createdUtc);
        }

        var v = new DateTimeOffset(DateTime.SpecifyKind(createdUtc, DateTimeKind.Utc)).ToUnixTimeSeconds();
        var normalizedVariant = variant.Trim().TrimStart('.');
        return $"/media/{imageKey.Trim('/')}/{normalizedVariant}.webp?v={v}";
    }
}
