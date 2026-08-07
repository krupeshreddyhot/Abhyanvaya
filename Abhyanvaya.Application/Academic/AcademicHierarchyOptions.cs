namespace Abhyanvaya.Application.Academic;

/// <summary>AI29.1A.6 — Feature flags for hierarchy performance tooling.</summary>
public sealed class AcademicHierarchyOptions
{
    public const string SectionName = "AcademicHierarchy";

    /// <summary>Daily hierarchy snapshots disabled by default until analytics need them.</summary>
    public bool EnableDailySnapshots { get; set; }
}
