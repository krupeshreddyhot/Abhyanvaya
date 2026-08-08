namespace Abhyanvaya.Domain.Academic;

/// <summary>
/// AI29.1B — Centralized lifecycle transition rules. All status changes must go through this machine
/// (via <c>ISectionLifecycleService</c>); callers must not assign <c>Section.Status</c> ad hoc.
/// </summary>
public static class SectionLifecycleStateMachine
{
    private static readonly Dictionary<string, HashSet<string>> Transitions = new(StringComparer.OrdinalIgnoreCase)
    {
        [SectionLifecycleStates.Draft] = new(StringComparer.OrdinalIgnoreCase)
        {
            SectionLifecycleStates.Planning, SectionLifecycleStates.Open, SectionLifecycleStates.Archived
        },
        [SectionLifecycleStates.Planning] = new(StringComparer.OrdinalIgnoreCase)
        {
            SectionLifecycleStates.Draft, SectionLifecycleStates.Open, SectionLifecycleStates.Closed, SectionLifecycleStates.Archived
        },
        [SectionLifecycleStates.Open] = new(StringComparer.OrdinalIgnoreCase)
        {
            SectionLifecycleStates.Active, SectionLifecycleStates.Planning, SectionLifecycleStates.Locked,
            SectionLifecycleStates.Closed, SectionLifecycleStates.Merged, SectionLifecycleStates.Split
        },
        [SectionLifecycleStates.Active] = new(StringComparer.OrdinalIgnoreCase)
        {
            SectionLifecycleStates.Locked, SectionLifecycleStates.Open, SectionLifecycleStates.Closed,
            SectionLifecycleStates.Merged, SectionLifecycleStates.Split
        },
        [SectionLifecycleStates.Locked] = new(StringComparer.OrdinalIgnoreCase)
        {
            SectionLifecycleStates.Active, SectionLifecycleStates.Closed, SectionLifecycleStates.Merged, SectionLifecycleStates.Split
        },
        [SectionLifecycleStates.Merged] = new(StringComparer.OrdinalIgnoreCase)
        {
            SectionLifecycleStates.Archived, SectionLifecycleStates.Active // reverse merge restore
        },
        [SectionLifecycleStates.Split] = new(StringComparer.OrdinalIgnoreCase)
        {
            SectionLifecycleStates.Archived, SectionLifecycleStates.Active // reverse split restore
        },
        [SectionLifecycleStates.Closed] = new(StringComparer.OrdinalIgnoreCase)
        {
            SectionLifecycleStates.Archived, SectionLifecycleStates.Open, SectionLifecycleStates.Draft
        },
        [SectionLifecycleStates.Archived] = new(StringComparer.OrdinalIgnoreCase),
    };

    public static bool CanTransition(string? from, string? to)
    {
        var f = SectionLifecycleStates.Normalize(from);
        var t = SectionLifecycleStates.Normalize(to);
        if (string.Equals(f, t, StringComparison.OrdinalIgnoreCase)) return true;
        return Transitions.TryGetValue(f, out var allowed) && allowed.Contains(t);
    }

    public static IReadOnlyList<string> GetAllowedTargets(string? from)
    {
        var f = SectionLifecycleStates.Normalize(from);
        return Transitions.TryGetValue(f, out var allowed)
            ? allowed.OrderBy(x => x).ToList()
            : [];
    }

    public static void EnsureCanTransition(string? from, string? to)
    {
        var f = SectionLifecycleStates.Normalize(from);
        var t = SectionLifecycleStates.Normalize(to);
        if (CanTransition(f, t)) return;
        throw new InvalidOperationException($"Invalid section lifecycle transition: '{f}' → '{t}'.");
    }
}
