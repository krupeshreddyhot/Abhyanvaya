using Abhyanvaya.Application.DTOs.Scheduling;

namespace Abhyanvaya.Application.Scheduling;

public interface IRoomFeatureService
{
    Task<IReadOnlyList<RoomFeatureDto>> ListFeaturesAsync(string? category, bool? isActive, CancellationToken cancellationToken = default);
    Task<RoomFeatureDto?> GetFeatureByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<RoomFeatureDto> CreateFeatureAsync(CreateRoomFeatureRequest request, CancellationToken cancellationToken = default);
    Task<RoomFeatureDto> UpdateFeatureAsync(UpdateRoomFeatureRequest request, CancellationToken cancellationToken = default);
    Task DeleteFeatureAsync(int id, CancellationToken cancellationToken = default);
    Task EnsureDefaultsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoomFeatureAssignmentDto>> ListAssignmentsByRoomAsync(int roomId, CancellationToken cancellationToken = default);
    Task<RoomFeatureAssignmentDto> AssignFeatureAsync(int roomId, AssignRoomFeatureRequest request, CancellationToken cancellationToken = default);
    Task UnassignFeatureAsync(int roomId, int roomFeatureId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoomFeatureAssignmentDto>> CloneAssignmentsAsync(CloneRoomFeatureAssignmentsRequest request, CancellationToken cancellationToken = default);
}
