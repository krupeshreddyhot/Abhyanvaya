using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities;

/// <summary>
/// Immutable point-in-time enrollment progress snapshot for audit and streaming ETag history.
/// </summary>
public class StudentEnrollmentProgressSnapshot : ITenantScoped
{
    public Guid Id { get; set; }

    public int TenantId { get; set; }

    public Guid BatchId { get; set; }

    public DateTime CapturedUtc { get; set; }

    public required string SnapshotJson { get; set; }

    public StudentEnrollmentBatch Batch { get; set; } = null!;
}
