namespace Abhyanvaya.Application.DTOs.Scheduling;

/// <summary>
/// AI-SCHED-TG.4A Prompt 7 — Explicit disposable pre-production TimetableEntry → TeachingGroup conversion.
/// Not a permanent production backfill.
/// </summary>
public sealed class ConvertLegacyTimetableEntriesRequest
{
    /// <summary>When true, validate and report only — no persistence.</summary>
    public bool DryRun { get; init; }

    /// <summary>Explicit per-entry mappings. Empty list is a no-op report.</summary>
    public IReadOnlyList<LegacyTimetableEntryConversionItem> Items { get; init; } = [];
}

public sealed class LegacyTimetableEntryConversionItem
{
    public int TimetableEntryId { get; init; }

    /// <summary>Required explicit TeachingGroup. Never inferred from SubjectAllocation.</summary>
    public int TeachingGroupId { get; init; }

    /// <summary>
    /// Explicit Section ids for TeachingGroupSection (via application boundary).
    /// Empty is allowed only when TeachingGroup type rules permit.
    /// </summary>
    public IReadOnlyList<int> SectionIds { get; init; } = [];
}

public sealed class LegacyTimetableConversionReportDto
{
    public bool DryRun { get; init; }
    public int ConvertedCount { get; init; }
    public int SkippedCount { get; init; }
    public int RejectedCount { get; init; }
    public IReadOnlyList<LegacyTimetableConversionItemResultDto> Results { get; init; } = [];
}

public sealed class LegacyTimetableConversionItemResultDto
{
    public int TimetableEntryId { get; init; }
    public int? TeachingGroupId { get; init; }
    /// <summary>Converted | Skipped | Rejected</summary>
    public string Outcome { get; init; } = null!;
    public string? Reason { get; init; }
}

public sealed class LegacyTimetableEntryWithoutTeachingGroupDto
{
    public int TimetableEntryId { get; init; }
    public int TimetableId { get; init; }
    public string TimetableStatus { get; init; } = null!;
    public bool TimetableIsFrozen { get; init; }
    public int SubjectAllocationId { get; init; }
    public int CourseId { get; init; }
    public int GroupId { get; init; }
    public int SemesterId { get; init; }
    public int SubjectId { get; init; }
}
