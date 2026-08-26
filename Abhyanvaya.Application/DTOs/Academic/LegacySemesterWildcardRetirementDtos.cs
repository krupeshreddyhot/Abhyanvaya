namespace Abhyanvaya.Application.DTOs.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3L (package 3I1) —
/// Legacy Semester disposition + operational wildcard retirement.
/// PromptCode P1-4-3L avoids colliding with Finance Section remediation (P1-4-3I).
/// </summary>
public static class LegacySemesterWildcardRetirementCodes
{
    public const string PromptCode = "P1-4-3L";
    public const string JournalDispositionCode = "OPERATIONAL_WILDCARD_RETIRED";
    public const string RetainHistorical = "RETAIN_HISTORICAL";
    public const string ManualMappingRequired = "MANUAL_MAPPING_REQUIRED";
    public const string DuplicateReview = "DUPLICATE_REVIEW";
    public const string BlockedByDependency = "BLOCKED_BY_DEPENDENCY";
    public const string ReadyForRetirement = "READY_FOR_RETIREMENT";
}

public sealed class LegacySemesterWildcardRetirementPreviewDto
{
    public DateTime GeneratedUtc { get; init; }
    public int TenantId { get; init; }
    public bool IsReadOnly { get; init; } = true;
    public bool NoMutationsPerformed { get; init; } = true;
    public string PromptCode { get; init; } = LegacySemesterWildcardRetirementCodes.PromptCode;
    public bool ExecutionSafe { get; init; }
    public bool OperationalWildcardRetiredInCode { get; init; }
    public int LegacySemesterCount { get; init; }
    public int RetainedCount { get; init; }
    public int ManualCount { get; init; }
    public int BlockedCount { get; init; }
    public int DuplicateReviewCount { get; init; }
    public int ReadyForRetirementCount { get; init; }
    public int ActiveOperationalDependencyCount { get; init; }
    public IReadOnlyList<LegacySemesterWildcardRetirementItemDto> Items { get; init; } = [];
    public IReadOnlyList<Prompt3HWildcardDependencyStatusDto> WildcardSites { get; init; } = [];
    public IReadOnlyList<string> BlockingReasons { get; init; } = [];
    public IReadOnlyList<string> Notes { get; init; } = [];
    public bool CanMakeGroupIdNotNull { get; init; }
    public bool CanAddGroupSemesterUniqueConstraint { get; init; }
    public bool CanRemoveLegacyWildcardSemantics { get; init; }
}

public sealed class LegacySemesterWildcardRetirementItemDto
{
    public int SemesterId { get; init; }
    public int CourseId { get; init; }
    public string CourseName { get; init; } = "";
    public int Number { get; init; }
    public string Name { get; init; } = "";
    public int? GroupId { get; init; }
    public string DispositionCode { get; init; } = "";
    public string Reason { get; init; } = "";
    public bool CanExecute { get; init; }
    public int DependencyCount { get; init; }
    public IReadOnlyList<string> DependencyTypes { get; init; } = [];
    public int StudentRefs { get; init; }
    public int AttendanceRefs { get; init; }
    public int SectionRefs { get; init; }
    public int SubjectRefs { get; init; }
    public int SubjectAllocationRefs { get; init; }
    public int TimetableEntryRefs { get; init; }
    public int TeachingGroupRefs { get; init; }
    public int StudentSectionRefs { get; init; }
    public int TimetableSectionRefs { get; init; }
}

public sealed class LegacySemesterWildcardRetirementResultDto
{
    public DateTime GeneratedUtc { get; init; }
    public int TenantId { get; init; }
    public bool IsReadOnly { get; init; }
    public string PromptCode { get; init; } = LegacySemesterWildcardRetirementCodes.PromptCode;
    public string ExecutionStatus { get; init; } = "NotExecuted";
    public bool RolledBack { get; init; }
    public bool TransactionCommitted { get; init; }
    public int ChangedCount { get; init; }
    public int AlreadyCompleteCount { get; init; }
    public int RetainedCount { get; init; }
    public int BlockedCount { get; init; }
    public int ManualCount { get; init; }
    public int DuplicateReviewCount { get; init; }
    public IReadOnlyList<int> AffectedSemesterIds { get; init; } = [];
    public IReadOnlyList<LegacySemesterWildcardRetirementItemDto> Items { get; init; } = [];
    public IReadOnlyList<string> Notes { get; init; } = [];
    public string? AbortReason { get; init; }
    public bool CanMakeGroupIdNotNull { get; init; }
    public bool CanAddGroupSemesterUniqueConstraint { get; init; }
    public bool CanRemoveLegacyWildcardSemantics { get; init; }
    public Prompt3HPostSectionIntegrityAuditDto? PostIntegrityAudit { get; init; }
}
