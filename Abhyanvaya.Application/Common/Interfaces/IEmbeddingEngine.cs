namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Provider-agnostic embedding engine for enrollment. Abstracts InsightFace, ArcFace, AdaFace, FaceNet, etc.
/// </summary>
public interface IEmbeddingEngine
{
    string EngineName { get; }

    string ModelName { get; }

    string ModelVersion { get; }

    int ExpectedDimension { get; }

    string NormalizationMethod { get; }

    /// <summary>Generates a raw embedding vector from a pre-aligned face image stream.</summary>
    Task<EmbeddingEngineResult> GenerateFromAlignedFaceAsync(
        Stream alignedFaceStream,
        CancellationToken cancellationToken = default);
}

/// <summary>Raw engine output before enrollment validation/normalization pipeline steps.</summary>
public sealed record EmbeddingEngineResult(
    float[] EmbeddingVector,
    long InferenceMilliseconds);
