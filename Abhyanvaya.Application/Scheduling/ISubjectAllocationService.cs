using Abhyanvaya.Application.DTOs.Scheduling;

namespace Abhyanvaya.Application.Scheduling;

public interface ISubjectAllocationService
{
    Task<IReadOnlyList<SubjectAllocationDto>> ListAsync(int? academicYearId, int? staffId, int? departmentId, CancellationToken cancellationToken = default);
    Task<SubjectAllocationDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<SubjectAllocationDto> CreateAsync(CreateSubjectAllocationRequest request, CancellationToken cancellationToken = default);
    Task<SubjectAllocationDto> UpdateAsync(UpdateSubjectAllocationRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
