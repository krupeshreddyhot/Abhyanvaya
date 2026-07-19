using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Entities;

/// <summary>
/// One SuperAdmin-initiated bulk AI enrollment run, scoped to a University + College + Academic Year.
/// Mirrors the <see cref="AttendanceSession"/> ↔ <see cref="AttendanceRecognition"/> "job/result" shape:
/// this is the job, <see cref="StudentEnrollmentItem"/> is the per-student result row.
/// See docs/AI20_ENROLLMENT_DATABASE.md (§3.1) and docs/AI20_ENROLLMENT_ARCHITECTURE.md (§6).
/// </summary>
/// <remarks>
/// Like <see cref="AttendanceSession"/>, this type does not use <c>IsDeleted</c> soft-delete — a batch is
/// historical audit data with no delete feature; only <see cref="BatchStatus.Cancelled"/> represents a
/// stopped batch. Tenant isolation is enforced via <see cref="ITenantScoped"/>.
/// </remarks>
public class StudentEnrollmentBatch : ITenantScoped
{
    public Guid Id { get; set; }

    public int TenantId { get; set; }

    /// <summary>Denormalized alongside <see cref="CollegeId"/> for filter/reporting without a join.</summary>
    public int UniversityId { get; set; }

    public int CollegeId { get; set; }

    /// <summary>The <c>{year}</c> segment used to build every item's source photo URL.</summary>
    public int AcademicYear { get; set; }

    public BatchStatus Status { get; set; } = BatchStatus.Created;

    /// <summary>Snapshot count of items created for this batch at creation time.</summary>
    public int TotalStudents { get; set; }

    public int PendingCount { get; set; }

    public int DownloadingCount { get; set; }

    public int ValidatingCount { get; set; }

    public int EmbeddingCount { get; set; }

    public int CompletedCount { get; set; }

    public int FailedCount { get; set; }

    public int RetryRequiredCount { get; set; }

    public int CancelledCount { get; set; }

    /// <summary>Set when a SuperAdmin cancels the batch; workers stop claiming new items for it once non-null.</summary>
    public DateTime? CancellationRequestedUtc { get; set; }

    public DateTime CreatedUtc { get; set; }

    /// <summary>SuperAdmin <see cref="User.Id"/> who created the batch. Always populated — never a system/anonymous action.</summary>
    public int CreatedBy { get; set; }

    public DateTime? StartedUtc { get; set; }

    public DateTime? CompletedUtc { get; set; }

    /// <summary>Optimistic concurrency token — multiple workers increment counters concurrently.</summary>
    public byte[] RowVersion { get; set; } = null!;

    /// <summary>Monotonic pipeline version pinned at creation; never updated after insert.</summary>
    public int PipelineVersion { get; set; } = 1;

    /// <summary>Immutable JSON configuration snapshot captured at batch creation.</summary>
    public string ConfigurationSnapshotJson { get; set; } = string.Empty;

    /// <summary>End-to-end correlation id for structured logging across batch lifecycle.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Resolved photo provider name for this batch (never fetched during creation).</summary>
    public required string PhotoProviderName { get; set; }

    /// <summary>Relative queue priority for worker scheduling (higher runs sooner).</summary>
    public int Priority { get; set; }

    public College College { get; set; } = null!;

    public University University { get; set; } = null!;

    public ICollection<StudentEnrollmentItem> Items { get; set; } = new List<StudentEnrollmentItem>();
}
