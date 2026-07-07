namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Abstraction for face-embedding providers (InsightFace, FaceNet, Azure Face, OpenCV, etc.).
/// Implementations are registered per provider in future AI phases.
/// </summary>
public interface IEmbeddingGenerator
{
    /// <summary>Provider identifier (see <see cref="Domain.Constants.EmbeddingProviders"/>).</summary>
    string ProviderName { get; }

    /// <summary>Model name or deployment id used for embedding generation.</summary>
    string ModelName { get; }

    /// <summary>Provider-specific model or pipeline version.</summary>
    string Version { get; }

    /// <summary>
    /// Generates a face embedding vector from the student photo at <paramref name="photoKey"/>.
    /// </summary>
    Task<EmbeddingGenerationResult> GenerateAsync(
        EmbeddingGenerationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Input for embedding generation from a stored student photo.</summary>
public sealed record EmbeddingGenerationRequest(
    int TenantId,
    int StudentId,
    string PhotoKey);

/// <summary>Output from a face-embedding provider.</summary>
public sealed record EmbeddingGenerationResult(
    float[] EmbeddingVector,
    string Model,
    string Version,
    Domain.Enums.EmbeddingQuality Quality,
    int? ExpectedDimension = null);
