using Abhyanvaya.Application.Common.Interfaces.Scheduling;

using Abhyanvaya.Domain.Entities.Scheduling;

using Microsoft.EntityFrameworkCore;



namespace Abhyanvaya.Infrastructure.Persistence.Repositories.Scheduling;



public sealed class TimeSlotTemplateRepository : ITimeSlotTemplateRepository

{

    private readonly ApplicationDbContext _context;



    public TimeSlotTemplateRepository(ApplicationDbContext context) => _context = context;



    public Task<IReadOnlyList<TimeSlotTemplate>> ListAsync(int tenantId, CancellationToken cancellationToken = default) =>

        _context.Set<TimeSlotTemplate>()

            .AsNoTracking()

            .Where(x => x.TenantId == tenantId)

            .OrderBy(x => x.Name)

            .ToListAsync(cancellationToken)

            .ContinueWith(t => (IReadOnlyList<TimeSlotTemplate>)t.Result, cancellationToken);



    public Task<TimeSlotTemplate?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>

        _context.Set<TimeSlotTemplate>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);



    public Task<TimeSlotTemplate?> GetWithSetsAndSlotsAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>

        _context.Set<TimeSlotTemplate>()

            .Include(x => x.TimeSlotSets.Where(s => !s.IsDeleted))

            .ThenInclude(s => s.TimeSlots.Where(t => !t.IsDeleted))

            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);



    public Task<bool> HasSetWithSlotsAsync(int tenantId, int templateId, CancellationToken cancellationToken = default) =>

        _context.Set<TimeSlotSet>()

            .AnyAsync(s => s.TenantId == tenantId

                && s.TimeSlotTemplateId == templateId

                && s.TimeSlots.Any(t => !t.IsDeleted),

                cancellationToken);



    public async Task ClearDefaultAsync(int tenantId, int? excludeId, CancellationToken cancellationToken = default)

    {

        var defaults = await _context.Set<TimeSlotTemplate>()

            .Where(x => x.TenantId == tenantId && x.IsDefault && (!excludeId.HasValue || x.Id != excludeId.Value))

            .ToListAsync(cancellationToken);

        foreach (var item in defaults)

            item.IsDefault = false;

    }



    public async Task AddAsync(TimeSlotTemplate entity, CancellationToken cancellationToken = default) =>

        await _context.Set<TimeSlotTemplate>().AddAsync(entity, cancellationToken);

}

