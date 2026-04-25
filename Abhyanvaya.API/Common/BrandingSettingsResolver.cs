namespace Abhyanvaya.API.Common;

public static class BrandingSettingsResolver
{
    public static string? Get(IConfiguration configuration, string keyPath)
    {
        var value = configuration[keyPath]?.Trim();
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        // Prefer standard .NET env key form first, then support single-underscore fallback.
        var envDoubleUnderscore = keyPath.Replace(":", "__", StringComparison.Ordinal);
        value = Environment.GetEnvironmentVariable(envDoubleUnderscore)?.Trim();
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        var envSingleUnderscore = keyPath.Replace(":", "_", StringComparison.Ordinal);
        value = Environment.GetEnvironmentVariable(envSingleUnderscore)?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
