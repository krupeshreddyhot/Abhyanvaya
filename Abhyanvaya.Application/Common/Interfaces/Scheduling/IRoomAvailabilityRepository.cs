using Abhyanvaya.Domain.Entities.Scheduling;



namespace Abhyanvaya.Application.Common.Interfaces.Scheduling;



public interface IRoomAvailabilityRepository

{

    Task<IReadOnlyList<RoomAvailability>> ListAsync(int tenantId, int? academicYearId, int? roomId, CancellationToken cancellationToken = default);

    Task<RoomAvailability?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoomAvailability>> GetOverlappingAsync(int tenantId, int roomId, int academicYearId, DateOnly startDate, DateOnly endDate, int? startSlotId, int? endSlotId, int? excludeId, CancellationToken cancellationToken = default);

    Task AddAsync(RoomAvailability entity, CancellationToken cancellationToken = default);

}

