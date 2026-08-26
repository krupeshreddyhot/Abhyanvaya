namespace Abhyanvaya.Application.DTOs.Academic;

/// <summary>AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3G — Section Semester remediation.</summary>
public enum SectionSemesterRemediationStatus
{
    Ready = 1,
    AlreadyComplete = 2,
    ManualReviewRequired = 3,
    Blocked = 4,
}

public sealed class SectionSemesterRemediationItemDto
{
    public int SectionId { get; init; }
    public string SectionCode { get; init; } = "";
    public string SectionName { get; init; } = "";
    public int TenantId { get; init; }
    public int CourseId { get; init; }
    public int GroupId { get; init; }
    public int AcademicYearId { get; init; }
    public int CurrentSemesterId { get; init; }
    public int? TargetSemesterId { get; init; }
    public string Status { get; init; } = "";
    public SectionSemesterRemediationStatus StatusKind { get; init; }
    public string StatusCode { get; init; } = "";
    public string Reason { get; init; } = "";
    public bool MutationAllowed { get; init; }
    public bool InApprovedSet { get; init; }
    public IReadOnlyList<int> ReferencingTeachingGroupIds { get; init; } = [];
    public int TeachingGroupSectionLinkCount { get; init; }
    public int CurrentStudentSectionCount { get; init; }
}

public sealed class SectionSemesterRemediationResultDto
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
    public int? TargetGroupId { get; init; }
    public int ChangedCount { get; init; }
    public int AlreadyCompleteCount { get; init; }
    public int BlockedCount { get; init; }
    public int ManualReviewCount { get; init; }
    public int EligibleCount { get; init; }
    public IReadOnlyList<int> ApprovedSectionIds { get; init; } = [];
    public IReadOnlyList<int> AffectedSectionIds { get; init; } = [];
    public IReadOnlyList<SectionSemesterRemediationItemDto> Items { get; init; } = [];
    public IReadOnlyList<string> Notes { get; init; } = [];
    public string? AbortReason { get; init; }
    public string? ConcurrencyResult { get; init; }
    public bool TransactionCommitted { get; init; }
    public bool TeachingGroupsUnchanged { get; init; } = true;
    public bool TeachingGroupSectionsUnchanged { get; init; } = true;
}
