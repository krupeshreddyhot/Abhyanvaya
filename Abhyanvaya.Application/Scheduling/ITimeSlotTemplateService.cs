using Abhyanvaya.Application.DTOs.Scheduling;



namespace Abhyanvaya.Application.Scheduling;



public interface ITimeSlotTemplateService

{

    Task<IReadOnlyList<TimeSlotTemplateDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<TimeSlotTemplateDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<TimeSlotTemplatePreviewDto?> PreviewAsync(int id, CancellationToken cancellationToken = default);

    Task<TimeSlotTemplateDto> CreateAsync(CreateTimeSlotTemplateRequest request, CancellationToken cancellationToken = default);

    Task<TimeSlotTemplateDto> UpdateAsync(UpdateTimeSlotTemplateRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<TimeSlotTemplateDto> CloneAsync(CloneTimeSlotTemplateRequest request, CancellationToken cancellationToken = default);

    Task<TimeSlotTemplateDto> SetDefaultAsync(int id, CancellationToken cancellationToken = default);

}

