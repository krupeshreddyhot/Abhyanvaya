namespace Abhyanvaya.Infrastructure.TenantContext;

public sealed class ContextPlatformOptions
{
    public const string SectionName = "TenantContext";

    public int ExpirationHours { get; set; } = 8;

    public int RecentCollegesMax { get; set; } = 10;

    public int CleanupIntervalMinutes { get; set; } = 15;
}
