using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>AI29.1B.5 — Advisory capacity recommendations only (no mutations).</summary>
public interface ISectionCapacityRecommendationService
{
    Task<IReadOnlyList<SectionCapacityRecommendationDto>> RecommendAsync(
        int? academicYearId = null,
        int? semesterId = null,
        CancellationToken cancellationToken = default);
}
