using Abhyanvaya.Application.Common.Interfaces;

namespace Abhyanvaya.Infrastructure.Embedding;

/// <summary>
/// L2-normalizes embedding vectors and validates the resulting magnitude.
/// </summary>
public sealed class EmbeddingNormalizer : IEmbeddingNormalizer
{
    private const float MagnitudeTolerance = 0.01f;

    public float[] Normalize(float[] vector)
    {
        if (vector == null || vector.Length == 0)
        {
            throw new InvalidOperationException("Cannot normalize an empty embedding vector.");
        }

        var magnitude = 0f;
        for (var i = 0; i < vector.Length; i++)
        {
            magnitude += vector[i] * vector[i];
        }

        magnitude = MathF.Sqrt(magnitude);
        if (magnitude <= float.Epsilon)
        {
            throw new InvalidOperationException("Cannot normalize a zero-magnitude embedding vector.");
        }

        var normalized = new float[vector.Length];
        for (var i = 0; i < vector.Length; i++)
        {
            normalized[i] = vector[i] / magnitude;
        }

        ValidateMagnitude(normalized);
        return normalized;
    }

    private static void ValidateMagnitude(float[] vector)
    {
        var magnitude = 0f;
        for (var i = 0; i < vector.Length; i++)
        {
            magnitude += vector[i] * vector[i];
        }

        magnitude = MathF.Sqrt(magnitude);
        if (MathF.Abs(magnitude - 1f) > MagnitudeTolerance)
        {
            throw new InvalidOperationException(
                $"Normalized embedding magnitude {magnitude:F4} is outside acceptable tolerance.");
        }
    }
}
