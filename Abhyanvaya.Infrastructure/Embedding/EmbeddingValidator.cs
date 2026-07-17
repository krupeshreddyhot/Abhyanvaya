using Abhyanvaya.Application.Common.Interfaces;

namespace Abhyanvaya.Infrastructure.Embedding;

/// <summary>
/// Validates embedding vectors before normalization and storage.
/// </summary>
public sealed class EmbeddingValidator : IEmbeddingValidator
{
    private const float NormalizationTolerance = 0.01f;

    public EmbeddingValidationResult Validate(float[] vector, int? expectedDimension = null)
    {
        if (vector == null || vector.Length == 0)
        {
            return new EmbeddingValidationResult(false, 0, "Embedding vector is empty.");
        }

        if (expectedDimension.HasValue && vector.Length != expectedDimension.Value)
        {
            return new EmbeddingValidationResult(
                false,
                vector.Length,
                $"Embedding dimension {vector.Length} does not match expected {expectedDimension.Value}.");
        }

        var magnitudeSquared = 0f;
        for (var i = 0; i < vector.Length; i++)
        {
            if (float.IsNaN(vector[i]) || float.IsInfinity(vector[i]))
            {
                return new EmbeddingValidationResult(false, vector.Length, $"Embedding contains invalid value at index {i}.");
            }

            magnitudeSquared += vector[i] * vector[i];
        }

        if (magnitudeSquared <= float.Epsilon)
        {
            return new EmbeddingValidationResult(false, vector.Length, "Embedding vector has zero magnitude.");
        }

        return new EmbeddingValidationResult(true, vector.Length);
    }

    public EmbeddingValidationResult ValidateNormalized(float[] vector, int? expectedDimension = null)
    {
        var baseResult = Validate(vector, expectedDimension);
        if (!baseResult.IsValid)
        {
            return baseResult;
        }

        var statistics = ComputeStatistics(vector);
        if (!statistics.IsNormalized)
        {
            return new EmbeddingValidationResult(
                false,
                vector.Length,
                $"Embedding magnitude {statistics.Magnitude:F4} is not normalized.");
        }

        return baseResult;
    }

    public EmbeddingValidationStatistics ComputeStatistics(float[] vector)
    {
        if (vector == null || vector.Length == 0)
        {
            return new EmbeddingValidationStatistics(0, 0f, 0f, 0f, 0f, false);
        }

        var min = float.MaxValue;
        var max = float.MinValue;
        var sum = 0f;
        var magnitudeSquared = 0f;

        for (var i = 0; i < vector.Length; i++)
        {
            var value = vector[i];
            min = MathF.Min(min, value);
            max = MathF.Max(max, value);
            sum += value;
            magnitudeSquared += value * value;
        }

        var magnitude = MathF.Sqrt(magnitudeSquared);
        var mean = sum / vector.Length;
        var isNormalized = MathF.Abs(magnitude - 1f) <= NormalizationTolerance;

        return new EmbeddingValidationStatistics(vector.Length, magnitude, min, max, mean, isNormalized);
    }
}
