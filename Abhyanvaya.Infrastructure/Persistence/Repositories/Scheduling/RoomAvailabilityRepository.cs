using Abhyanvaya.Application.Common.Interfaces.Scheduling;

using Abhyanvaya.Domain.Entities.Scheduling;

using Microsoft.EntityFrameworkCore;



namespace Abhyanvaya.Infrastructure.Persistence.Repositories.Scheduling;



public sealed class RoomAvailabilityRepository : IRoomAvailabilityRepository

{

    private readonly ApplicationDbContext _context;



    public RoomAvailabilityRepository(ApplicationDbContext context) => _context = context;



    public Task<IReadOnlyList<RoomAvailability>> ListAsync(int tenantId, int? academicYearId, int? roomId, CancellationToken cancellationToken = default)

    {

        var query = _context.Set<RoomAvailability>().AsNoTracking().Where(x => x.TenantId == tenantId);

        if (academicYearId.HasValue) query = query.Where(x => x.AcademicYearId == academicYearId.Value);

        if (roomId.HasValue) query = query.Where(x => x.RoomId == roomId.Value);

        return query.OrderBy(x => x.StartDate).ToListAsync(cancellationToken)

            .ContinueWith(t => (IReadOnlyList<RoomAvailability>)t.Result, cancellationToken);

    }



    public Task<RoomAvailability?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>

        _context.Set<RoomAvailability>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);



    public Task<IReadOnlyList<RoomAvailability>> GetOverlappingAsync(int tenantId, int roomId, int academicYearId, DateOnly startDate, DateOnly endDate, int? startSlotId, int? endSlotId, int? excludeId, CancellationToken cancellationToken = default) =>

        _context.Set<RoomAvailability>()

            .AsNoTracking()

            .Where(x => x.TenantId == tenantId

                && x.RoomId == roomId

                && x.AcademicYearId == academicYearId

                && x.StartDate <= endDate

                && x.EndDate >= startDate

                && (!excludeId.HasValue || x.Id != excludeId.Value))

            .ToListAsync(cancellationToken)

            .ContinueWith(t => (IReadOnlyList<RoomAvailability>)t.Result, cancellationToken);



    public async Task AddAsync(RoomAvailability entity, CancellationToken cancellationToken = default) =>

        await _context.Set<RoomAvailability>().AddAsync(entity, cancellationToken);

}

