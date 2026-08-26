using Abhyanvaya.Application.DTOs.Course;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI29.1D.24 Prompt 4B — Course Master create/update with transactional Program assignment orchestration.
/// </summary>
public interface ICourseMasterWriteService
{
    Task<CourseMasterRowDto> CreateAsync(CreateCourseRequest request, CancellationToken cancellationToken = default);
    Task<CourseMasterRowDto> UpdateAsync(UpdateCourseRequest request, CancellationToken cancellationToken = default);
}
