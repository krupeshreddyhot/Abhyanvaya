namespace Abhyanvaya.Application.DTOs.Academic;

/// <summary>AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3C — downstream legacy Semester remediation.</summary>
public enum DownstreamRemediationStatus
{
    Ready = 1,
    AlreadyRemediated = 2,
    ManualReviewRequired = 3,
    DeferredByArchitectureBoundary = 4,
}

public sealed class DownstreamRemediationItemDto
{
    public string EntityType { get; init; } = null!;
    public string RecordId { get; init; } = null!;
    public int OldSemesterId { get; init; }
    public int OldSemesterNumber { get; init; }
    public int? GroupId { get; init; }
    public int? CourseId { get; init; }
    public int? ProposedSemesterId { get; init; }
    public int? TargetSemesterNumber { get; init; }
    public DownstreamRemediationStatus Status { get; init; }
    public string StatusCode { get; init; } = null!;
    public string Reason { get; init; } = null!;
    public bool MutationAllowed { get; init; }
}

public sealed class DownstreamRemediationSummaryDto
{
    public int Audited { get; init; }
    public int Ready { get; init; }
    public int AlreadyRemediated { get; init; }
    public int ManualReviewRequired { get; init; }
    public int DeferredByArchitectureBoundary { get; init; }
    public int Remediated { get; init; }
}

public sealed class DownstreamRemediationReportDto
{
    public DateTime GeneratedUtc { get; init; }
    public int TenantId { get; init; }
    public bool IsReadOnly { get; init; }
    public int? LegacySemesterId { get; init; }
    public int LegacySemesterNumber { get; init; }
    public DownstreamRemediationSummaryDto Summary { get; init; } = new();
    public IReadOnlyList<DownstreamRemediationItemDto> Items { get; init; } = [];
    public IReadOnlyList<string> Notes { get; init; } = [];
    public string? AbortReason { get; init; }
    public bool RolledBack { get; init; }
    public string ExecutionStatus { get; init; } = "NotExecuted"; // NotExecuted | Completed | Aborted | AlreadyComplete
    public SemesterPostMigrationIntegrityAuditDto? PostIntegrityAudit { get; init; }
}
