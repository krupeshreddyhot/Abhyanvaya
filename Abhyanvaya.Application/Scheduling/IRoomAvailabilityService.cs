using Abhyanvaya.Application.DTOs.Scheduling;



namespace Abhyanvaya.Application.Scheduling;



public interface IRoomAvailabilityService

{

    Task<IReadOnlyList<RoomAvailabilityDto>> ListAsync(int? academicYearId, int? roomId, CancellationToken cancellationToken = default);

    Task<RoomAvailabilityDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<RoomAvailabilityDto> CreateAsync(CreateRoomAvailabilityRequest request, CancellationToken cancellationToken = default);

    Task<RoomAvailabilityDto> UpdateAsync(UpdateRoomAvailabilityRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

}

