using Abhyanvaya.Application.FaceEnrollment;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.ArtifactStorage;

public sealed record ArtifactStorageContext
{
    public required Guid ArtifactId { get; init; }
    public required Guid EnrollmentId { get; init; }
    public required int StudentId { get; init; }
    public required Guid BatchId { get; init; }
    public required string StorageProvider { get; init; }
    public required string Bucket { get; init; }
    public required string StorageKey { get; init; }
    public required string Checksum { get; init; }
    public required Guid CorrelationId { get; init; }
    public required Guid TraceId { get; init; }
    public required DateTime CreatedUtc { get; init; }
}

public sealed record ArtifactMetadata
{
    public required string ArtifactType { get; init; }
    public required string ContentType { get; init; }
    public required long FileSize { get; init; }
    public required string Checksum { get; init; }
    public bool Compression { get; init; }
    public required string Version { get; init; }
    public required DateTime CreatedUtc { get; init; }
    public required string RetentionPolicy { get; init; }
    public required string StorageClass { get; init; }
}

public sealed record ArtifactStorageStatistics
{
    public int Uploaded { get; init; }
    public int Verified { get; init; }
    public int Failed { get; init; }
    public int Archived { get; init; }
    public int Deleted { get; init; }
    public int RetryCount { get; init; }
    public TimeSpan AverageUploadTime { get; init; }
    public long AverageFileSize { get; init; }
    public long StorageUsed { get; init; }
    public decimal CompressionRatio { get; init; }
}

public sealed record ArtifactUploadRequest
{
    public required EnrollmentArtifact Artifact { get; init; }
    public required Guid EnrollmentId { get; init; }
    public required Guid BatchId { get; init; }
    public required Guid PhotoId { get; init; }
    public required int TenantId { get; init; }
    public required Guid CorrelationId { get; init; }
    public required Guid TraceId { get; init; }
    public byte[]? OriginalPhotoBytes { get; init; }
    public required byte[] AlignedFaceBytes { get; init; }
    public required float[] Embedding { get; init; }
    public string? OriginalContentType { get; init; }
}

public sealed record ArtifactUploadItem
{
    public required string ArtifactType { get; init; }
    public required string ContentType { get; init; }
    public required byte[] Content { get; init; }
    public required string Checksum { get; init; }
    public required string Version { get; init; }
}

public sealed record ArtifactUploadResult
{
    public required Guid ArtifactId { get; init; }
    public required string StorageKey { get; init; }
    public required string Checksum { get; init; }
    public required long FileSize { get; init; }
    public required bool Verified { get; init; }
    public ArtifactUploadState FinalState { get; init; }
    public string? FailureReason { get; init; }
}

public sealed record ArtifactVerificationResult
{
    public required bool Passed { get; init; }
    public IReadOnlyList<string>? Errors { get; init; }
}

public sealed record ArtifactBatchUploadResult
{
    public required Guid EnrollmentId { get; init; }
    public required IReadOnlyList<ArtifactUploadResult> Results { get; init; }
    public required ArtifactStorageStatistics Statistics { get; init; }
}

public sealed record ArtifactUploadReport
{
    public required Guid BatchId { get; init; }
    public required ArtifactStorageStatistics Statistics { get; init; }
    public IReadOnlyList<string>? Failures { get; init; }
}

public sealed record ArtifactVerificationReport
{
    public required Guid BatchId { get; init; }
    public int Passed { get; init; }
    public int Failed { get; init; }
    public IReadOnlyList<string>? Failures { get; init; }
}

public sealed record ArtifactLifecycleReport
{
    public int Archived { get; init; }
    public int Deleted { get; init; }
    public int Retained { get; init; }
    public long StorageUsed { get; init; }
}

public enum ArtifactRetentionMode
{
    KeepForever = 0,
    ArchiveAfterDays = 1,
    DeleteAfterDays = 2,
}

public enum ArtifactCompressionMode
{
    None = 0,
    Gzip = 1,
}

public sealed class ArtifactStorageOptions
{
    public const string SectionName = "ArtifactStorage";

    public string Provider { get; set; } = "r2";
    /// <summary>Root directory for <c>Provider=local</c>. Relative paths resolve under the host content root.</summary>
    public string PhysicalRoot { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = "artifacts";
    public int MultipartThresholdBytes { get; set; } = 5 * 1024 * 1024;
    public int PartSizeBytes { get; set; } = 5 * 1024 * 1024;
    public bool EnableCompression { get; set; }
    public int MaxParallelUploads { get; set; } = 16;
    public string StorageClass { get; set; } = "STANDARD";
    public string RecognitionVersion { get; set; } = "2.3";
    public string EnrollmentVersion { get; set; } = "1.0";
    public string ManifestVersion { get; set; } = "1.0";
    public int RetentionVersion { get; set; } = 1;
}

public sealed class ArtifactVerificationPolicyOptions
{
    public const string SectionName = "ArtifactVerificationPolicy";

