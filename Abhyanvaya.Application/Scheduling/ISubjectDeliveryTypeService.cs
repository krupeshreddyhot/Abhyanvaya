using Abhyanvaya.Application.DTOs.Scheduling;

namespace Abhyanvaya.Application.Scheduling;

public interface ISubjectDeliveryTypeService
{
    Task<IReadOnlyList<SubjectDeliveryTypeDto>> ListAsync(bool? isActive, CancellationToken cancellationToken = default);
    Task<SubjectDeliveryTypeDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<SubjectDeliveryTypeDto> CreateAsync(CreateSubjectDeliveryTypeRequest request, CancellationToken cancellationToken = default);
    Task<SubjectDeliveryTypeDto> UpdateAsync(UpdateSubjectDeliveryTypeRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task EnsureDefaultsAsync(CancellationToken cancellationToken = default);
    Task UpdateSubjectDeliveryFieldsAsync(UpdateSubjectDeliveryFieldsRequest request, CancellationToken cancellationToken = default);
}
