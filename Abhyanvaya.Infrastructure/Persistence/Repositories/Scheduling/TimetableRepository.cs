using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Infrastructure.Persistence.Repositories.Scheduling;

public sealed class TimetableRepository : ITimetableRepository
{
    private readonly ApplicationDbContext _context;

    public TimetableRepository(ApplicationDbContext context) => _context = context;

    public Task<IReadOnlyList<Timetable>> ListAsync(int tenantId, int? academicYearId, TimetableStatus? status, int? departmentId, bool includeArchived, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Timetable>().AsNoTracking().Where(x => x.TenantId == tenantId);
        if (academicYearId.HasValue) query = query.Where(x => x.AcademicYearId == academicYearId.Value);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (departmentId.HasValue) query = query.Where(x => x.DepartmentId == departmentId.Value);
        if (!includeArchived) query = query.Where(x => x.Status != TimetableStatus.Archived);
        return query.OrderBy(x => x.Name).ToListAsync(cancellationToken).ContinueWith(t => (IReadOnlyList<Timetable>)t.Result, cancellationToken);
    }

    public Task<Timetable?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<Timetable>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public Task<Timetable?> GetByIdWithEntriesAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<Timetable>().Include(x => x.Entries.Where(e => !e.IsDeleted)).FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(int tenantId, int academicYearId, string code, int? excludeId, CancellationToken cancellationToken = default) =>
        _context.Set<Timetable>().AnyAsync(x =>
            x.TenantId == tenantId
            && x.AcademicYearId == academicYearId
            && x.Code == code
            && (!excludeId.HasValue || x.Id != excludeId.Value),
            cancellationToken);

    public async Task AddAsync(Timetable entity, CancellationToken cancellationToken = default) =>
        await _context.Set<Timetable>().AddAsync(entity, cancellationToken);

    public Task<int> CountEntriesAsync(int tenantId, int timetableId, CancellationToken cancellationToken = default) =>
        _context.Set<TimetableEntry>().CountAsync(x => x.TenantId == tenantId && x.TimetableId == timetableId, cancellationToken);

    private IQueryable<TimetableEntry> EntriesQuery(int tenantId, int timetableId) =>
        _context.Set<TimetableEntry>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.TimetableId == timetableId)
            .OrderBy(x => x.DayOfWeek).ThenBy(x => x.TimeSlotId);

    public Task<IReadOnlyList<TimetableEntry>> ListEntriesAsync(int tenantId, int timetableId, CancellationToken cancellationToken = default) =>
        EntriesQuery(tenantId, timetableId).ToListAsync(cancellationToken).ContinueWith(t => (IReadOnlyList<TimetableEntry>)t.Result, cancellationToken);

    public Task<IReadOnlyList<TimetableEntry>> ListEntriesByStaffAsync(int tenantId, int timetableId, int staffId, CancellationToken cancellationToken = default) =>
        EntriesQuery(tenantId, timetableId).Where(x => x.StaffId == staffId).ToListAsync(cancellationToken).ContinueWith(t => (IReadOnlyList<TimetableEntry>)t.Result, cancellationToken);

    public Task<IReadOnlyList<TimetableEntry>> ListEntriesByRoomAsync(int tenantId, int timetableId, int roomId, CancellationToken cancellationToken = default) =>
        EntriesQuery(tenantId, timetableId).Where(x => x.RoomId == roomId).ToListAsync(cancellationToken).ContinueWith(t => (IReadOnlyList<TimetableEntry>)t.Result, cancellationToken);

    public Task<IReadOnlyList<TimetableEntry>> ListEntriesByStudentAsync(int tenantId, int timetableId, int courseId, int groupId, int semesterId, CancellationToken cancellationToken = default) =>
        EntriesQuery(tenantId, timetableId)
            .Where(x => x.CourseId == courseId && x.GroupId == groupId && x.SemesterId == semesterId)
            .ToListAsync(cancellationToken).ContinueWith(t => (IReadOnlyList<TimetableEntry>)t.Result, cancellationToken);

    public Task<IReadOnlyList<TimetableEntry>> ListEntriesByDepartmentAsync(int tenantId, int timetableId, int departmentId, CancellationToken cancellationToken = default) =>
        EntriesQuery(tenantId, timetableId).Where(x => x.DepartmentId == departmentId).ToListAsync(cancellationToken).ContinueWith(t => (IReadOnlyList<TimetableEntry>)t.Result, cancellationToken);

    public Task<TimetableEntry?> GetEntryByIdAsync(int tenantId, int entryId, CancellationToken cancellationToken = default) =>
        _context.Set<TimetableEntry>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == entryId, cancellationToken);

    public async Task AddEntryAsync(TimetableEntry entity, CancellationToken cancellationToken = default) =>
        await _context.Set<TimetableEntry>().AddAsync(entity, cancellationToken);

    public async Task AddEntriesAsync(IEnumerable<TimetableEntry> entities, CancellationToken cancellationToken = default) =>
        await _context.Set<TimetableEntry>().AddRangeAsync(entities, cancellationToken);
}
