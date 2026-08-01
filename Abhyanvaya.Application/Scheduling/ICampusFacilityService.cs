using Abhyanvaya.Application.DTOs.Scheduling;

namespace Abhyanvaya.Application.Scheduling;

public interface ICampusFacilityService
{
    Task<IReadOnlyList<CampusDto>> ListCampusesAsync(CancellationToken cancellationToken = default);
    Task<CampusDto?> GetCampusByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<CampusDto> CreateCampusAsync(CreateCampusRequest request, CancellationToken cancellationToken = default);
    Task<CampusDto> UpdateCampusAsync(UpdateCampusRequest request, CancellationToken cancellationToken = default);
    Task DeleteCampusAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BuildingDto>> ListBuildingsAsync(int? campusId, CancellationToken cancellationToken = default);
    Task<BuildingDto?> GetBuildingByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<BuildingDto> CreateBuildingAsync(CreateBuildingRequest request, CancellationToken cancellationToken = default);
    Task<BuildingDto> UpdateBuildingAsync(UpdateBuildingRequest request, CancellationToken cancellationToken = default);
    Task DeleteBuildingAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FloorDto>> ListFloorsAsync(int? buildingId, CancellationToken cancellationToken = default);
    Task<FloorDto?> GetFloorByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<FloorDto> CreateFloorAsync(CreateFloorRequest request, CancellationToken cancellationToken = default);
    Task<FloorDto> UpdateFloorAsync(UpdateFloorRequest request, CancellationToken cancellationToken = default);
    Task DeleteFloorAsync(int id, CancellationToken cancellationToken = default);
    Task<PagedRoomsResult> SearchRoomsAsync(RoomSearchQuery query, CancellationToken cancellationToken = default);
    Task<RoomDto?> GetRoomByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<RoomDto> CreateRoomAsync(CreateRoomRequest request, CancellationToken cancellationToken = default);
    Task<RoomDto> UpdateRoomAsync(UpdateRoomRequest request, CancellationToken cancellationToken = default);
    Task DeleteRoomAsync(int id, CancellationToken cancellationToken = default);
}