    public bool ChecksumValidation { get; set; } = true;
    public bool MetadataValidation { get; set; } = true;
    public bool ContentLengthValidation { get; set; } = true;
    public bool ManifestValidation { get; set; } = true;
    public bool VersionValidation { get; set; } = true;
}

public sealed class ArtifactRetryPolicyOptions
{
    public const string SectionName = "ArtifactRetryPolicy";

    public int MaximumRetries { get; set; } = 5;
    public int InitialDelayMilliseconds { get; set; } = 500;
    public double BackoffMultiplier { get; set; } = 2.0;
    public int MaxDelayMilliseconds { get; set; } = 30_000;
    public int UploadTimeoutSeconds { get; set; } = 120;
}

public sealed class ArtifactRetentionPolicyOptions
{
    public const string SectionName = "ArtifactRetentionPolicy";

    public ArtifactRetentionMode Mode { get; set; } = ArtifactRetentionMode.KeepForever;
    public int ArchiveAfterDays { get; set; } = 365;
    public int DeleteAfterDays { get; set; } = 730;
    public bool EnableVersionCleanup { get; set; }
    public int VersionCleanupAfterDays { get; set; } = 90;
}

public sealed class R2StorageOptions
{
    public const string SectionName = "ArtifactStorage:R2";

    public string Bucket { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string Region { get; set; } = "auto";
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public bool ForcePathStyle { get; set; } = true;
}

public interface IArtifactVerificationPolicy
{
    bool ChecksumValidation { get; }
    bool MetadataValidation { get; }
    bool ContentLengthValidation { get; }
    bool ManifestValidation { get; }
    bool VersionValidation { get; }
}

public interface IArtifactRetryPolicy
{
    int MaximumRetries { get; }
    TimeSpan GetDelay(int attempt);
    bool ShouldRetry(Exception exception, int attempt);
    TimeSpan UploadTimeout { get; }
}

public interface IArtifactRetentionPolicy
{
    ArtifactRetentionMode Mode { get; }
    int ArchiveAfterDays { get; }
    int DeleteAfterDays { get; }
    bool EnableVersionCleanup { get; }
    int VersionCleanupAfterDays { get; }
    bool ShouldArchive(DateTime createdUtc);
    bool ShouldDelete(DateTime createdUtc);
}

public sealed class ConfigurableArtifactVerificationPolicy : IArtifactVerificationPolicy
{
    private readonly ArtifactVerificationPolicyOptions _options;

    public ConfigurableArtifactVerificationPolicy(Microsoft.Extensions.Options.IOptions<ArtifactVerificationPolicyOptions> options)
    {
        _options = options.Value;
    }

    public bool ChecksumValidation => _options.ChecksumValidation;
    public bool MetadataValidation => _options.MetadataValidation;
    public bool ContentLengthValidation => _options.ContentLengthValidation;
    public bool ManifestValidation => _options.ManifestValidation;
    public bool VersionValidation => _options.VersionValidation;
}

public sealed class ConfigurableArtifactRetryPolicy : IArtifactRetryPolicy
{
    private readonly ArtifactRetryPolicyOptions _options;

    public ConfigurableArtifactRetryPolicy(Microsoft.Extensions.Options.IOptions<ArtifactRetryPolicyOptions> options)
    {
        _options = options.Value;
    }

    public int MaximumRetries => _options.MaximumRetries;
    public TimeSpan UploadTimeout => TimeSpan.FromSeconds(Math.Max(1, _options.UploadTimeoutSeconds));

    public TimeSpan GetDelay(int attempt)
    {
        var delay = _options.InitialDelayMilliseconds * Math.Pow(_options.BackoffMultiplier, Math.Max(0, attempt - 1));
        return TimeSpan.FromMilliseconds(Math.Min(delay, _options.MaxDelayMilliseconds));
    }

    public bool ShouldRetry(Exception exception, int attempt)
    {
        if (attempt >= MaximumRetries)
        {
            return false;
        }

        return exception is not OperationCanceledException;
    }
}

public sealed class ConfigurableArtifactRetentionPolicy : IArtifactRetentionPolicy
{
    private readonly ArtifactRetentionPolicyOptions _options;

    public ConfigurableArtifactRetentionPolicy(Microsoft.Extensions.Options.IOptions<ArtifactRetentionPolicyOptions> options)
    {
        _options = options.Value;
    }

    public ArtifactRetentionMode Mode => _options.Mode;
    public int ArchiveAfterDays => _options.ArchiveAfterDays;
    public int DeleteAfterDays => _options.DeleteAfterDays;
    public bool EnableVersionCleanup => _options.EnableVersionCleanup;
    public int VersionCleanupAfterDays => _options.VersionCleanupAfterDays;

    public bool ShouldArchive(DateTime createdUtc) =>
        Mode == ArtifactRetentionMode.ArchiveAfterDays &&
        DateTime.UtcNow - createdUtc >= TimeSpan.FromDays(ArchiveAfterDays);

    public bool ShouldDelete(DateTime createdUtc) =>
        Mode == ArtifactRetentionMode.DeleteAfterDays &&
        DateTime.UtcNow - createdUtc >= TimeSpan.FromDays(DeleteAfterDays);
}
