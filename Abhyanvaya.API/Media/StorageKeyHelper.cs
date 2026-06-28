namespace Abhyanvaya.API.Media;

internal static class StorageKeyHelper
{
    public static string NormalizeRelativeKey(string relativeKey) =>
        NormalizeRelativeKey(relativeKey, requireValid: true);

    internal static string NormalizeRelativeKey(string relativeKey, bool requireValid)
    {
        var normalized = relativeKey.Replace('\\', '/').TrimStart('/');
        if (requireValid && !IsValidStorageKey(normalized))
            throw new InvalidOperationException("Invalid storage key.");

        return normalized;
    }

    public static string NormalizeBasePath(string storageBasePath) =>
        NormalizeBasePath(storageBasePath, requireValid: true);

    internal static string NormalizeBasePath(string storageBasePath, bool requireValid)
    {
        if (string.IsNullOrWhiteSpace(storageBasePath))
            return string.Empty;

        var normalized = storageBasePath.Replace('\\', '/').Trim().Trim('/');
        if (requireValid && !IsValidStorageKey(normalized))
            throw new InvalidOperationException("Invalid storage base path.");

        return normalized;
    }

    /// <summary>Rejects traversal, absolute paths, and empty segments.</summary>
    public static bool IsValidStorageKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        var normalized = key.Replace('\\', '/').Trim().Trim('/');
        if (normalized.Length == 0)
            return false;

        if (normalized.Contains("..", StringComparison.Ordinal))
            return false;

        foreach (var segment in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is "." or "..")
                return false;
        }

        return true;
    }
}
