namespace Abhyanvaya.Domain.Academic;

/// <summary>AI29.1B.5 — Supported SectionVersion.Operation values (string constants, not business enums).</summary>
public static class SectionVersionOperations
{
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Merge = "Merge";
    public const string Split = "Split";
    public const string CapacityChange = "CapacityChange";
    public const string LifecycleChange = "LifecycleChange";

    public static IReadOnlyList<string> All { get; } =
    [
        Create, Update, Merge, Split, CapacityChange, LifecycleChange
    ];
}
