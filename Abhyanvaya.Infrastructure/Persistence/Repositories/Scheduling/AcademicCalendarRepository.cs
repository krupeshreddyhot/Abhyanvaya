using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Infrastructure.Persistence.Repositories.Scheduling;

public sealed class AcademicCalendarRepository : IAcademicCalendarRepository
{
    private readonly ApplicationDbContext _context;

    public AcademicCalendarRepository(ApplicationDbContext context) => _context = context;

    public Task<IReadOnlyList<AcademicYear>> ListYearsAsync(int tenantId, CancellationToken cancellationToken = default) =>
        _context.Set<AcademicYear>()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.StartDate)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<AcademicYear>)t.Result, cancellationToken);

    public Task<AcademicYear?> GetYearByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<AcademicYear>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public Task<AcademicYear?> GetYearWithDetailsAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<AcademicYear>()
            .Include(x => x.Terms)
            .Include(x => x.WorkingDays)
            .Include(x => x.Holidays)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public Task<AcademicYear?> GetCurrentYearAsync(int tenantId, CancellationToken cancellationToken = default) =>
        _context.Set<AcademicYear>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.IsCurrent, cancellationToken);

    public Task<bool> YearCodeExistsAsync(int tenantId, string code, int? excludeId, CancellationToken cancellationToken = default) =>
        _context.Set<AcademicYear>().AnyAsync(
            x => x.TenantId == tenantId && x.Code == code && (!excludeId.HasValue || x.Id != excludeId.Value),
            cancellationToken);

    public async Task AddYearAsync(AcademicYear entity, CancellationToken cancellationToken = default) =>
        await _context.Set<AcademicYear>().AddAsync(entity, cancellationToken);

    public Task<IReadOnlyList<AcademicTerm>> ListTermsAsync(int tenantId, int? academicYearId, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<AcademicTerm>().AsNoTracking().Where(x => x.TenantId == tenantId);
        if (academicYearId.HasValue)
            query = query.Where(x => x.AcademicYearId == academicYearId.Value);
        return query.OrderBy(x => x.Sequence).ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<AcademicTerm>)t.Result, cancellationToken);
    }

    public Task<AcademicTerm?> GetTermByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<AcademicTerm>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public async Task AddTermAsync(AcademicTerm entity, CancellationToken cancellationToken = default) =>
        await _context.Set<AcademicTerm>().AddAsync(entity, cancellationToken);

    public Task<IReadOnlyList<WorkingDay>> ListWorkingDaysAsync(int tenantId, int academicYearId, CancellationToken cancellationToken = default) =>
        _context.Set<WorkingDay>()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)
            .OrderBy(x => x.DayOfWeek)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<WorkingDay>)t.Result, cancellationToken);

    public Task<WorkingDay?> GetWorkingDayByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<WorkingDay>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public async Task AddWorkingDayAsync(WorkingDay entity, CancellationToken cancellationToken = default) =>
        await _context.Set<WorkingDay>().AddAsync(entity, cancellationToken);

    public Task<IReadOnlyList<Holiday>> ListHolidaysAsync(int tenantId, int? academicYearId, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Holiday>().AsNoTracking().Where(x => x.TenantId == tenantId);
        if (academicYearId.HasValue)
            query = query.Where(x => x.AcademicYearId == academicYearId.Value);
        return query.OrderBy(x => x.Date).ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<Holiday>)t.Result, cancellationToken);
    }

    public Task<Holiday?> GetHolidayByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<Holiday>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public async Task AddHolidayAsync(Holiday entity, CancellationToken cancellationToken = default) =>
        await _context.Set<Holiday>().AddAsync(entity, cancellationToken);

    public async Task AddRangeAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class =>
        await _context.Set<T>().AddRangeAsync(entities, cancellationToken);
}
