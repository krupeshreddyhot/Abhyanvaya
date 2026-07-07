using Abhyanvaya.Application.Common.Interfaces;

namespace Abhyanvaya.Infrastructure.Embedding;

/// <summary>
/// Validates embedding vectors before normalization and storage.
/// </summary>
public sealed class EmbeddingValidator : IEmbeddingValidator
{
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

        for (var i = 0; i < vector.Length; i++)
        {
            if (float.IsNaN(vector[i]) || float.IsInfinity(vector[i]))
            {
                return new EmbeddingValidationResult(false, vector.Length, $"Embedding contains invalid value at index {i}.");
            }
        }

        return new EmbeddingValidationResult(true, vector.Length);
    }
}
