using Abhyanvaya.Application.ModelLifecycle;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Aggregates recognition quality metrics — no inference (AI20.PHASE2.5).</summary>
public interface IRecognitionQualityEngine
{
    Task<RecognitionQualitySummary> BuildDailySummaryAsync(QualityAggregationRequest request, CancellationToken cancellationToken = default);

    Task<RecognitionQualitySummary> BuildWeeklySummaryAsync(QualityAggregationRequest request, CancellationToken cancellationToken = default);

    Task<RecognitionQualitySummary> BuildMonthlySummaryAsync(QualityAggregationRequest request, CancellationToken cancellationToken = default);
}
