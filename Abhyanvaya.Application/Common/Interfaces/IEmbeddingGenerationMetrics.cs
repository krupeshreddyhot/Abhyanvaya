namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// In-process metrics for embedding generation (OpenTelemetry-ready).
/// </summary>
public interface IEmbeddingGenerationMetrics
{
    void RecordSuccess(
        string provider,
        string model,
        int embeddingDimension,
        long generationDurationMs,
        long normalizationDurationMs,
        long validationDurationMs,
        int retryCount);

    void RecordFailure(string provider, string? model, int retryCount);

    EmbeddingMetricsSnapshot GetSnapshot();
}

/// <summary>Point-in-time embedding generation metrics.</summary>
public sealed record EmbeddingMetricsSnapshot(
    long SuccessfulEmbeddings,
    long FailedEmbeddings,
    double AverageGenerationTimeMs,
    double AverageRetries);
