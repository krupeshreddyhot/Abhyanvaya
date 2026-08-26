namespace Abhyanvaya.Application.DTOs.Academic;

/// <summary>AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3F — Teaching Group Semester remediation.</summary>
public enum TeachingGroupSemesterRemediationStatus
{
    Ready = 1,
    AlreadyComplete = 2,
    ManualReviewRequired = 3,
    Blocked = 4,
}

public sealed class TeachingGroupSemesterRemediationSectionCheckDto
{
    public int TeachingGroupSectionId { get; init; }
    public int SectionId { get; init; }
    public int SectionCourseId { get; init; }
    public int SectionGroupId { get; init; }
    public int SectionSemesterId { get; init; }
    public bool IsCompatible { get; init; }
    public string Notes { get; init; } = "";
}

public sealed class TeachingGroupSemesterRemediationItemDto
{
    public int TeachingGroupId { get; init; }
    public string? Code { get; init; }
    public string Name { get; init; } = "";
    public int TenantId { get; init; }
    public int CourseId { get; init; }
    public int GroupId { get; init; }
    public int SubjectId { get; init; }
    public int SubjectAllocationId { get; init; }
    public int AcademicYearId { get; init; }
    public string Status { get; init; } = "";
    public int CurrentSemesterId { get; init; }
    public int? TargetSemesterId { get; init; }
    public int? TargetGroupId { get; init; }
    public int? TargetCourseId { get; init; }
    public int? TargetNumber { get; init; }
    public TeachingGroupSemesterRemediationStatus StatusKind { get; init; }
    public string StatusCode { get; init; } = "";
    public string Reason { get; init; } = "";
    public bool MutationAllowed { get; init; }
    public bool TeachingGroupSectionUnchanged { get; init; } = true;
    public bool MembershipUnchanged { get; init; } = true;
    public bool SubjectAllocationConsistent { get; init; }
    public bool TimetableEntryConsistent { get; init; }
    public bool ProjectionConsistent { get; init; }
    public int TeachingGroupSectionCount { get; init; }
    public int MembershipCount { get; init; }
    public int TimetableEntryCount { get; init; }
    public IReadOnlyList<TeachingGroupSemesterRemediationSectionCheckDto> SectionChecks { get; init; } = [];
    public IReadOnlyList<int> TimetableEntryIds { get; init; } = [];
}

public sealed class TeachingGroupSemesterRemediationResultDto
{
    public DateTime GeneratedUtc { get; init; }
    public int TenantId { get; init; }
    public bool IsReadOnly { get; init; }
    public string ExecutionStatus { get; init; } = "NotExecuted";
    public bool RolledBack { get; init; }
    public bool ExecutionSafe { get; init; }
    public int ChangedCount { get; init; }
    public int AlreadyCompleteCount { get; init; }
    public int BlockedCount { get; init; }
    public int ManualReviewCount { get; init; }
    public int DeferredCount { get; init; }
    public IReadOnlyList<int> ApprovedTeachingGroupIds { get; init; } = [];
    public IReadOnlyList<int> AffectedTeachingGroupIds { get; init; } = [];
    public IReadOnlyList<int> OldSemesterIds { get; init; } = [];
    public IReadOnlyList<int> NewSemesterIds { get; init; } = [];
    public IReadOnlyList<TeachingGroupSemesterRemediationItemDto> Items { get; init; } = [];
    public IReadOnlyList<string> Notes { get; init; } = [];
    public string? AbortReason { get; init; }
    public string? ConcurrencyResult { get; init; }
    public bool TransactionCommitted { get; init; }
    public SemesterPostMigrationIntegrityAuditDto? PostIntegrityAudit { get; init; }
    public LegacySemesterFinalizationAuditDto? PostFinalizationAudit { get; init; }
}
