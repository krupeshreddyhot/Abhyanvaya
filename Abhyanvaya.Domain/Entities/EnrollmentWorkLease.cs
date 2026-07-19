using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Entities;

/// <summary>
/// Active processing lease for one enrollment work item (AI20.PHASE2.2).
/// Prevents duplicate processing across workers and servers.
/// </summary>
public class EnrollmentWorkLease : ITenantScoped
{
    public Guid Id { get; set; }

    public int TenantId { get; set; }

    public Guid ItemId { get; set; }

    public Guid BatchId { get; set; }

    public int StudentId { get; set; }

    public required string WorkerId { get; set; }

    public required string NodeId { get; set; }

    public DateTime AcquiredUtc { get; set; }

    public DateTime ExpiresUtc { get; set; }

    public DateTime HeartbeatUtc { get; set; }

    public int RenewalCount { get; set; }

    public Guid CorrelationId { get; set; }

    public byte[] LeaseVersion { get; set; } = null!;

    public EnrollmentWorkerState PipelineState { get; set; } = EnrollmentWorkerState.Running;

    public bool IsActive { get; set; } = true;

    public DateTime? ReleasedUtc { get; set; }

    public StudentEnrollmentItem Item { get; set; } = null!;
}
