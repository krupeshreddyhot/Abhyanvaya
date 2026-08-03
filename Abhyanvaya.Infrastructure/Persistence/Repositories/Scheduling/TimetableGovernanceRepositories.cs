using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Infrastructure.Persistence.Repositories.Scheduling;

public sealed class ScheduleVersionRepository : IScheduleVersionRepository
{
    private readonly ApplicationDbContext _context;

    public ScheduleVersionRepository(ApplicationDbContext context) => _context = context;

    public Task<IReadOnlyList<ScheduleVersion>> ListAsync(int tenantId, int? academicYearId, int? academicTermId, ScheduleVersionStatus? status, bool includeArchived, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<ScheduleVersion>().AsNoTracking().Where(x => x.TenantId == tenantId);
        if (academicYearId.HasValue) query = query.Where(x => x.AcademicYearId == academicYearId.Value);
        if (academicTermId.HasValue) query = query.Where(x => x.AcademicTermId == academicTermId.Value);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (!includeArchived) query = query.Where(x => x.Status != ScheduleVersionStatus.Archived);
        return query.OrderByDescending(x => x.VersionNumber).ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<ScheduleVersion>)t.Result, cancellationToken);
    }

    public Task<ScheduleVersion?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<ScheduleVersion>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public Task<ScheduleVersion?> GetCurrentAsync(int tenantId, int academicYearId, int? academicTermId, CancellationToken cancellationToken = default) =>
        _context.Set<ScheduleVersion>().FirstOrDefaultAsync(x =>
            x.TenantId == tenantId
            && x.AcademicYearId == academicYearId
            && x.AcademicTermId == academicTermId
            && x.IsCurrent, cancellationToken);

    public async Task<int> GetNextVersionNumberAsync(int tenantId, int academicYearId, int? academicTermId, CancellationToken cancellationToken = default)
    {
        var max = await _context.Set<ScheduleVersion>()
            .Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId && x.AcademicTermId == academicTermId)
            .Select(x => (int?)x.VersionNumber)
            .MaxAsync(cancellationToken);
        return (max ?? 0) + 1;
    }

    public async Task AddAsync(ScheduleVersion entity, CancellationToken cancellationToken = default) =>
        await _context.Set<ScheduleVersion>().AddAsync(entity, cancellationToken);

    public Task<IReadOnlyList<ScheduleVersion>> ListHistoryAsync(int tenantId, int academicYearId, int? academicTermId, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<ScheduleVersion>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId);
        if (academicTermId.HasValue) query = query.Where(x => x.AcademicTermId == academicTermId.Value);
        return query.OrderBy(x => x.VersionNumber).ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<ScheduleVersion>)t.Result, cancellationToken);
    }

    public async Task UnsetCurrentForScopeAsync(int tenantId, int academicYearId, int? academicTermId, int? excludeId, CancellationToken cancellationToken = default)
    {
        var items = await _context.Set<ScheduleVersion>()
            .Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId && x.AcademicTermId == academicTermId && x.IsCurrent)
            .Where(x => !excludeId.HasValue || x.Id != excludeId.Value)
            .ToListAsync(cancellationToken);
        foreach (var item in items)
            item.IsCurrent = false;
    }
}

public sealed class TimetableApprovalRepository : ITimetableApprovalRepository
{
    private readonly ApplicationDbContext _context;

    public TimetableApprovalRepository(ApplicationDbContext context) => _context = context;

