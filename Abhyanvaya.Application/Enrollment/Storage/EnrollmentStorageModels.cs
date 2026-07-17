using Abhyanvaya.Application.Enrollment.Validation;

namespace Abhyanvaya.Application.Enrollment.Storage;

public static class EnrollmentArtifactTypeNames
{
    public const string AlignedFace = "AlignedFace";
    public const string ValidationReport = "ValidationReport";
    public const string Thumbnail = "Thumbnail";
    public const string DiagnosticImage = "DiagnosticImage";
    public const string Embedding = "Embedding";
    public const string AuditImage = "AuditImage";
    public const string PassportCrop = "PassportCrop";
}

public static class EnrollmentStorageVersions
{
    public const int StorageSchemaVersion = 1;
    public const int ValidationSchemaVersion = 1;
}

/// <summary>Extracts artifact bytes and metadata from a validation artifact for storage.</summary>
public interface IEnrollmentArtifactTypeDefinition
{
    string ArtifactType { get; }

    bool EnabledByDefault { get; }

    string FileExtension { get; }

    string ContentType { get; }

    bool IsPrimary { get; }

    Task<EnrollmentArtifactPayload?> TryCreatePayloadAsync(
        EnrollmentValidationArtifact artifact,
        CancellationToken cancellationToken = default);
}

public sealed record EnrollmentArtifactPayload
{
    public required byte[] Bytes { get; init; }
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }
}

public sealed record EnrollmentStorageRequest
{
    public required int TenantId { get; init; }
    public required int CollegeId { get; init; }
    public required int AcademicYear { get; init; }
    public required int StudentId { get; init; }
    public required Guid BatchId { get; init; }
    public required Guid ItemId { get; init; }
    public required int PipelineVersion { get; init; }
    public required EnrollmentValidationArtifact Artifact { get; init; }
    public required Guid ExecutionTraceId { get; init; }
    public ValidationProfileKind? ValidationProfile { get; init; }
}

public sealed record EnrollmentStorageResult
{
    public required bool Success { get; init; }
    public Guid? StorageRecordId { get; init; }
    public string? StorageProvider { get; init; }
    public string? StoragePath { get; init; }
    public int StorageVersion { get; init; }
    public string? Checksum { get; init; }
    public long? FileSize { get; init; }
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }
    public string? ContentType { get; init; }
    public required TimeSpan Duration { get; init; }
    public string? FailureReason { get; init; }
    public EnrollmentStorageManifest? Manifest { get; init; }

    /// <summary>Canonical student photo base path for downstream writers (engine contract compatibility).</summary>
    public string? PhotoKey { get; init; }

    public IReadOnlyList<EnrollmentStoredArtifactEntry>? Artifacts { get; init; }
}

public sealed record EnrollmentStoredArtifactEntry
{
    public required Guid ArtifactId { get; init; }
    public required string ArtifactType { get; init; }
    public required string ObjectKey { get; init; }
    public required string Checksum { get; init; }
    public required int ArtifactVersion { get; init; }
    public required long FileSize { get; init; }
    public string? ContentType { get; init; }
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }
    public required bool Persisted { get; init; }
    public bool IsDuplicate { get; init; }
    public string? FailureReason { get; init; }
}

public sealed record EnrollmentStorageManifest
{
    public required Guid ManifestId { get; init; }
    public required Guid StorageGroupId { get; init; }
    public required IReadOnlyList<EnrollmentStorageManifestEntry> Entries { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public required int PipelineVersion { get; init; }
    public required int ValidationVersion { get; init; }
    public string? ValidationProfile { get; init; }
    public required Guid CorrelationId { get; init; }
}

public sealed record EnrollmentStorageManifestEntry
{
    public required Guid ArtifactId { get; init; }
    public required string ArtifactType { get; init; }
    public required string StorageProvider { get; init; }
    public required string ObjectKey { get; init; }
    public required string Checksum { get; init; }
    public required int Version { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public required int PipelineVersion { get; init; }
    public string? ValidationProfile { get; init; }
    public EnrollmentStorageImageMetadata? ImageMetadata { get; init; }
    public required string ContentType { get; init; }
}

public sealed record EnrollmentStorageImageMetadata
{
    public int? Width { get; init; }
    public int? Height { get; init; }
    public long? FileSize { get; init; }
}

public sealed record EnrollmentStoragePolicyRequest
{
    public required int TenantId { get; init; }
    public int? CollegeId { get; init; }
    public int? ProgramId { get; init; }
    public string? ExamCode { get; init; }
    public string? AdmissionType { get; init; }
}

public sealed record EnrollmentStoragePolicyDecision
{
    public required IReadOnlySet<string> EnabledArtifactTypes { get; init; }
    public int RetentionDays { get; init; } = 365;
    public bool EnableCompression { get; init; }
    public bool EnableEncryption { get; init; }
    public string? PreferredProvider { get; init; }
    public string StorageTier { get; init; } = "standard";
}

public sealed record EnrollmentStoragePathContext
{
    public required int TenantId { get; init; }
    public required int CollegeId { get; init; }
    public required int AcademicYear { get; init; }
    public required int StudentId { get; init; }
    public required int PipelineVersion { get; init; }
    public required string ArtifactType { get; init; }
    public required int ArtifactVersion { get; init; }
    public required string FileExtension { get; init; }
}
