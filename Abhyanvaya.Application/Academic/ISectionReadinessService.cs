using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI29.1B — Advisory operational readiness only. Never allocates faculty, moves students,
/// creates rooms, or modifies timetables.
/// </summary>
public interface ISectionReadinessService
{
    Task<SectionReadinessDto> EvaluateAsync(int sectionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SectionReadinessDto>> EvaluateManyAsync(
        int? academicYearId = null,
        int? semesterId = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SectionReadinessDto>> GetSectionHealthAsync(CancellationToken cancellationToken = default);
}