    public Task<TimetableApprovalRequest?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<TimetableApprovalRequest>()
            .Include(x => x.Steps.OrderBy(s => s.StepOrder))
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public Task<TimetableApprovalRequest?> GetPendingByTimetableAsync(int tenantId, int timetableId, CancellationToken cancellationToken = default) =>
        _context.Set<TimetableApprovalRequest>()
            .Where(x => x.TenantId == tenantId && x.TimetableId == timetableId)
            .Where(x => x.Status == TimetableApprovalRequestStatus.Pending || x.Status == TimetableApprovalRequestStatus.InReview)
            .OrderByDescending(x => x.SubmittedUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<IReadOnlyList<TimetableApprovalRequest>> ListQueueAsync(int tenantId, TimetableApprovalRequestStatus? status, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<TimetableApprovalRequest>().AsNoTracking().Where(x => x.TenantId == tenantId);
        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);
        else
            query = query.Where(x => x.Status == TimetableApprovalRequestStatus.Pending || x.Status == TimetableApprovalRequestStatus.InReview);
        return query.OrderBy(x => x.SubmittedUtc).ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<TimetableApprovalRequest>)t.Result, cancellationToken);
    }

    public async Task AddAsync(TimetableApprovalRequest entity, CancellationToken cancellationToken = default) =>
        await _context.Set<TimetableApprovalRequest>().AddAsync(entity, cancellationToken);

    public async Task AddStepAsync(TimetableApprovalStep entity, CancellationToken cancellationToken = default) =>
        await _context.Set<TimetableApprovalStep>().AddAsync(entity, cancellationToken);

    public async Task AddHistoryAsync(TimetableApprovalHistory entity, CancellationToken cancellationToken = default) =>
        await _context.Set<TimetableApprovalHistory>().AddAsync(entity, cancellationToken);

    public Task<IReadOnlyList<TimetableApprovalHistory>> ListHistoryAsync(int tenantId, int requestId, CancellationToken cancellationToken = default) =>
        _context.Set<TimetableApprovalHistory>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.RequestId == requestId)
            .OrderBy(x => x.OccurredUtc)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<TimetableApprovalHistory>)t.Result, cancellationToken);

    public Task<int> CountPendingAsync(int tenantId, CancellationToken cancellationToken = default) =>
        _context.Set<TimetableApprovalRequest>().CountAsync(x =>
            x.TenantId == tenantId && (x.Status == TimetableApprovalRequestStatus.Pending || x.Status == TimetableApprovalRequestStatus.InReview),
            cancellationToken);
}

public sealed class TimetableCloneJobRepository : ITimetableCloneJobRepository
{
    private readonly ApplicationDbContext _context;

    public TimetableCloneJobRepository(ApplicationDbContext context) => _context = context;

    public Task<TimetableCloneJob?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<TimetableCloneJob>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public Task<IReadOnlyList<TimetableCloneJob>> ListAsync(int tenantId, TimetableCloneJobStatus? status, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<TimetableCloneJob>().AsNoTracking().Where(x => x.TenantId == tenantId);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        return query.OrderByDescending(x => x.CreatedDate).ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<TimetableCloneJob>)t.Result, cancellationToken);
    }

    public async Task AddAsync(TimetableCloneJob entity, CancellationToken cancellationToken = default) =>
        await _context.Set<TimetableCloneJob>().AddAsync(entity, cancellationToken);

    public Task<TimetableCloneJob?> DequeueNextAsync(int tenantId, CancellationToken cancellationToken = default) =>
        _context.Set<TimetableCloneJob>()
            .Where(x => x.TenantId == tenantId && x.Status == TimetableCloneJobStatus.Queued)
            .OrderBy(x => x.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken);
}

public sealed class TimetableChangeHistoryRepository : ITimetableChangeHistoryRepository
{
    private readonly ApplicationDbContext _context;

    public TimetableChangeHistoryRepository(ApplicationDbContext context) => _context = context;

    public async Task AddAsync(TimetableChangeHistory entity, CancellationToken cancellationToken = default) =>
        await _context.Set<TimetableChangeHistory>().AddAsync(entity, cancellationToken);

    public Task<IReadOnlyList<TimetableChangeHistory>> ListAsync(int tenantId, TimetableChangeHistoryFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<TimetableChangeHistory>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.TimetableId == filter.TimetableId);
        if (filter.EntryId.HasValue) query = query.Where(x => x.EntryId == filter.EntryId.Value);
        if (filter.Operation.HasValue) query = query.Where(x => x.Operation == filter.Operation.Value);
        if (filter.FromUtc.HasValue) query = query.Where(x => x.OccurredUtc >= filter.FromUtc.Value);
        if (filter.ToUtc.HasValue) query = query.Where(x => x.OccurredUtc <= filter.ToUtc.Value);
        return query.OrderByDescending(x => x.OccurredUtc).ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<TimetableChangeHistory>)t.Result, cancellationToken);
    }

    public Task<int> CountRecentAsync(int tenantId, DateTime sinceUtc, CancellationToken cancellationToken = default) =>
        _context.Set<TimetableChangeHistory>().CountAsync(x => x.TenantId == tenantId && x.OccurredUtc >= sinceUtc, cancellationToken);
}

public sealed class TimetableWarningDismissalRepository : ITimetableWarningDismissalRepository
{
    private readonly ApplicationDbContext _context;

