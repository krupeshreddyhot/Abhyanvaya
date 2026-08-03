using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Common.Interfaces.Scheduling;

public interface ICampusFacilityRepository
{
    Task<IReadOnlyList<Campus>> ListCampusesAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<Campus?> GetCampusByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task<bool> CampusCodeExistsAsync(int tenantId, string code, int? excludeId, CancellationToken cancellationToken = default);
    Task AddCampusAsync(Campus entity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Building>> ListBuildingsAsync(int tenantId, int? campusId, CancellationToken cancellationToken = default);
    Task<Building?> GetBuildingByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task AddBuildingAsync(Building entity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Floor>> ListFloorsAsync(int tenantId, int? buildingId, CancellationToken cancellationToken = default);
    Task<Floor?> GetFloorByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task AddFloorAsync(Floor entity, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Room> Items, int TotalCount)> SearchRoomsAsync(
        int tenantId,
        string? search,
        RoomType? roomType,
        RoomStatus? status,
        int? campusId,
        int? buildingId,
        int? floorId,
        bool? isActive,
        string? sortBy,
        bool sortDescending,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
    Task<Room?> GetRoomByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task AddRoomAsync(Room entity, CancellationToken cancellationToken = default);
}
