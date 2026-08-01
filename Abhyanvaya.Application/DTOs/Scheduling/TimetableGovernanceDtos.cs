using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.DTOs.Scheduling;

public sealed class ScheduleVersionDto
{
    public int Id { get; init; }
    public int AcademicYearId { get; init; }
    public string? AcademicYearName { get; init; }
    public int? AcademicTermId { get; init; }
    public string? AcademicTermName { get; init; }
    public int VersionNumber { get; init; }
    public string VersionName { get; init; } = null!;
    public ScheduleVersionStatus Status { get; init; }
    public bool IsCurrent { get; init; }
    public DateTime? PublishedDate { get; init; }
    public int? PublishedBy { get; init; }
    public DateTime? ArchivedDate { get; init; }
    public int? ArchivedBy { get; init; }
    public int? ArchiveReasonId { get; init; }
    public string? ArchiveReasonName { get; init; }
    public string? ArchiveComments { get; init; }
    public int? ReferenceVersionId { get; init; }
    public int? ParentVersionId { get; init; }
    public string? Remarks { get; init; }
    public int TimetableCount { get; init; }
}

public sealed class CreateScheduleVersionRequest
{
    public int AcademicYearId { get; init; }
    public int? AcademicTermId { get; init; }
    public string VersionName { get; init; } = null!;
    public string? Remarks { get; init; }
    public bool CreateEmptyTimetable { get; init; }
    public string? TimetableName { get; init; }
    public int? DepartmentId { get; init; }
    public int? TimeSlotSetId { get; init; }
}

public sealed class DuplicateScheduleVersionRequest
{
    public int SourceVersionId { get; init; }
    public string VersionName { get; init; } = null!;
    public string? Remarks { get; init; }
    public bool CloneAllTimetables { get; init; }
}

public sealed class ScheduleVersionHistoryDto
{
    public int VersionId { get; init; }
    public string VersionName { get; init; } = null!;
    public int VersionNumber { get; init; }
    public ScheduleVersionStatus Status { get; init; }
    public DateTime CreatedDate { get; init; }
    public int? CreatedBy { get; init; }
    public DateTime? PublishedDate { get; init; }
    public DateTime? ArchivedDate { get; init; }
}

public sealed class TimetableApprovalRequestDto
{
    public int Id { get; init; }
    public int ScheduleVersionId { get; init; }
    public string? VersionName { get; init; }
    public int TimetableId { get; init; }
    public string? TimetableName { get; init; }
    public TimetableApprovalRequestStatus Status { get; init; }
    public int SubmittedBy { get; init; }
    public DateTime SubmittedUtc { get; init; }
    public int CurrentStepOrder { get; init; }
    public IReadOnlyList<TimetableApprovalStepDto> Steps { get; init; } = [];
}

public sealed class TimetableApprovalStepDto
{
    public int Id { get; init; }
    public int StepOrder { get; init; }
    public string RoleKey { get; init; } = null!;
    public TimetableApprovalRequestStatus Status { get; init; }
    public int? AssignedTo { get; init; }
    public int? DecidedBy { get; init; }
    public DateTime? DecidedUtc { get; init; }
    public ApprovalDecision? Decision { get; init; }
    public string? Comments { get; init; }
}

public sealed class SubmitForReviewRequest
{
    public int TimetableId { get; init; }
    public string? Comments { get; init; }
}

public sealed class DecideApprovalStepRequest
{
    public int RequestId { get; init; }
    public int StepOrder { get; init; }
    public ApprovalDecision Decision { get; init; }
    public string? Comments { get; init; }
    public string? DecisionNotes { get; init; }
    public string? ReviewerRemarks { get; init; }
}

public sealed class TimetableApprovalTimelineDto
{
    public int RequestId { get; init; }
    public TimetableApprovalRequestStatus Status { get; init; }
    public IReadOnlyList<TimetableApprovalHistoryDto> Events { get; init; } = [];
    public IReadOnlyList<ApprovalCommentDto> Comments { get; init; } = [];
    public IReadOnlyList<DecisionHistoryDto> Decisions { get; init; } = [];
}

