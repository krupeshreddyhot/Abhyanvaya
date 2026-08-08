using Abhyanvaya.Application.Academic.Observability;
using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

public sealed class SectionCapacityRecommendationService : ISectionCapacityRecommendationService
{
    private readonly ISectionCapacityEngine _capacity;
    private readonly IAcademicTelemetryService _telemetry;

    public SectionCapacityRecommendationService(
        ISectionCapacityEngine capacity,
        IAcademicTelemetryService telemetry)
    {
        _capacity = capacity;
        _telemetry = telemetry;
    }

    public Task<IReadOnlyList<SectionCapacityRecommendationDto>> RecommendAsync(
        int? academicYearId = null,
        int? semesterId = null,
        CancellationToken cancellationToken = default)
        => _telemetry.TrackAsync(
            AcademicOperations.SectionCapacityRecommend,
            "SectionCapacity.Recommend",
            ct => BuildAsync(academicYearId, semesterId, ct),
            cancellationToken);

    private async Task<IReadOnlyList<SectionCapacityRecommendationDto>> BuildAsync(
        int? academicYearId,
        int? semesterId,
        CancellationToken ct)
    {
        var rows = await _capacity.GetOccupancyAsync(null, academicYearId, semesterId, ct);
        return rows.Select(r =>
        {
            string rec;
            string rationale;
            if (r.IsOverCapacity || r.OccupancyPercent >= 95)
            {
                rec = "SplitCandidate";
                rationale = $"Occupancy {r.OccupancyPercent}% suggests split or capacity increase.";
                if (r.IsOverCapacity) rec = "IncreaseCapacity";
            }
            else if (r.IsUnderCapacity && r.CurrentStrength > 0)
            {
                rec = r.OccupancyPercent <= 25 ? "MergeCandidate" : "DecreaseCapacity";
                rationale = $"Occupancy {r.OccupancyPercent}% suggests consolidation or lower planning capacity.";
            }
            else if (r.HasWarning)
            {
                rec = "IncreaseCapacity";
                rationale = string.Join(" ", r.Warnings);
            }
            else
            {
                rec = "Healthy";
                rationale = $"Occupancy {r.OccupancyPercent}% within policy thresholds.";
            }

            return new SectionCapacityRecommendationDto
            {
                SectionId = r.SectionId,
                SectionCode = r.SectionCode,
                Recommendation = rec,
                Rationale = rationale,
                OccupancyPercent = r.OccupancyPercent,
            };
        }).ToList();
    }
}
