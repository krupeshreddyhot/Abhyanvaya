namespace Abhyanvaya.Application.Academic.Allocation;

/// <summary>AI29.1D.24B.4A — Existing assignment handling for placement strategies.</summary>
public static class ExistingAssignmentPolicies
{
    /// <summary>
    /// Legacy / default when omitted from ConfigJson: preserve when current section is in targets
    /// and capacity remains; otherwise reconsider (may move outside-target into targets).
    /// </summary>
    public const string LegacyPreserveWhenCapacityAllows = "LegacyPreserveWhenCapacityAllows";

    /// <summary>
    /// Keep in-target assignments when capacity remains. Do not silently move students whose
    /// current section is outside targets or full — leave them without a new recommendation.
    /// </summary>
    public const string PreserveExisting = "PreserveExisting";

    /// <summary>Skip seeding; all eligible students are reconsidered by the placement strategy.</summary>
    public const string Reallocate = "Reallocate";

    public static IReadOnlyList<string> All { get; } =
    [
        LegacyPreserveWhenCapacityAllows,
        PreserveExisting,
        Reallocate,
    ];

    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return LegacyPreserveWhenCapacityAllows;
        var trimmed = raw.Trim();
        if (All.Any(p => string.Equals(p, trimmed, StringComparison.OrdinalIgnoreCase)))
            return All.First(p => string.Equals(p, trimmed, StringComparison.OrdinalIgnoreCase));
        return LegacyPreserveWhenCapacityAllows;
    }

    public static bool IsReallocate(string? policy)
        => string.Equals(Normalize(policy), Reallocate, StringComparison.OrdinalIgnoreCase);

    public static bool IsStrictPreserve(string? policy)
        => string.Equals(Normalize(policy), PreserveExisting, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Shared deterministic section ordering + existing-assignment seeding for placement.</summary>
public static class AllocationPlacementSupport
{
    /// <summary>
    /// Authoritative order: DisplayOrder, then SectionCode, then SectionId.
    /// Reuses Section.DisplayOrder projected into Allocation Context (AI29.1D.24B.4A Prompt 3).
    /// </summary>
    public static List<AllocationSectionProjection> OrderTargetSections(
        IEnumerable<AllocationSectionProjection> sections)
        => sections
            .Where(s => s.Lifecycle is not ("Merged" or "Split" or "Archived" or "Closed"))
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.SectionCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.SectionId)
            .ToList();

    public static Dictionary<int, int> BuildRemainingSeats(
        IReadOnlyList<AllocationSectionProjection> sections,
        IReadOnlyDictionary<int, AllocationCapacityProjection> caps)
        => sections.ToDictionary(
            s => s.SectionId,
            s =>
            {
                caps.TryGetValue(s.SectionId, out var c);
                var max = c?.MaximumCapacity ?? 0;
                var reserved = c?.ReservedSeats ?? 0;
                return max > 0 ? Math.Max(0, max - reserved) : 0;
            });

    /// <summary>
    /// Seeds Assignments from CurrentSectionId according to ExistingAssignmentPolicy.
    /// Returns student ids that must not be reconsidered (strict preserve skip).
    /// </summary>
    public static HashSet<int> SeedExistingAssignments(
        AllocationWorkingState state,
        Dictionary<int, int> remaining,
        Action<int, string> addExplanation)
    {
        var skipped = new HashSet<int>();
        var policy = ExistingAssignmentPolicies.Normalize(state.Config.ExistingAssignmentPolicy);

        if (ExistingAssignmentPolicies.IsReallocate(policy))
            return skipped;

        foreach (var st in state.Context.Students.OrderBy(s => s.StudentId))
        {
            if (st.CurrentSectionId is not int sid)
                continue;

            var inTargets = remaining.ContainsKey(sid);
            var hasSeat = inTargets && remaining[sid] > 0;

            if (hasSeat)
            {
                state.Assignments[st.StudentId] = sid;
                remaining[sid]--;
                addExplanation(
                    st.StudentId,
                    policy == ExistingAssignmentPolicies.LegacyPreserveWhenCapacityAllows
                        ? $"Kept in Section {st.CurrentSectionCode ?? "?"} because the current assignment still has capacity."
                        : AllocationBusinessExplanations.Preserved(st.CurrentSectionCode));
                continue;
            }

            if (policy == ExistingAssignmentPolicies.LegacyPreserveWhenCapacityAllows)
            {
                // Legacy: not seeded → placement loop may move the student.
                continue;
            }

            // PreserveExisting: do not silently move outside-target or full-section students.
            skipped.Add(st.StudentId);
            if (inTargets)
                addExplanation(st.StudentId, AllocationBusinessExplanations.PreservedButFull(st.CurrentSectionCode));
            else
                addExplanation(st.StudentId, AllocationBusinessExplanations.PreservedOutsideTarget(st.CurrentSectionCode));
        }

        return skipped;
    }

    public static void WarnIfBandExceedsCapacity(
        AllocationWorkingState state,
        int bandSize,
        IReadOnlyList<AllocationSectionProjection> sections,
        IReadOnlyDictionary<int, AllocationCapacityProjection> caps)
    {
        foreach (var s in sections)
        {
            caps.TryGetValue(s.SectionId, out var c);
            var hard = c is null ? 0 : Math.Max(0, c.MaximumCapacity - c.ReservedSeats);
            if (hard > 0 && bandSize > hard)
            {
                state.Warnings.Add(
                    $"Your allocation band contains more students than section {s.SectionCode} can hold. Some students may remain unallocated.");
            }
        }
    }
}

/// <summary>Administrator-facing explanation strings (server-authoritative).</summary>
public static class AllocationBusinessExplanations
{
    public static string Preserved(string? sectionCode)
        => $"Kept in Section {sectionCode ?? "?"} because Preserve Existing Assignments is selected.";

    public static string PreservedButFull(string? sectionCode)
        => $"Kept in Section {sectionCode ?? "?"} (section is at capacity). Not moved to another section.";

    public static string PreservedOutsideTarget(string? sectionCode)
        => $"Kept in Section {sectionCode ?? "?"} because that section is outside the selected targets. Not moved.";

    public static string Reassigned(string? fromCode, string toCode, string reason)
        => $"Reassigned from Section {fromCode ?? "Unassigned"} to Section {toCode}: {reason}";

    public static string NewAssignment(string toCode, string reason)
        => $"Assigned to Section {toCode} because {reason}";

    public static string RollBandReason(int bandIndex, string sectionCode, int bandSize)
        => $"this student is within allocation band {bandIndex + 1} for Section {sectionCode} (band size {bandSize})";

    public static string CapacityBalanceReason()
        => "capacity was available and occupancy balance improved";

    public static string UnallocatedCapacity()
        => "Not allocated because the selected Sections do not have sufficient capacity.";

    public static string UnallocatedBandOverflow(string sectionCode)
        => $"Not allocated because Section {sectionCode} does not have sufficient capacity for this allocation band.";

    public static string UnallocatedNoLast3()
        => "Not allocated because the last three digits of the student number are unavailable.";

    public static string UnallocatedBandExceedsSections(int bandIndex, int sectionCount)
        => $"Not allocated because allocation band {bandIndex + 1} exceeds the {sectionCount} selected Sections.";
}
