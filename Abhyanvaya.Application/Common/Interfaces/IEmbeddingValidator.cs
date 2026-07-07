namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Validates embedding vectors before persistence.
/// </summary>
public interface IEmbeddingValidator
{
    /// <summary>
    /// Validates the vector; throws when validation fails.
    /// </summary>
    EmbeddingValidationResult Validate(float[] vector, int? expectedDimension = null);
}

/// <summary>Outcome of embedding vector validation.</summary>
public sealed record EmbeddingValidationResult(
    bool IsValid,
    int Dimension,
    string? FailureReason = null);
