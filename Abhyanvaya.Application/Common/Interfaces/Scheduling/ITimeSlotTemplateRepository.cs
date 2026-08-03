using Abhyanvaya.Domain.Entities.Scheduling;



namespace Abhyanvaya.Application.Common.Interfaces.Scheduling;



public interface ITimeSlotTemplateRepository

{

    Task<IReadOnlyList<TimeSlotTemplate>> ListAsync(int tenantId, CancellationToken cancellationToken = default);

    Task<TimeSlotTemplate?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);

    Task<TimeSlotTemplate?> GetWithSetsAndSlotsAsync(int tenantId, int id, CancellationToken cancellationToken = default);

    Task<bool> HasSetWithSlotsAsync(int tenantId, int templateId, CancellationToken cancellationToken = default);

    Task ClearDefaultAsync(int tenantId, int? excludeId, CancellationToken cancellationToken = default);

    Task AddAsync(TimeSlotTemplate entity, CancellationToken cancellationToken = default);

}

