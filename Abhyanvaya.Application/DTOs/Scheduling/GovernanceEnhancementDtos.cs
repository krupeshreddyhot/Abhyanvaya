using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.DTOs.Scheduling;

public sealed class ArchiveReasonDto
{
    public int Id { get; init; }
    public ArchiveReasonCode Code { get; init; }
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
    public int SortOrder { get; init; }
}

public sealed class FreezeTimetableRequest
{
    public string Reason { get; init; } = null!;
}

public sealed class UnlockFrozenTimetableRequest
{
    public string Reason { get; init; } = null!;
}

public sealed class ArchiveTimetableGovernanceRequest
{
    public int ArchiveReasonId { get; init; }
    public string? Comments { get; init; }
    public int? ReferenceVersionId { get; init; }
}

public sealed class ArchiveScheduleVersionRequest
{
    public int ArchiveReasonId { get; init; }
    public string? Comments { get; init; }
    public int? ReferenceVersionId { get; init; }
}

public sealed class AddApprovalCommentRequest
{
    public int RequestId { get; init; }
    public string Comment { get; init; } = null!;
    public bool IsDecisionNote { get; init; }
}

public sealed class ApprovalCommentDto
{
    public int Id { get; init; }
    public int RequestId { get; init; }
    public int ActorUserId { get; init; }
    public string Comment { get; init; } = null!;
    public DateTime OccurredUtc { get; init; }
    public bool IsDecisionNote { get; init; }
}

public sealed class DecisionHistoryDto
{
    public int Id { get; init; }
    public int RequestId { get; init; }
    public int StepOrder { get; init; }
    public int ActorUserId { get; init; }
    public ApprovalDecision? Decision { get; init; }
    public string Action { get; init; } = null!;
    public string? Comment { get; init; }
    public string? DecisionNotes { get; init; }
    public string? ReviewerRemarks { get; init; }
    public TimetableApprovalRequestStatus? OldStatus { get; init; }
    public TimetableApprovalRequestStatus? NewStatus { get; init; }
    public DateTime OccurredUtc { get; init; }
}

public sealed class ArchiveLifecycleItemDto
{
    public int TimetableId { get; init; }
    public string TimetableName { get; init; } = null!;
    public string? ArchiveReasonName { get; init; }
    public ArchiveReasonCode? ArchiveReasonCode { get; init; }
    public string? Comments { get; init; }
    public int? ArchivedBy { get; init; }
    public DateTime? ArchivedDate { get; init; }
    public int? ReferenceVersionId { get; init; }
    public string? ReferenceVersionName { get; init; }
}
