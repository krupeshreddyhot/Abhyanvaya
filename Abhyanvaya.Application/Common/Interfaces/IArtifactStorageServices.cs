using Abhyanvaya.Application.ArtifactStorage;

namespace Abhyanvaya.Application.Common.Interfaces;

public interface IArtifactUploadCoordinator
{
    Task<ArtifactBatchUploadResult> ProcessQueuedItemAsync(ArtifactUploadRequest request, CancellationToken cancellationToken = default);
    Task RunContinuousAsync(CancellationToken cancellationToken = default);
}

public interface IArtifactStorageProvider
{
    string ProviderName { get; }
    string Bucket { get; }

    Task UploadAsync(
        string storageKey,
        Stream content,
        ArtifactMetadata metadata,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyExistsAsync(string storageKey, long expectedLength, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);

    Task ArchiveAsync(string storageKey, CancellationToken cancellationToken = default);
}

public interface IR2StorageProvider : IArtifactStorageProvider;

public interface IArtifactUploadService
{
    Task<ArtifactUploadResult> UploadItemAsync(
        ArtifactStorageContext context,
        ArtifactUploadItem item,
        CancellationToken cancellationToken = default);
}

public interface IArtifactVerificationService
{
    Task<ArtifactVerificationResult> VerifyAsync(
        ArtifactStorageContext context,
        ArtifactMetadata metadata,
        byte[] sourceContent,
        CancellationToken cancellationToken = default);
}

public interface IArtifactManifestRepository
{
    Task SaveManifestAsync(ArtifactStorageManifestRecord record, CancellationToken cancellationToken = default);
    Task<ArtifactStorageManifestRecord?> GetManifestAsync(Guid manifestId, CancellationToken cancellationToken = default);
    Task UpdateManifestStatusAsync(Guid manifestId, Domain.Enums.ArtifactUploadState status, CancellationToken cancellationToken = default);
}

public sealed record ArtifactStorageManifestRecord
{
    public required Guid Id { get; init; }
    public required Guid BatchId { get; init; }
    public required Guid EnrollmentId { get; init; }
    public required int TenantId { get; init; }
    public required string ManifestJson { get; init; }
    public required int ManifestVersion { get; init; }
    public Domain.Enums.ArtifactUploadState Status { get; init; }
    public DateTime CreatedUtc { get; init; }
}

public interface IArtifactVersionManager
{
    int AssignArtifactVersion(string artifactType, int studentId);
    string ResolveEmbeddingVersion(string configuredVersion);
    string ResolveRecognitionVersion(string configuredVersion);
    string ResolveEnrollmentVersion(string configuredVersion);
    int ResolveManifestVersion(string configuredVersion);
    int ResolveRetentionVersion(int configuredVersion);
}

public interface IArtifactLifecycleManager
{
    Task ApplyRetentionAsync(CancellationToken cancellationToken = default);
    Task ArchiveEligibleAsync(CancellationToken cancellationToken = default);
    Task DeleteEligibleAsync(CancellationToken cancellationToken = default);
}

public interface IArtifactIntegrityService
{
    string ComputeSha256(byte[] content);
    string ComputeSha256(Stream content);
    bool ValidateChecksum(string expected, string actual);
}

public interface IArtifactReportService
{
    Task<ArtifactUploadReport> GenerateUploadReportAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<ArtifactVerificationReport> GenerateVerificationReportAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<ArtifactStorageStatistics> GenerateStorageStatisticsAsync(Guid? batchId = null, CancellationToken cancellationToken = default);
    Task<ArtifactLifecycleReport> GenerateLifecycleReportAsync(CancellationToken cancellationToken = default);
}

public interface IArtifactRegistryRepository
{
    Task SaveAsync(ArtifactRegistryRecord record, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(Guid artifactId, Domain.Enums.ArtifactUploadState status, string? verificationJson = null, string? failureReason = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArtifactRegistryRecord>> GetByBatchAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArtifactRegistryRecord>> GetEligibleForArchiveAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArtifactRegistryRecord>> GetEligibleForDeleteAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
}

public sealed record ArtifactRegistryRecord
{
    public required Guid Id { get; init; }
    public required Guid EnrollmentId { get; init; }
    public required Guid BatchId { get; init; }
    public required Guid ManifestId { get; init; }
    public required int TenantId { get; init; }
    public required int StudentId { get; init; }
    public required string ArtifactType { get; init; }
    public Domain.Enums.ArtifactUploadState Status { get; init; }
    public required string StorageProvider { get; init; }
    public required string Bucket { get; init; }
    public required string StorageKey { get; init; }
    public required string Checksum { get; init; }
    public long FileSize { get; init; }
    public int ArtifactVersion { get; init; }
    public int StorageVersion { get; init; }
    public Guid CorrelationId { get; init; }
    public Guid TraceId { get; init; }
    public int RetryCount { get; init; }
    public DateTime CreatedUtc { get; init; }
}
