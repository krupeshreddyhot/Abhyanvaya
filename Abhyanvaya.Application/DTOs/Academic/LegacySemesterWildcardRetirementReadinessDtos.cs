namespace Abhyanvaya.Application.DTOs.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3I (package 3I3 / PromptCode P1-4-3I3) —
/// Read-only legacy wildcard retirement readiness contract.
/// Does not collide with Finance Section remediation PromptCode P1-4-3I.
/// </summary>
public sealed class LegacySemesterWildcardRetirementReadinessDto
{
    public DateTime GeneratedUtc { get; init; }
    public int TenantId { get; init; }
    public bool IsReadOnly { get; init; } = true;
    public bool NoMutationsPerformed { get; init; } = true;
    public bool SaveChangesInvoked { get; init; }
    public string PromptCode { get; init; } = "P1-4-3I3";

    public int LegacyNullGroupCount { get; init; }
    public int ActiveLegacyWildcardCount { get; init; }
    public int HistoricalOnlyCount { get; init; }
    public int ManualMappingRequiredCount { get; init; }
    public int DuplicateReviewCount { get; init; }
    public int DownstreamReferenceCount { get; init; }
    public int WildcardQueryDependencyCount { get; init; }

    public bool TenantIsolationPassed { get; init; }
    public bool OperationalSemesterResolutionPassed { get; init; }
    public bool HistoricalRetentionPassed { get; init; }
    public bool NewNullGroupWritePathBlocked { get; init; }
    public bool WildcardRetirementReady { get; init; }

    public IReadOnlyList<string> Blockers { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> Notes { get; init; } = [];

    public IReadOnlyList<LegacySemesterWildcardRetirementItemDto> DispositionMatrix { get; init; } = [];
    public IReadOnlyList<Prompt3HWildcardDependencyStatusDto> WildcardDependencyInventory { get; init; } = [];

    /// <summary>Phase 5 — Semester 1 historical Subject investigation (no auto-map).</summary>
    public LegacySemesterManualMappingPreviewDto? Semester1ManualMappingPreview { get; init; }

    /// <summary>Phase 6 — Semesters 4/5 duplicate review (no merge/delete).</summary>
    public IReadOnlyList<LegacySemesterDuplicateReviewPreviewDto> DuplicateReviewPreviews { get; init; } = [];

    public bool CanMakeGroupIdNotNull { get; init; }
    public bool CanAddGroupSemesterUniqueConstraint { get; init; }
    public string RecommendedNextPrompt { get; init; } = "";
}

public sealed class LegacySemesterManualMappingPreviewDto
{
    public int SemesterId { get; init; }
    public int CourseId { get; init; }
    public string CourseName { get; init; } = "";
    public int Number { get; init; }
    public int? GroupId { get; init; }
    public int SubjectReferenceCount { get; init; }
    public IReadOnlyList<LegacySemesterSubjectRefSampleDto> SubjectReferences { get; init; } = [];
    public IReadOnlyList<LegacySemesterWildcardCandidateGroupDto> CandidateGroups { get; init; } = [];
    public bool DeterministicMappingProven { get; init; }
    public string DispositionCode { get; init; } = "MANUAL_MAPPING_REQUIRED";
    public string ReasonMappingNotSafe { get; init; } = "";
}

public sealed class LegacySemesterSubjectRefSampleDto
{
    public int SubjectId { get; init; }
    public int? GroupId { get; init; }
    public int CourseId { get; init; }
    public string Name { get; init; } = "";
}

public sealed class LegacySemesterWildcardCandidateGroupDto
{
    public int GroupId { get; init; }
    public string GroupName { get; init; } = "";
    public int CourseId { get; init; }
    public bool DeterministicallyDerived { get; init; }
    public string Evidence { get; init; } = "";
}

public sealed class LegacySemesterDuplicateReviewPreviewDto
{
    public int SemesterId { get; init; }
    public int Number { get; init; }
    public int CourseId { get; init; }
    public string CourseName { get; init; } = "";
    public int? GroupId { get; init; }
    public int StudentRefs { get; init; }
    public int AttendanceRefs { get; init; }
    public int SubjectRefs { get; init; }
    public int SectionRefs { get; init; }
    public int SubjectAllocationRefs { get; init; }
    public int TimetableEntryRefs { get; init; }
    public int TeachingGroupRefs { get; init; }
    public bool SafeToRetainHistorically { get; init; }
    public bool DeterministicMappingProven { get; init; }
    public string DispositionCode { get; init; } = "DUPLICATE_REVIEW";
    public string Evidence { get; init; } = "";
}
