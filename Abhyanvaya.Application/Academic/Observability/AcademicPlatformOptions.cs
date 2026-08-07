namespace Abhyanvaya.Application.Academic.Observability;

/// <summary>AI29.1A.7 — Feature flags for academic platform observability.</summary>
public sealed class AcademicPlatformOptions
{
    public const string SectionName = "AcademicPlatform";

    public bool EnableTelemetry { get; set; } = true;
    public bool EnablePerformanceMetrics { get; set; } = true;
    public bool EnableArchitectureMetrics { get; set; } = true;
    public bool EnableSnapshots { get; set; }
}

/// <summary>Documented performance budgets (ms).</summary>
public static class AcademicPerformanceBudgets
{
    public const double HierarchyCacheMs = 50;
    public const double StatisticsCacheMs = 30;
    public const double TreeBuildMs = 100;
    public const double SearchMs = 40;
    public const double BreadcrumbMs = 20;
    public const double AcademicStructureApiMs = 150;
}
