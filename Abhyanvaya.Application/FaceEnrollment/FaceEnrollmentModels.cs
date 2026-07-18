using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.FaceEnrollment;

public sealed record EnrollmentContext
{
    public required Guid EnrollmentId { get; init; }
    public required int StudentId { get; init; }
    public required Guid BatchId { get; init; }
    public required Guid PhotoId { get; init; }
    public required Guid CorrelationId { get; init; }
    public required Guid TraceId { get; init; }
    public required DateTime StartedTime { get; init; }
    public required string RecognitionConfigurationVersion { get; init; }
    public required string EnrollmentPolicyVersion { get; init; }
}

public sealed record EnrollmentArtifact
{
    public required int StudentId { get; init; }
    public required string PhotoReference { get; init; }
    public required string AlignedPhotoReference { get; init; }
    public required string EmbeddingReference { get; init; }
    public required int EmbeddingDimension { get; init; }
    public required string EmbeddingVersion { get; init; }
    public required decimal QualityScore { get; init; }
    public required Guid ManifestId { get; init; }
    public required string EnrollmentVersion { get; init; }
    public required DateTime CreatedUtc { get; init; }
    public required string Checksum { get; init; }
}

public sealed record EnrollmentStatistics
{
    public int Queued { get; init; }
    public int Completed { get; init; }
    public int Failed { get; init; }
    public int Duplicates { get; init; }
    public TimeSpan AverageDuration { get; init; }
    public decimal AverageQuality { get; init; }
    public TimeSpan AverageEmbeddingTime { get; init; }
    public int RetryCount { get; init; }
    public decimal SuccessRate { get; init; }
}

public sealed record FaceDetectionResult
{
    public required int FaceCount { get; init; }
    public required float TopConfidence { get; init; }
    public required int ImageWidth { get; init; }
    public required int ImageHeight { get; init; }
}

public sealed record FaceAlignmentResult
{
    public required bool Success { get; init; }
    public byte[]? AlignedFaceBytes { get; init; }
    public string? ContentType { get; init; }
    public string? FailureReason { get; init; }
}

public sealed record EnrollmentQualityResult
{
    public required bool Passed { get; init; }
    public required decimal QualityScore { get; init; }
    public IReadOnlyList<string>? Errors { get; init; }
}

public sealed record EnrollmentDuplicateResult
{
    public required bool IsDuplicate { get; init; }
    public string? DuplicateType { get; init; }
    public string? Detail { get; init; }
}

public sealed record EnrollmentManifestEntry
{
    public required Guid EnrollmentId { get; init; }
    public required int StudentId { get; init; }
    public required string StudentNumber { get; init; }
    public EnrollmentState FinalState { get; init; }
    public decimal? QualityScore { get; init; }
    public string? FailureReason { get; init; }
}

public sealed record EnrollmentManifest
{
    public required Guid BatchId { get; init; }
    public required Guid ManifestId { get; init; }
    public required IReadOnlyList<EnrollmentManifestEntry> SuccessList { get; init; }
    public required IReadOnlyList<EnrollmentManifestEntry> FailureList { get; init; }
    public required IReadOnlyList<EnrollmentManifestEntry> DuplicateList { get; init; }
    public required IReadOnlyList<EnrollmentManifestEntry> RetryList { get; init; }
    public DateTime GeneratedUtc { get; init; }
}

public sealed record EnrollmentProgressSnapshot
{
    public required Guid EnrollmentId { get; init; }
    public required EnrollmentState State { get; init; }
    public required decimal ProgressPercent { get; init; }
    public TimeSpan Duration { get; init; }
}

public sealed record EnrollmentBatchRequest
{
    public required Guid AcquisitionBatchId { get; init; }
    public required int TenantId { get; init; }
    public int? MaxParallelism { get; init; }
}

public sealed record EnrollmentBatchResult
{
    public required Guid BatchId { get; init; }
    public required EnrollmentManifest Manifest { get; init; }
    public required EnrollmentStatistics Statistics { get; init; }
}

public sealed record EnrollmentReport
{
    public required Guid BatchId { get; init; }
    public required EnrollmentManifest Manifest { get; init; }
    public required EnrollmentStatistics Statistics { get; init; }
    public IReadOnlyList<string>? FailureDetails { get; init; }
    public IReadOnlyList<string>? DuplicateDetails { get; init; }
}

public interface IEnrollmentPolicy
{
    int MinimumWidth { get; }
    int MinimumHeight { get; }
    decimal MinimumQualityScore { get; }
    decimal MaximumRotationDegrees { get; }
    decimal MinimumBrightness { get; }
    DuplicatePolicyMode DuplicatePolicy { get; }
    FaceCountPolicyMode FaceCountPolicy { get; }
    EmbeddingPolicyMode EmbeddingPolicy { get; }
    decimal MinimumDetectionConfidence { get; }
}

public enum DuplicatePolicyMode
{
    Reject = 0,
    AllowMetadataDuplicate = 1,
}

public enum FaceCountPolicyMode
{
    ExactlyOne = 0,
}

public enum EmbeddingPolicyMode
{
    RequiredNormalized = 0,
}

public sealed class EnrollmentPolicyOptions
{
    public const string SectionName = "FaceEnrollmentPolicy";

    public int MinimumWidth { get; set; } = 200;
    public int MinimumHeight { get; set; } = 200;
    public decimal MinimumQualityScore { get; set; } = 0.5m;
    public decimal MaximumRotationDegrees { get; set; } = 15m;
    public decimal MinimumBrightness { get; set; } = 0.2m;
    public DuplicatePolicyMode DuplicatePolicy { get; set; } = DuplicatePolicyMode.Reject;
    public FaceCountPolicyMode FaceCountPolicy { get; set; } = FaceCountPolicyMode.ExactlyOne;
    public EmbeddingPolicyMode EmbeddingPolicy { get; set; } = EmbeddingPolicyMode.RequiredNormalized;
    public decimal MinimumDetectionConfidence { get; set; } = 0.5m;
    public string RecognitionConfigurationVersion { get; set; } = "2.3";
    public string EnrollmentPolicyVersion { get; set; } = "1.0";
    public int MaxRetryAttempts { get; set; } = 3;
    public int MaxParallelism { get; set; } = 16;
}

public sealed class ConfigurableEnrollmentPolicy : IEnrollmentPolicy
{
    private readonly EnrollmentPolicyOptions _options;

    public ConfigurableEnrollmentPolicy(Microsoft.Extensions.Options.IOptions<EnrollmentPolicyOptions> options)
    {
        _options = options.Value;
    }

    public int MinimumWidth => _options.MinimumWidth;
    public int MinimumHeight => _options.MinimumHeight;
    public decimal MinimumQualityScore => _options.MinimumQualityScore;
    public decimal MaximumRotationDegrees => _options.MaximumRotationDegrees;
    public decimal MinimumBrightness => _options.MinimumBrightness;
    public DuplicatePolicyMode DuplicatePolicy => _options.DuplicatePolicy;
    public FaceCountPolicyMode FaceCountPolicy => _options.FaceCountPolicy;
    public EmbeddingPolicyMode EmbeddingPolicy => _options.EmbeddingPolicy;
    public decimal MinimumDetectionConfidence => _options.MinimumDetectionConfidence;
}
