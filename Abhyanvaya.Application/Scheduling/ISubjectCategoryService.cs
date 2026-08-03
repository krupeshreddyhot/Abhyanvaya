using Abhyanvaya.Application.DTOs.Scheduling;



namespace Abhyanvaya.Application.Scheduling;



public interface ISubjectCategoryService

{

    Task<IReadOnlyList<SubjectCategoryDto>> ListAsync(bool? isActive, CancellationToken cancellationToken = default);

    Task<SubjectCategoryDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<SubjectCategoryDto> CreateAsync(CreateSubjectCategoryRequest request, CancellationToken cancellationToken = default);

    Task<SubjectCategoryDto> UpdateAsync(UpdateSubjectCategoryRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task EnsureDefaultsAsync(CancellationToken cancellationToken = default);

    Task UpdateSubjectCategoryFieldsAsync(UpdateSubjectSchedulingCategoryRequest request, CancellationToken cancellationToken = default);

}

