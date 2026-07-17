namespace Abhyanvaya.Application.Enrollment.Embedding;

using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Storage;

public static class EnrollmentEmbeddingFailureCodes
{
    public const string ArtifactMissing = "artifact.missing";
    public const string UnsupportedArtifact = "artifact.unsupported";
    public const string EmbeddingFailure = "embedding.failure";
    public const string InvalidDimension = "embedding.invalid_dimension";
    public const string InvalidVector = "embedding.invalid_vector";
}

public sealed record EnrollmentEmbeddingRequest
{
    public required EnrollmentStorageManifest Manifest { get; init; }

    public required int StudentId { get; init; }

    public required Guid BatchId { get; init; }

    public string ArtifactType { get; init; } = EnrollmentArtifactTypeNames.AlignedFace;

    public Guid? CorrelationId { get; init; }

    public int? PipelineVersion { get; init; }
}

/// <summary>Immutable enrollment embedding output. Vector and metadata must not be mutated after creation.</summary>
public sealed record EnrollmentEmbeddingArtifact
{
    public required int StudentId { get; init; }

    public required Guid BatchId { get; init; }

    public required IReadOnlyList<float> EmbeddingVector { get; init; }

    public required int EmbeddingDimension { get; init; }

    public required string EmbeddingModel { get; init; }

    public required string EmbeddingModelVersion { get; init; }

    public required int PipelineVersion { get; init; }

    public required int ValidationVersion { get; init; }

    public required int StorageVersion { get; init; }

    public required int ArtifactVersion { get; init; }

    public required int ManifestVersion { get; init; }

    public required float QualityScore { get; init; }

    public required Guid CorrelationId { get; init; }

    public required TimeSpan EmbeddingDuration { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }
}

public sealed record EmbeddingMetadata
{
    public required string Model { get; init; }

    public required string ModelVersion { get; init; }

    public required int EmbeddingDimension { get; init; }

    public required string Normalization { get; init; }

    public string? FrameworkVersion { get; init; }

    public string? OnnxVersion { get; init; }

    public string? InferenceProvider { get; init; }

    public required string ExecutionDevice { get; init; }

    public required TimeSpan ExecutionTime { get; init; }
}

public sealed record EnrollmentEmbeddingTelemetry
{
    public required TimeSpan ResolveDuration { get; init; }

    public required TimeSpan InferenceDuration { get; init; }

    public required TimeSpan NormalizationDuration { get; init; }

    public required TimeSpan ValidationDuration { get; init; }

    public required TimeSpan TotalDuration { get; init; }
}

public sealed record EnrollmentEmbeddingResult
{
    public required bool Success { get; init; }

    public EnrollmentEmbeddingArtifact? Artifact { get; init; }

    public EmbeddingMetadata? Metadata { get; init; }

    public EmbeddingValidationStatistics? Statistics { get; init; }

    public IReadOnlyList<string>? Warnings { get; init; }

    public EnrollmentEmbeddingTelemetry? Telemetry { get; init; }

    public string? FailureCode { get; init; }

    public string? FailureReason { get; init; }

    public static EnrollmentEmbeddingResult Succeeded(
        EnrollmentEmbeddingArtifact artifact,
        EmbeddingMetadata metadata,
        EmbeddingValidationStatistics statistics,
        IReadOnlyList<string> warnings,
        EnrollmentEmbeddingTelemetry telemetry) =>
        new()
        {
            Success = true,
            Artifact = artifact,
            Metadata = metadata,
            Statistics = statistics,
            Warnings = warnings,
            Telemetry = telemetry,
        };

    public static EnrollmentEmbeddingResult Failed(string code, string reason) =>
        new()
        {
            Success = false,
            FailureCode = code,
            FailureReason = reason,
        };
}
