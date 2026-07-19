namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Validates embedding vectors before persistence or enrollment artifact emission.
/// </summary>
public interface IEmbeddingValidator
{
    /// <summary>Validates dimension, numeric integrity, and non-zero magnitude.</summary>
    EmbeddingValidationResult Validate(float[] vector, int? expectedDimension = null);

    /// <summary>Validates a vector that must already be L2-normalized.</summary>
    EmbeddingValidationResult ValidateNormalized(float[] vector, int? expectedDimension = null);

    /// <summary>Computes descriptive statistics for diagnostics and quality analysis.</summary>
    EmbeddingValidationStatistics ComputeStatistics(float[] vector);
}

/// <summary>Outcome of embedding vector validation.</summary>
public sealed record EmbeddingValidationResult(
    bool IsValid,
    int Dimension,
    string? FailureReason = null);

/// <summary>Computed embedding vector statistics.</summary>
public sealed record EmbeddingValidationStatistics(
    int Dimension,
    float Magnitude,
    float MinValue,
    float MaxValue,
    float Mean,
    bool IsNormalized);
