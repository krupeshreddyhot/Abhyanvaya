namespace Abhyanvaya.Application;

/// <summary>Storage path and public URL helpers for student media.</summary>
public static class StudentMediaPaths
{
    /// <summary>Caller-defined storage base path for a tenant student photo.</summary>
    public static string BuildStoragePath(int tenantId, int studentId) =>
        $"students/{tenantId}/{studentId}";

    /// <summary>Public URL for a WebP variant under <paramref name="photoKey"/>.</summary>
    public static string? BuildVariantPath(
        string? photoKey,
        DateTime? photoUploadedUtc,
        string variant,
        string? publicBaseUrl = null)
    {
        if (string.IsNullOrWhiteSpace(photoKey) || photoUploadedUtc is null)
            return null;

        var v = new DateTimeOffset(DateTime.SpecifyKind(photoUploadedUtc.Value, DateTimeKind.Utc)).ToUnixTimeSeconds();
        var normalizedVariant = variant.Trim().TrimStart('.');
        if (string.IsNullOrWhiteSpace(publicBaseUrl))
            return $"/media/{photoKey.Trim('/')}/{normalizedVariant}.webp?v={v}";

        var trimmed = publicBaseUrl.Trim().TrimEnd('/');
        return $"{trimmed}/{photoKey.Trim('/')}/{normalizedVariant}.webp?v={v}";
    }
}
