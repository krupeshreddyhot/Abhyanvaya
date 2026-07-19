namespace Abhyanvaya.Domain.Enums;

/// <summary>
/// Lifecycle state of a single <see cref="Entities.StudentEnrollmentItem"/> within a
/// <see cref="Entities.StudentEnrollmentBatch"/>. See docs/AI20_ENROLLMENT_ARCHITECTURE.md (§6)
/// for the full state machine and docs/AI20_ENROLLMENT_DATABASE.md (§3.2) for column mapping.
/// </summary>
public enum EnrollmentStatus
{
    /// <summary>Item row created; not yet claimed by a worker. This status (plus <see cref="RetryRequired"/>) is what the durable job queue polls for.</summary>
    Pending = 0,

    /// <summary>Worker claimed the item and is downloading the source photo.</summary>
    Downloading = 1,

    /// <summary>Photo downloaded and validated at the file level; ready for face validation.</summary>
    Downloaded = 2,

    /// <summary>Running face-count/blur/resolution/pose validation (see docs/AI20_ENROLLMENT_ENGINE.md).</summary>
    Validating = 3,

    /// <summary>Generating the 512-d embedding for the single approved face.</summary>
    Embedding = 4,

    /// <summary>Terminal: embedding stored, <c>Student.PhotoKey</c> updated, <c>StudentFaceEmbedding</c> row created.</summary>
    Completed = 5,

    /// <summary>
    /// Terminal: a permanent failure (see <see cref="FailureCategory"/>) that automatic retry cannot fix.
    /// Still manually retriable by a SuperAdmin after the underlying cause is addressed.
    /// </summary>
    Failed = 6,

    /// <summary>
    /// Non-terminal: a transient failure eligible for automatic retry (bounded by a max retry count)
    /// before escalating to <see cref="Failed"/>. Re-enters the queue exactly like <see cref="Pending"/>.
    /// </summary>
    RetryRequired = 7,

    /// <summary>Terminal: the parent batch was cancelled before this item was claimed.</summary>
    Cancelled = 8
}
