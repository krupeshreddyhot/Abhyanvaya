using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Infrastructure.Persistence.Repositories.Scheduling;

public sealed class TimeSlotRepository : ITimeSlotRepository
{
    private readonly ApplicationDbContext _context;

    public TimeSlotRepository(ApplicationDbContext context) => _context = context;

    public Task<IReadOnlyList<TimeSlotSet>> ListSetsAsync(int tenantId, int? academicYearId, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<TimeSlotSet>().AsNoTracking().Where(x => x.TenantId == tenantId);
        if (academicYearId.HasValue) query = query.Where(x => x.AcademicYearId == academicYearId.Value);
        return query.OrderBy(x => x.Name).ToListAsync(cancellationToken).ContinueWith(t => (IReadOnlyList<TimeSlotSet>)t.Result, cancellationToken);
    }

    public Task<TimeSlotSet?> GetSetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<TimeSlotSet>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public Task<TimeSlotSet?> GetSetWithSlotsAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<TimeSlotSet>().Include(x => x.TimeSlots).FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public Task<bool> SetCodeExistsAsync(int tenantId, string code, int? excludeId, CancellationToken cancellationToken = default) =>
        _context.Set<TimeSlotSet>().AnyAsync(x => x.TenantId == tenantId && x.Code == code && (!excludeId.HasValue || x.Id != excludeId.Value), cancellationToken);

    public async Task AddSetAsync(TimeSlotSet entity, CancellationToken cancellationToken = default) =>
        await _context.Set<TimeSlotSet>().AddAsync(entity, cancellationToken);

    public Task<IReadOnlyList<TimeSlot>> ListSlotsAsync(int tenantId, int timeSlotSetId, CancellationToken cancellationToken = default) =>
        _context.Set<TimeSlot>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.TimeSlotSetId == timeSlotSetId)
            .OrderBy(x => x.DayOfWeek).ThenBy(x => x.StartTime)
            .ToListAsync(cancellationToken).ContinueWith(t => (IReadOnlyList<TimeSlot>)t.Result, cancellationToken);

    public Task<TimeSlot?> GetSlotByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<TimeSlot>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public async Task AddSlotAsync(TimeSlot entity, CancellationToken cancellationToken = default) =>
        await _context.Set<TimeSlot>().AddAsync(entity, cancellationToken);

    public async Task AddRangeAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class =>
        await _context.Set<T>().AddRangeAsync(entities, cancellationToken);
}
