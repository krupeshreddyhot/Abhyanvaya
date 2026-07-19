using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Recognition;

namespace Abhyanvaya.Infrastructure.Recognition.Engine;

public sealed class CosineSimilarityProvider : ISimilarityProvider
{
    public SimilarityMetric Metric => SimilarityMetric.Cosine;

    public float ComputeDistance(float[] query, float[] candidate)
    {
        if (query.Length != candidate.Length || query.Length == 0)
        {
            return 1f;
        }

        var dot = 0f;
        for (var i = 0; i < query.Length; i++)
        {
            dot += query[i] * candidate[i];
        }

        return 1f - Math.Clamp(dot, -1f, 1f);
    }

    public float NormalizeScore(float distance) =>
        Math.Clamp(1f - distance, 0f, 1f);
}

public sealed class EuclideanSimilarityProvider : ISimilarityProvider
{
    public SimilarityMetric Metric => SimilarityMetric.Euclidean;

    public float ComputeDistance(float[] query, float[] candidate)
    {
        if (query.Length != candidate.Length || query.Length == 0)
        {
            return float.MaxValue;
        }

        var sum = 0f;
        for (var i = 0; i < query.Length; i++)
        {
            var diff = query[i] - candidate[i];
            sum += diff * diff;
        }

        return MathF.Sqrt(sum);
    }

    public float NormalizeScore(float distance)
    {
        if (float.IsInfinity(distance) || float.IsNaN(distance))
        {
            return 0f;
        }

        return 1f / (1f + distance);
    }
}

public sealed class InnerProductSimilarityProvider : ISimilarityProvider
{
    public SimilarityMetric Metric => SimilarityMetric.InnerProduct;

    public float ComputeDistance(float[] query, float[] candidate)
    {
        if (query.Length != candidate.Length || query.Length == 0)
        {
            return 0f;
        }

        var dot = 0f;
        for (var i = 0; i < query.Length; i++)
        {
            dot += query[i] * candidate[i];
        }

        return -dot;
    }

    public float NormalizeScore(float distance)
    {
        var similarity = -distance;
        return Math.Clamp((similarity + 1f) / 2f, 0f, 1f);
    }
}
