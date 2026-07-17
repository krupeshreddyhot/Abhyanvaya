using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Entities;

/// <summary>
/// One student's AI enrollment attempt within a <see cref="StudentEnrollmentBatch"/>. Tracks the full
/// pipeline (download → validate → upload → embed) independently of <see cref="StudentFaceEmbedding"/>,
/// which continues to track only the current active embedding's own lifecycle for manual photo uploads.
/// See docs/AI20_ENROLLMENT_DATABASE.md (§3.2) — this entity was named <c>StudentEnrollmentJob</c> in the
/// architecture design and renamed to <see cref="StudentEnrollmentItem"/> during implementation
/// (AI20.IMPLEMENT.2) to avoid confusion with the batch itself being "the job."
/// </summary>
/// <remarks>
/// Deliberately implements <see cref="ITenantScoped"/> (not <see cref="BaseEntity"/>), mirroring
/// <see cref="AttendanceRecognition"/> and <see cref="StudentFaceEmbedding"/> — a <see cref="Guid"/> key,
/// no soft delete (rows are immutable historical facts once terminal), and a manually-managed
/// <see cref="RowVersion"/> optimistic concurrency token used by the durable job-queue claim protocol
/// (docs/AI20_ENROLLMENT_BACKGROUND.md §3.1).
/// </remarks>
public class StudentEnrollmentItem : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>Denormalized from the batch for direct tenant-scoped queries.</summary>
    public int TenantId { get; set; }

    public Guid BatchId { get; set; }

    public int StudentId { get; set; }

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Pending;

    /// <summary>Populated only when <see cref="Status"/> is <see cref="EnrollmentStatus.Failed"/> or <see cref="EnrollmentStatus.RetryRequired"/>.</summary>
    public FailureCategory? FailureCategory { get; set; }

    // ---- Photo metadata ----

    /// <summary>Fully resolved download URL actually requested (audit trail — the base URL template is configurable).</summary>
    public required string SourceUrl { get; set; }

    /// <summary>Set once uploaded — matches <see cref="Student.PhotoKey"/> on success.</summary>
    public string? PhotoKey { get; set; }

    public string? ContentType { get; set; }

    public int? ByteSize { get; set; }

    /// <summary>SHA-256 hex digest of the downloaded bytes; used for duplicate-detection/idempotency.</summary>
    public string? Checksum { get; set; }

    public int? ImageWidth { get; set; }

    public int? ImageHeight { get; set; }

    // ---- Embedding metadata ----

    /// <summary>Copy of the value written to the resulting <see cref="StudentFaceEmbedding.EmbeddingVersion"/> on success.</summary>
    public string? EmbeddingVersion { get; set; }

    /// <summary>Composite 0.0–1.0 quality score from validation (detection confidence × resolution × sharpness × pose). Advisory only — never causes rejection by itself.</summary>
    public float? QualityScore { get; set; }

    /// <summary>Direct traceability to the embedding row this item produced.</summary>
    public Guid? StudentFaceEmbeddingId { get; set; }

    // ---- Retry / failure ----

    /// <summary>Automatic-retry attempts consumed (transient failures only).</summary>
    public int RetryCount { get; set; }

    public string? LastError { get; set; }

    /// <summary>Updated on every transition, including retries — drives the stuck-item recovery sweep's staleness check.</summary>
    public DateTime? LastAttemptUtc { get; set; }

    /// <summary>When set, item is not claimable until this UTC time (scheduler-owned retry timing).</summary>
    public DateTime? NextAttemptUtc { get; set; }

    // ---- Stage timestamps (per-student timeline for the Student Detail Screen) ----

    public DateTime CreatedUtc { get; set; }

    public DateTime? DownloadStartedUtc { get; set; }

    public DateTime? DownloadedUtc { get; set; }

    public DateTime? ValidationStartedUtc { get; set; }

    public DateTime? ValidatedUtc { get; set; }

    public DateTime? EmbeddingStartedUtc { get; set; }

    public DateTime? CompletedUtc { get; set; }

    /// <summary>Optimistic concurrency token used by the claim protocol (docs/AI20_ENROLLMENT_BACKGROUND.md §3.1).</summary>
    public byte[] RowVersion { get; set; } = null!;

    public StudentEnrollmentBatch Batch { get; set; } = null!;

    public Student Student { get; set; } = null!;

    public StudentFaceEmbedding? StudentFaceEmbedding { get; set; }
}
