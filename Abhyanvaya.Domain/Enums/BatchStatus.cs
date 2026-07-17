namespace Abhyanvaya.Domain.Enums;

/// <summary>
/// Lifecycle state of a <see cref="Entities.StudentEnrollmentBatch"/> — a SuperAdmin-initiated
/// bulk AI enrollment run scoped to a University + College + Academic Year.
/// See docs/AI20_ENROLLMENT_ARCHITECTURE.md (§6) and docs/AI20_ENROLLMENT_DATABASE.md (§3.1).
/// </summary>
public enum BatchStatus
{
    /// <summary>Batch row created with its item rows; no worker has claimed an item yet.</summary>
    Created = 0,

    /// <summary>At least one item has been claimed by a worker and is in flight.</summary>
    Running = 1,

    /// <summary>Every item reached a terminal state with zero failures.</summary>
    Completed = 2,

    /// <summary>Every item reached a terminal state and at least one item is <see cref="EnrollmentStatus.Failed"/>.</summary>
    PartiallyFailed = 3,

    /// <summary>SuperAdmin explicitly cancelled the batch; remaining unclaimed items move to <see cref="EnrollmentStatus.Cancelled"/>.</summary>
    Cancelled = 4
}
