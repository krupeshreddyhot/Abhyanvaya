using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Common.Interfaces.Scheduling;

public interface IScheduleVersionRepository
{
    Task<IReadOnlyList<ScheduleVersion>> ListAsync(int tenantId, int? academicYearId, int? academicTermId, ScheduleVersionStatus? status, bool includeArchived, CancellationToken cancellationToken = default);
    Task<ScheduleVersion?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task<ScheduleVersion?> GetCurrentAsync(int tenantId, int academicYearId, int? academicTermId, CancellationToken cancellationToken = default);
    Task<int> GetNextVersionNumberAsync(int tenantId, int academicYearId, int? academicTermId, CancellationToken cancellationToken = default);
    Task AddAsync(ScheduleVersion entity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScheduleVersion>> ListHistoryAsync(int tenantId, int academicYearId, int? academicTermId, CancellationToken cancellationToken = default);
    Task UnsetCurrentForScopeAsync(int tenantId, int academicYearId, int? academicTermId, int? excludeId, CancellationToken cancellationToken = default);
}

public interface ITimetableApprovalRepository
{
    Task<TimetableApprovalRequest?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task<TimetableApprovalRequest?> GetPendingByTimetableAsync(int tenantId, int timetableId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimetableApprovalRequest>> ListQueueAsync(int tenantId, TimetableApprovalRequestStatus? status, CancellationToken cancellationToken = default);
    Task AddAsync(TimetableApprovalRequest entity, CancellationToken cancellationToken = default);
    Task AddStepAsync(TimetableApprovalStep entity, CancellationToken cancellationToken = default);
    Task AddHistoryAsync(TimetableApprovalHistory entity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimetableApprovalHistory>> ListHistoryAsync(int tenantId, int requestId, CancellationToken cancellationToken = default);
    Task<int> CountPendingAsync(int tenantId, CancellationToken cancellationToken = default);
}

public interface ITimetableCloneJobRepository
{
    Task<TimetableCloneJob?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimetableCloneJob>> ListAsync(int tenantId, TimetableCloneJobStatus? status, CancellationToken cancellationToken = default);
    Task AddAsync(TimetableCloneJob entity, CancellationToken cancellationToken = default);
    Task<TimetableCloneJob?> DequeueNextAsync(int tenantId, CancellationToken cancellationToken = default);
}

public interface ITimetableChangeHistoryRepository
{
    Task AddAsync(TimetableChangeHistory entity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimetableChangeHistory>> ListAsync(int tenantId, TimetableChangeHistoryFilter filter, CancellationToken cancellationToken = default);
    Task<int> CountRecentAsync(int tenantId, DateTime sinceUtc, CancellationToken cancellationToken = default);
}

public interface ITimetableWarningDismissalRepository
{
    Task<IReadOnlyList<TimetableWarningDismissal>> ListForTimetableAsync(int tenantId, int timetableId, CancellationToken cancellationToken = default);
    Task AddAsync(TimetableWarningDismissal entity, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int tenantId, int timetableId, string code, int? entryId, int? staffId, int? roomId, byte? dayOfWeek, int? timeSlotId, CancellationToken cancellationToken = default);
}

public interface IVersionComparisonRepository
{
    Task<IReadOnlyList<TimetableEntry>> ListEntriesForVersionAsync(int tenantId, int scheduleVersionId, int? departmentId, CancellationToken cancellationToken = default);
}

public interface ITimetableApprovalCommentRepository
{
    Task AddAsync(TimetableApprovalComment entity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimetableApprovalComment>> ListByRequestAsync(int tenantId, int requestId, CancellationToken cancellationToken = default);
}

public interface ITimetableDecisionHistoryRepository
{
    Task AddAsync(TimetableDecisionHistory entity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimetableDecisionHistory>> ListByRequestAsync(int tenantId, int requestId, CancellationToken cancellationToken = default);
}

public interface IArchiveReasonRepository
{
    Task<IReadOnlyList<ArchiveReasonLookup>> ListActiveAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<ArchiveReasonLookup?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);
}
