using Abhyanvaya.Application.Enrollment.Validation;
using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Application.Enrollment.Storage;

/// <summary>Mutable state passed through the enrollment storage pipeline (AI20.PHASE2.1.5B).</summary>
public sealed class EnrollmentStoragePipelineContext
{
    public required EnrollmentStorageRequest Request { get; init; }
    public EnrollmentStoragePolicyDecision? Policy { get; set; }
    public IReadOnlyList<IEnrollmentArtifactTypeDefinition> EnabledTypes { get; set; } = [];
    public Guid StorageGroupId { get; set; }
    public Guid ManifestId { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public List<EnrollmentStoragePipelineArtifactItem> ArtifactItems { get; } = [];
    public List<string> UploadedKeys { get; } = [];
    public List<EnrollmentStoredArtifactEntry> StoredEntries { get; } = [];
    public List<EnrollmentStorageManifestEntry> ManifestEntries { get; } = [];
    public List<EnrollmentStorageRecord> PendingRecords { get; } = [];
    public EnrollmentStorageRecord? PrimaryRecord { get; set; }
    public EnrollmentStorageManifest? Manifest { get; set; }
    public bool Failed { get; set; }
    public string? FailureReason { get; set; }
}

public sealed class EnrollmentStoragePipelineArtifactItem
{
    public required IEnrollmentArtifactTypeDefinition TypeDefinition { get; init; }
    public EnrollmentArtifactPayload? Payload { get; set; }
    public string? Checksum { get; set; }
    public bool IsDuplicate { get; set; }
    public EnrollmentStorageRecord? ExistingRecord { get; set; }
    public int ArtifactVersion { get; set; }
    public string? ObjectKey { get; set; }
}
