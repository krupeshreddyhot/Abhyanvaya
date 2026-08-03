using Abhyanvaya.Domain.Entities.Scheduling;

namespace Abhyanvaya.Application.Common.Interfaces.Scheduling;

public interface ITimeSlotRepository
{
    Task<IReadOnlyList<TimeSlotSet>> ListSetsAsync(int tenantId, int? academicYearId, CancellationToken cancellationToken = default);
    Task<TimeSlotSet?> GetSetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task<TimeSlotSet?> GetSetWithSlotsAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task<bool> SetCodeExistsAsync(int tenantId, string code, int? excludeId, CancellationToken cancellationToken = default);
    Task AddSetAsync(TimeSlotSet entity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimeSlot>> ListSlotsAsync(int tenantId, int timeSlotSetId, CancellationToken cancellationToken = default);
    Task<TimeSlot?> GetSlotByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task AddSlotAsync(TimeSlot entity, CancellationToken cancellationToken = default);
    Task AddRangeAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class;
}