    public TimetableWarningDismissalRepository(ApplicationDbContext context) => _context = context;

    public Task<IReadOnlyList<TimetableWarningDismissal>> ListForTimetableAsync(int tenantId, int timetableId, CancellationToken cancellationToken = default) =>
        _context.Set<TimetableWarningDismissal>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.TimetableId == timetableId)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<TimetableWarningDismissal>)t.Result, cancellationToken);

    public async Task AddAsync(TimetableWarningDismissal entity, CancellationToken cancellationToken = default) =>
        await _context.Set<TimetableWarningDismissal>().AddAsync(entity, cancellationToken);

    public Task<bool> ExistsAsync(int tenantId, int timetableId, string code, int? entryId, int? staffId, int? roomId, byte? dayOfWeek, int? timeSlotId, CancellationToken cancellationToken = default) =>
        _context.Set<TimetableWarningDismissal>().AnyAsync(x =>
            x.TenantId == tenantId
            && x.TimetableId == timetableId
            && x.WarningCode == code
            && x.EntryId == entryId
            && x.StaffId == staffId
            && x.RoomId == roomId
            && x.DayOfWeek == dayOfWeek
            && x.TimeSlotId == timeSlotId, cancellationToken);
}

public sealed class VersionComparisonRepository : IVersionComparisonRepository
{
    private readonly ApplicationDbContext _context;

    public VersionComparisonRepository(ApplicationDbContext context) => _context = context;

    public Task<IReadOnlyList<TimetableEntry>> ListEntriesForVersionAsync(int tenantId, int scheduleVersionId, int? departmentId, CancellationToken cancellationToken = default)
    {
        var query =
            from e in _context.Set<TimetableEntry>().AsNoTracking()
            join t in _context.Set<Timetable>().AsNoTracking() on e.TimetableId equals t.Id
            where e.TenantId == tenantId && t.TenantId == tenantId && t.ScheduleVersionId == scheduleVersionId
            select e;
        if (departmentId.HasValue)
            query = query.Where(e => e.DepartmentId == departmentId.Value);
        return query.ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<TimetableEntry>)t.Result, cancellationToken);
    }
}

public sealed class TimetableApprovalCommentRepository : ITimetableApprovalCommentRepository
{
    private readonly ApplicationDbContext _context;

    public TimetableApprovalCommentRepository(ApplicationDbContext context) => _context = context;

    public async Task AddAsync(TimetableApprovalComment entity, CancellationToken cancellationToken = default) =>
        await _context.Set<TimetableApprovalComment>().AddAsync(entity, cancellationToken);

    public Task<IReadOnlyList<TimetableApprovalComment>> ListByRequestAsync(int tenantId, int requestId, CancellationToken cancellationToken = default) =>
        _context.Set<TimetableApprovalComment>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.RequestId == requestId)
            .OrderBy(x => x.OccurredUtc)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<TimetableApprovalComment>)t.Result, cancellationToken);
}

public sealed class TimetableDecisionHistoryRepository : ITimetableDecisionHistoryRepository
{
    private readonly ApplicationDbContext _context;

    public TimetableDecisionHistoryRepository(ApplicationDbContext context) => _context = context;

    public async Task AddAsync(TimetableDecisionHistory entity, CancellationToken cancellationToken = default) =>
        await _context.Set<TimetableDecisionHistory>().AddAsync(entity, cancellationToken);

    public Task<IReadOnlyList<TimetableDecisionHistory>> ListByRequestAsync(int tenantId, int requestId, CancellationToken cancellationToken = default) =>
        _context.Set<TimetableDecisionHistory>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.RequestId == requestId)
            .OrderBy(x => x.OccurredUtc)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<TimetableDecisionHistory>)t.Result, cancellationToken);
}

public sealed class ArchiveReasonRepository : IArchiveReasonRepository
{
    private readonly ApplicationDbContext _context;

    public ArchiveReasonRepository(ApplicationDbContext context) => _context = context;

    public Task<IReadOnlyList<ArchiveReasonLookup>> ListActiveAsync(int tenantId, CancellationToken cancellationToken = default) =>
        _context.Set<ArchiveReasonLookup>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<ArchiveReasonLookup>)t.Result, cancellationToken);

    public Task<ArchiveReasonLookup?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<ArchiveReasonLookup>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);
}
