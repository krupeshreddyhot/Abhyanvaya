namespace Abhyanvaya.Application.DTOs.Academic;

/// <summary>AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3J — Subject Catalog Semester remediation.</summary>
public enum SubjectCatalogRemediationStatus
{
    AlreadyCorrect = 1,
    SafeToRemap = 2,
    ManualMappingRequired = 3,
    Blocked = 4,
    HistoricalRetain = 5,
    AlreadyComplete = 6,
}

public sealed class SubjectCatalogRemediationItemDto
{
    public int SubjectId { get; init; }
    public int TenantSubjectId { get; init; }
    public int TenantId { get; init; }
    public int CourseId { get; init; }
    public int GroupId { get; init; }
    public int CurrentSemesterId { get; init; }
    public int? CurrentSemesterNumber { get; init; }
    public bool CurrentSemesterIsNullGroup { get; init; }
    public int? TargetSemesterId { get; init; }
    public int? TargetSemesterNumber { get; init; }
    public IReadOnlyList<int> CandidateTargetSemesterIds { get; init; } = [];
    public SubjectCatalogRemediationStatus StatusKind { get; init; }
    public string StatusCode { get; init; } = "";
    public string Reason { get; init; } = "";
    public bool MutationAllowed { get; init; }
    public IReadOnlyList<int> ReferencingTeachingGroupIds { get; init; } = [];
    public int SubjectAllocationCount { get; init; }
}

public sealed class SubjectCatalogRemediationResultDto
{
    public DateTime GeneratedUtc { get; init; }
    public int TenantId { get; init; }
    public bool IsReadOnly { get; init; }
    public string ExecutionStatus { get; init; } = "NotExecuted";
    public bool RolledBack { get; init; }
    public bool ExecutionSafe { get; init; }
    public int ChangedCount { get; init; }
    public int AlreadyCompleteCount { get; init; }
    public int SafeToRemapCount { get; init; }
    public int ManualMappingCount { get; init; }
    public int BlockedCount { get; init; }
    public int HistoricalRetainCount { get; init; }
    public int AlreadyCorrectCount { get; init; }
    public IReadOnlyList<int> AffectedSubjectIds { get; init; } = [];
    public IReadOnlyList<SubjectCatalogRemediationItemDto> Items { get; init; } = [];
    public IReadOnlyList<string> Notes { get; init; } = [];
    public string? AbortReason { get; init; }
    public string? ConcurrencyResult { get; init; }
    public bool TransactionCommitted { get; init; }
    public bool TeachingGroupsUnchanged { get; init; } = true;
    public bool SubjectAllocationsUnchanged { get; init; } = true;
    public bool TimetableSectionsUnchanged { get; init; } = true;
    public string CorrelationId { get; init; } = "";
}
