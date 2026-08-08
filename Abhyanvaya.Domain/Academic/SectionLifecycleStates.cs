namespace Abhyanvaya.Domain.Academic;

/// <summary>
/// AI29.1B — Canonical section lifecycle state names (string constants, not business enums).
/// Persistence uses <see cref="Entities.Academic.Section.Status"/> with these values.
/// </summary>
public static class SectionLifecycleStates
{
    public const string Draft = "Draft";
    public const string Planning = "Planning";
    public const string Open = "Open";
    public const string Active = "Active";
    public const string Locked = "Locked";
    public const string Merged = "Merged";
    public const string Split = "Split";
    public const string Closed = "Closed";
    public const string Archived = "Archived";

    /// <summary>Legacy AI29 values mapped into the enterprise lifecycle.</summary>
    public const string LegacyInactive = "Inactive";
    public const string LegacyUnassigned = "Unassigned";

    public static IReadOnlyList<string> All { get; } =
    [
        Draft, Planning, Open, Active, Locked, Merged, Split, Closed, Archived
    ];

    public static string Normalize(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return Draft;
        var s = status.Trim();
        if (string.Equals(s, LegacyInactive, StringComparison.OrdinalIgnoreCase)) return Closed;
        if (string.Equals(s, LegacyUnassigned, StringComparison.OrdinalIgnoreCase)) return Draft;
        foreach (var known in All)
        {
            if (string.Equals(known, s, StringComparison.OrdinalIgnoreCase)) return known;
        }
        return s;
    }
}
