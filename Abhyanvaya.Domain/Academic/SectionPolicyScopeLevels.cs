namespace Abhyanvaya.Domain.Academic;

/// <summary>AI29.1B.5 — Policy scope hierarchy (most specific overrides).</summary>
public static class SectionPolicyScopeLevels
{
    public const string Tenant = "Tenant";
    public const string Program = "Program";
    public const string Course = "Course";
    public const string SectionType = "SectionType";

    /// <summary>Higher index = more specific.</summary>
    public static int Specificity(string? level) => level switch
    {
        SectionType => 4,
        Course => 3,
        Program => 2,
        Tenant => 1,
        _ => 0
    };
}
