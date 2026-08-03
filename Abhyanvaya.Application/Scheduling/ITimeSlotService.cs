using Abhyanvaya.Application.DTOs.Scheduling;

namespace Abhyanvaya.Application.Scheduling;

public interface ITimeSlotService
{
    Task<IReadOnlyList<TimeSlotSetDto>> ListSetsAsync(int? academicYearId, CancellationToken cancellationToken = default);
    Task<TimeSlotSetDto?> GetSetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<TimeSlotSetDto> CreateSetAsync(CreateTimeSlotSetRequest request, CancellationToken cancellationToken = default);
    Task<TimeSlotSetDto> UpdateSetAsync(UpdateTimeSlotSetRequest request, CancellationToken cancellationToken = default);
    Task DeleteSetAsync(int id, CancellationToken cancellationToken = default);
    Task<TimeSlotSetDto> CloneSetAsync(CloneTimeSlotSetRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimeSlotDto>> ListSlotsAsync(int timeSlotSetId, CancellationToken cancellationToken = default);
    Task<TimeSlotDto?> GetSlotByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<TimeSlotDto> CreateSlotAsync(CreateTimeSlotRequest request, CancellationToken cancellationToken = default);
    Task<TimeSlotDto> UpdateSlotAsync(UpdateTimeSlotRequest request, CancellationToken cancellationToken = default);
    Task DeleteSlotAsync(int id, CancellationToken cancellationToken = default);
}
