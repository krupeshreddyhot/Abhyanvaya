using Abhyanvaya.Domain.Entities.Scheduling;

namespace Abhyanvaya.Application.Common.Interfaces.Scheduling;

public interface IRoomFeatureRepository
{
    Task<IReadOnlyList<RoomFeature>> ListFeaturesAsync(int tenantId, string? category, bool? isActive, CancellationToken cancellationToken = default);
    Task<RoomFeature?> GetFeatureByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task<bool> FeatureCodeExistsAsync(int tenantId, string code, int? excludeId, CancellationToken cancellationToken = default);
    Task AddFeatureAsync(RoomFeature entity, CancellationToken cancellationToken = default);
    Task AddFeaturesAsync(IEnumerable<RoomFeature> entities, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoomFeatureAssignment>> ListAssignmentsByRoomAsync(int tenantId, int roomId, CancellationToken cancellationToken = default);
    Task<RoomFeatureAssignment?> GetAssignmentAsync(int tenantId, int roomId, int roomFeatureId, CancellationToken cancellationToken = default);
    Task<bool> AssignmentExistsAsync(int tenantId, int roomId, int roomFeatureId, CancellationToken cancellationToken = default);
    Task AddAssignmentAsync(RoomFeatureAssignment entity, CancellationToken cancellationToken = default);
    Task AddAssignmentsAsync(IEnumerable<RoomFeatureAssignment> entities, CancellationToken cancellationToken = default);
    Task RemoveAssignmentAsync(RoomFeatureAssignment entity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoomFeatureAssignment>> ListAssignmentsByRoomIdsAsync(int tenantId, IEnumerable<int> roomIds, CancellationToken cancellationToken = default);
}
