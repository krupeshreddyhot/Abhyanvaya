using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities;

/// <summary>
/// Append-only metadata for a persisted enrollment artifact (AI20.PHASE2.1.5).
/// Binary content lives in object storage; this row is the immutable record of what was stored.
/// </summary>
public class EnrollmentStorageRecord : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid StorageGroupId { get; set; }

    public int TenantId { get; set; }

    public int CollegeId { get; set; }

    public int AcademicYear { get; set; }

    public int StudentId { get; set; }

    public Guid BatchId { get; set; }

    public Guid ItemId { get; set; }

    public required string ArtifactType { get; set; }

    public required string ObjectKey { get; set; }

    public required string StorageProvider { get; set; }

    public required string Checksum { get; set; }

    public required string ContentType { get; set; }

    public long FileSize { get; set; }

    public int? ImageWidth { get; set; }

    public int? ImageHeight { get; set; }

    public int ArtifactVersion { get; set; }

    public int StorageVersion { get; set; } = 1;

    public int PipelineVersion { get; set; }

    public int ValidationVersion { get; set; } = 1;

    public string? ValidationProfile { get; set; }

    public Guid CorrelationId { get; set; }

    public bool IsPrimary { get; set; }

    public DateTime CreatedUtc { get; set; }
}
