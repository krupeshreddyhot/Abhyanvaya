namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Produces advisory embedding quality diagnostics. Never rejects enrollment — analytics only.
/// </summary>
public interface IEmbeddingQualityAnalyzer
{
    EmbeddingQualityAnalysis Analyze(float[] vector, EmbeddingValidationStatistics statistics);
}

public sealed record EmbeddingQualityAnalysis
{
    public required float QualityScore { get; init; }

    public required IReadOnlyList<string> Diagnostics { get; init; }

    public required bool IsWithinExpectedRange { get; init; }
}