public sealed class TimetableApprovalHistoryDto
{
    public int StepOrder { get; init; }
    public int ActorUserId { get; init; }
    public ApprovalDecision? Decision { get; init; }
    public string? Comments { get; init; }
    public DateTime OccurredUtc { get; init; }
    public TimetableApprovalRequestStatus? OldStatus { get; init; }
    public TimetableApprovalRequestStatus? NewStatus { get; init; }
}

public sealed class TimetableCloneJobDto
{
    public int Id { get; init; }
    public TimetableCloneJobType JobType { get; init; }
    public int SourceTimetableId { get; init; }
    public int? TargetTimetableId { get; init; }
    public string? PayloadJson { get; init; }
    public TimetableCloneJobStatus Status { get; init; }
    public int ProgressPercent { get; init; }
    public string? Summary { get; init; }
    public string? Error { get; init; }
    public int RequestedBy { get; init; }
    public DateTime? StartedUtc { get; init; }
    public DateTime? CompletedUtc { get; init; }
}

public sealed class EnqueueTimetableCloneRequest
{
    public TimetableCloneJobType JobType { get; init; }
    public int SourceTimetableId { get; init; }
    public int? TargetTimetableId { get; init; }
    public byte? SourceDayOfWeek { get; init; }
    public byte? TargetDayOfWeek { get; init; }
    public int? TargetScheduleVersionId { get; init; }
    public string? TargetTimetableName { get; init; }
    public int? DepartmentId { get; init; }
    public int? CourseId { get; init; }
    public int? GroupId { get; init; }
    public int? StaffId { get; init; }
    public int? RoomId { get; init; }
    public bool ExecuteSynchronously { get; init; } = true;
}

public sealed class SoftWarningDto
{
    public string Code { get; init; } = null!;
    public string Severity { get; init; } = "Warning";
    public string Message { get; init; } = null!;
    public int? EntryId { get; init; }
    public int? StaffId { get; init; }
    public int? RoomId { get; init; }
    public byte? DayOfWeek { get; init; }
    public int? TimeSlotId { get; init; }
    public bool Dismissed { get; init; }
}

public sealed class DismissSoftWarningRequest
{
    public string Code { get; init; } = null!;
    public int? EntryId { get; init; }
    public int? StaffId { get; init; }
    public int? RoomId { get; init; }
    public byte? DayOfWeek { get; init; }
    public int? TimeSlotId { get; init; }
}

public sealed class TimetableChangeHistoryDto
{
    public int Id { get; init; }
    public int TimetableId { get; init; }
    public int? EntryId { get; init; }
    public int? UserId { get; init; }
    public DateTime OccurredUtc { get; init; }
    public TimetableChangeOperation Operation { get; init; }
    public string? OldValueJson { get; init; }
    public string? NewValueJson { get; init; }
    public string? Reason { get; init; }
}

public sealed class TimetableChangeHistoryFilter
{
    public int TimetableId { get; init; }
    public int? EntryId { get; init; }
    public TimetableChangeOperation? Operation { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
}

public sealed class TimetableGovernanceDashboardDto
{
    public int DraftVersionCount { get; init; }
    public int PublishedVersionCount { get; init; }
    public int ApprovalQueueCount { get; init; }
    public int PendingReviewsCount { get; init; }
    public int SoftWarningCount { get; init; }
    public int RecentlyPublishedCount { get; init; }
    public int ArchivedVersionCount { get; init; }
    public int RecentChangesCount { get; init; }
    public int FrozenTimetableCount { get; init; }
    public int ArchivedTimetableCount { get; init; }
    public IReadOnlyList<NamedCountDto> ApprovalTrend { get; init; } = [];
    public IReadOnlyList<NamedCountDto> VersionGrowth { get; init; } = [];
    public IReadOnlyList<NamedCountDto> PublishingHistory { get; init; } = [];
    public IReadOnlyList<NamedCountDto> ArchiveReasonDistribution { get; init; } = [];
    public IReadOnlyList<ArchiveLifecycleItemDto> LatestArchives { get; init; } = [];
}

public sealed class PublishTimetableRequest
{
    public string? Reason { get; init; }
}

public sealed class ArchiveTimetableRequest
{
    public string? Reason { get; init; }
    public int? ArchiveReasonId { get; init; }
    public string? Comments { get; init; }
    public int? ReferenceVersionId { get; init; }
}
