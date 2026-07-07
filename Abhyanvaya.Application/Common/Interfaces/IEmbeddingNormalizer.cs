namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Normalizes embedding vectors (L2) before persistence.
/// </summary>
public interface IEmbeddingNormalizer
{
    /// <summary>L2-normalizes the vector and validates the resulting magnitude.</summary>
    float[] Normalize(float[] vector);
}
