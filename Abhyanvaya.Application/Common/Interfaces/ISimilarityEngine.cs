using Abhyanvaya.Application.Recognition;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Scores and normalizes candidate vectors — no threshold decisions (AI20.PHASE2.3).</summary>
public interface ISimilarityEngine
{
    Task<IReadOnlyList<SimilarityMatch>> RankAsync(
        IReadOnlyList<RecognitionSearchResult> searchResults,
        SimilarityMetric metric,
        CancellationToken cancellationToken = default);

    SimilarityStatistics ComputeStatistics(IReadOnlyList<SimilarityMatch> matches);
}
