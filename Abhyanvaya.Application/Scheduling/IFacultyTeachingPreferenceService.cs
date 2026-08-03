using Abhyanvaya.Application.DTOs.Scheduling;

namespace Abhyanvaya.Application.Scheduling;

public interface IFacultyTeachingPreferenceService
{
    Task<IReadOnlyList<FacultyTeachingPreferenceDto>> ListAsync(int? academicYearId, int? staffId, bool? isActive, CancellationToken cancellationToken = default);
    Task<FacultyTeachingPreferenceDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<FacultyTeachingPreferenceDto> CreateAsync(CreateFacultyTeachingPreferenceRequest request, CancellationToken cancellationToken = default);
    Task<FacultyTeachingPreferenceDto> UpdateAsync(UpdateFacultyTeachingPreferenceRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
