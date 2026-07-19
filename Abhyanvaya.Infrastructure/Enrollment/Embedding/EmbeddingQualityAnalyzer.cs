using Abhyanvaya.Application.Common.Interfaces;

namespace Abhyanvaya.Infrastructure.Enrollment.Embedding;

/// <summary>Advisory embedding quality diagnostics for enrollment analytics.</summary>
public sealed class EmbeddingQualityAnalyzer : IEmbeddingQualityAnalyzer
{
    private const float ExpectedComponentRange = 0.35f;

    public EmbeddingQualityAnalysis Analyze(float[] vector, EmbeddingValidationStatistics statistics)
    {
        var diagnostics = new List<string>();

        if (!statistics.IsNormalized)
        {
            diagnostics.Add($"Vector magnitude {statistics.Magnitude:F4} deviates from unit length.");
        }

        if (MathF.Abs(statistics.MaxValue) > ExpectedComponentRange ||
            MathF.Abs(statistics.MinValue) > ExpectedComponentRange)
        {
            diagnostics.Add(
                $"Component range [{statistics.MinValue:F4}, {statistics.MaxValue:F4}] exceeds expected ±{ExpectedComponentRange:F2}.");
        }

        var spread = statistics.MaxValue - statistics.MinValue;
        if (spread < 0.05f)
        {
            diagnostics.Add("Low component spread may indicate a collapsed embedding.");
        }

        var qualityScore = ComputeQualityScore(statistics, diagnostics.Count);
        var withinRange = statistics.IsNormalized &&
                          MathF.Abs(statistics.MaxValue) <= ExpectedComponentRange &&
                          MathF.Abs(statistics.MinValue) <= ExpectedComponentRange;

        return new EmbeddingQualityAnalysis
        {
            QualityScore = qualityScore,
            Diagnostics = diagnostics,
            IsWithinExpectedRange = withinRange,
        };
    }

    private static float ComputeQualityScore(EmbeddingValidationStatistics statistics, int diagnosticCount)
    {
        if (statistics.Dimension == 0)
        {
            return 0f;
        }

        var normalizationScore = statistics.IsNormalized ? 1f : MathF.Max(0f, 1f - MathF.Abs(statistics.Magnitude - 1f));
        var rangePenalty = MathF.Min(1f, diagnosticCount * 0.15f);
        return Math.Clamp(normalizationScore - rangePenalty, 0f, 1f);
    }
}
