using Abhyanvaya.Application.Recognition;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Similarity metric provider — Cosine, Euclidean, Inner Product (AI20.PHASE2.3).</summary>
public interface ISimilarityProvider
{
    SimilarityMetric Metric { get; }

    float ComputeDistance(float[] query, float[] candidate);

    float NormalizeScore(float distance);
}
