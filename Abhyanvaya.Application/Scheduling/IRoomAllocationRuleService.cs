using Abhyanvaya.Application.DTOs.Scheduling;

namespace Abhyanvaya.Application.Scheduling;

public interface IRoomAllocationRuleService
{
    Task<IReadOnlyList<RoomAllocationRuleDto>> ListAsync(int? academicYearId, CancellationToken cancellationToken = default);
    Task<RoomAllocationRuleDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<RoomAllocationRuleDto> CreateAsync(CreateRoomAllocationRuleRequest request, CancellationToken cancellationToken = default);
    Task<RoomAllocationRuleDto> UpdateAsync(UpdateRoomAllocationRuleRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
