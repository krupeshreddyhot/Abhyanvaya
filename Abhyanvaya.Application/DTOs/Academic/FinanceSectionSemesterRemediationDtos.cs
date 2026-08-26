namespace Abhyanvaya.Application.DTOs.Academic;

/// <summary>AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3I — Finance Section Semester remediation.</summary>
public enum FinanceSectionRemediationStatus
{
    SafeToRemediate = 1,
    AlreadyComplete = 2,
    Blocked = 3,
    ManualReview = 4,
    NotInScope = 5,
}

public sealed class FinanceSectionRemediationItemDto
{
    public int SectionId { get; init; }
    public string SectionCode { get; init; } = "";
    public string SectionName { get; init; } = "";
    public int TenantId { get; init; }
    public int CourseId { get; init; }
    public int GroupId { get; init; }
    public int AcademicYearId { get; init; }
    public int CurrentSemesterId { get; init; }
    public string CurrentSemesterClassification { get; init; } = "";
    public int? TargetFinanceGroupId { get; init; }
    public int? TargetSemesterId { get; init; }
    public int? TargetSemesterNumber { get; init; }
    public string Status { get; init; } = "";
    public FinanceSectionRemediationStatus StatusKind { get; init; }
    public string StatusCode { get; init; } = "";
    public string Reason { get; init; } = "";
    public bool MutationAllowed { get; init; }
    public bool InApprovedSet { get; init; }
    public IReadOnlyList<int> ReferencingTeachingGroupIds { get; init; } = [];
    public int TeachingGroupSectionLinkCount { get; init; }
    public int CurrentStudentSectionCount { get; init; }
}

public sealed class FinanceSectionRemediationResultDto
{
    public DateTime GeneratedUtc { get; init; }
    public int TenantId { get; init; }
    public bool IsReadOnly { get; init; }
    public string ExecutionStatus { get; init; } = "NotExecuted";
    public bool RolledBack { get; init; }
    public bool ExecutionSafe { get; init; }
    public int LegacySemesterId { get; init; }
    public int TargetSemesterId { get; init; }
    public int? TargetCourseId { get; init; }
    public int? TargetFinanceGroupId { get; init; }
    public int ChangedCount { get; init; }
    public int AlreadyCompleteCount { get; init; }
    public int BlockedCount { get; init; }
    public int ManualReviewCount { get; init; }
    public int NotInScopeCount { get; init; }
    public int EligibleCount { get; init; }
    public IReadOnlyList<int> ApprovedSectionIds { get; init; } = [];
    public IReadOnlyList<int> AffectedSectionIds { get; init; } = [];
    public IReadOnlyList<FinanceSectionRemediationItemDto> Items { get; init; } = [];
    public IReadOnlyList<string> Notes { get; init; } = [];
    public string? AbortReason { get; init; }
    public string? ConcurrencyResult { get; init; }
    public bool TransactionCommitted { get; init; }
    public bool TeachingGroupsUnchanged { get; init; } = true;
    public bool TeachingGroupSectionsUnchanged { get; init; } = true;
    public bool StudentsUnchanged { get; init; } = true;
    public bool TimetableSectionsUnchanged { get; init; } = true;
}
