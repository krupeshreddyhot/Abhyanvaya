using Abhyanvaya.Domain.Entities.Scheduling;

namespace Abhyanvaya.Application.Common.Interfaces.Scheduling;

public interface IRoomAllocationRuleRepository
{
    Task<IReadOnlyList<RoomAllocationRule>> ListAsync(int tenantId, int? academicYearId, CancellationToken cancellationToken = default);
    Task<RoomAllocationRule?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task AddAsync(RoomAllocationRule entity, CancellationToken cancellationToken = default);
}
