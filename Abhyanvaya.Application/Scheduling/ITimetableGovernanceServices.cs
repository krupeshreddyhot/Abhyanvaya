using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling;

public interface IScheduleVersionService
{
    Task<IReadOnlyList<ScheduleVersionDto>> ListAsync(int? academicYearId, int? academicTermId, ScheduleVersionStatus? status, bool includeArchived, CancellationToken cancellationToken = default);
    Task<ScheduleVersionDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScheduleVersionHistoryDto>> HistoryAsync(int academicYearId, int? academicTermId, CancellationToken cancellationToken = default);
    Task<ScheduleVersionDto> CreateAsync(CreateScheduleVersionRequest request, CancellationToken cancellationToken = default);
    Task<ScheduleVersionDto> DuplicateAsync(DuplicateScheduleVersionRequest request, CancellationToken cancellationToken = default);
    Task<ScheduleVersionDto> ClonePreviousVersionAsync(int academicYearId, int? academicTermId, string versionName, CancellationToken cancellationToken = default);
    Task<ScheduleVersionDto> MarkCurrentAsync(int id, CancellationToken cancellationToken = default);
    Task<ScheduleVersionDto> ArchiveAsync(int id, ArchiveScheduleVersionRequest? request = null, CancellationToken cancellationToken = default);
}

public interface ITimetableApprovalService
{
    Task<TimetableApprovalRequestDto> SubmitForReviewAsync(SubmitForReviewRequest request, CancellationToken cancellationToken = default);
    Task<TimetableApprovalRequestDto> DecideStepAsync(DecideApprovalStepRequest request, CancellationToken cancellationToken = default);
    Task<ApprovalCommentDto> AddCommentAsync(AddApprovalCommentRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimetableApprovalRequestDto>> ListQueueAsync(TimetableApprovalRequestStatus? status, CancellationToken cancellationToken = default);
    Task<TimetableApprovalTimelineDto?> GetTimelineAsync(int requestId, CancellationToken cancellationToken = default);
}

public interface ITimetableLifecycleService
{
    Task<TimetableDto> PublishAsync(int timetableId, PublishTimetableRequest? request, CancellationToken cancellationToken = default);
    Task<TimetableDto> ArchiveAsync(int timetableId, ArchiveTimetableRequest? request, CancellationToken cancellationToken = default);
    Task<TimetableDto> FreezeAsync(int timetableId, FreezeTimetableRequest request, CancellationToken cancellationToken = default);
    Task<TimetableDto> UnlockFrozenAsync(int timetableId, UnlockFrozenTimetableRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArchiveReasonDto>> ListArchiveReasonsAsync(CancellationToken cancellationToken = default);
}

public interface ITimetableCloneService
{
    Task<TimetableCloneJobDto> EnqueueAsync(EnqueueTimetableCloneRequest request, CancellationToken cancellationToken = default);
    Task<TimetableCloneJobDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimetableCloneJobDto>> ListAsync(TimetableCloneJobStatus? status, CancellationToken cancellationToken = default);
    Task ExecuteJobAsync(int jobId, CancellationToken cancellationToken = default);
}

public interface ITimetableSoftValidationService
{
    Task<IReadOnlyList<SoftWarningDto>> ValidateAsync(int timetableId, CancellationToken cancellationToken = default);
    Task DismissWarningAsync(int timetableId, DismissSoftWarningRequest request, CancellationToken cancellationToken = default);
}

public interface ITimetableChangeHistoryService
{
    Task RecordAsync(int timetableId, TimetableChangeOperation operation, int? entryId, object? oldValue, object? newValue, string? reason, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimetableChangeHistoryDto>> ListAsync(TimetableChangeHistoryFilter filter, CancellationToken cancellationToken = default);
    Task<byte[]> ExportExcelAsync(TimetableChangeHistoryFilter filter, CancellationToken cancellationToken = default);
}

public interface ITimetableGovernanceDashboardService
{
    Task<TimetableGovernanceDashboardDto> GetDashboardAsync(int? academicYearId, CancellationToken cancellationToken = default);
}
