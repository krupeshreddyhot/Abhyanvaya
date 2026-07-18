using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Entities;

public class ArtifactRegistryEntry
{
    public Guid Id { get; set; }

    public Guid EnrollmentId { get; set; }

    public Guid BatchId { get; set; }

    public Guid ManifestId { get; set; }

    public int TenantId { get; set; }

    public int StudentId { get; set; }

    public required string ArtifactType { get; set; }

    public ArtifactUploadState Status { get; set; } = ArtifactUploadState.Queued;

    public required string StorageProvider { get; set; }

    public required string Bucket { get; set; }

    public required string StorageKey { get; set; }

    public required string Checksum { get; set; }

    public long FileSize { get; set; }

    public int ArtifactVersion { get; set; }

    public int StorageVersion { get; set; } = 1;

    public string? VerificationResultJson { get; set; }

    public Guid CorrelationId { get; set; }

    public Guid TraceId { get; set; }

    public int RetryCount { get; set; }

    public string? FailureReason { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? VerifiedUtc { get; set; }

    public DateTime? ArchivedUtc { get; set; }
}

public class ArtifactStorageManifest
{
    public Guid Id { get; set; }

    public Guid BatchId { get; set; }

    public Guid EnrollmentId { get; set; }

    public int TenantId { get; set; }

    public required string ManifestJson { get; set; }

    public int ManifestVersion { get; set; }

    public ArtifactUploadState Status { get; set; } = ArtifactUploadState.Queued;

    public DateTime CreatedUtc { get; set; }

    public DateTime? VerifiedUtc { get; set; }
}
