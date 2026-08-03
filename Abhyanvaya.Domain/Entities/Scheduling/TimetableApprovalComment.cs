using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Scheduling;

/// <summary>Free-form approval comment / reviewer remark on an approval request.</summary>
public class TimetableApprovalComment : BaseEntity
{
    public int RequestId { get; set; }
    public TimetableApprovalRequest? Request { get; set; }
    public int ActorUserId { get; set; }
    public string Comment { get; set; } = null!;
    public DateTime OccurredUtc { get; set; }
    public bool IsDecisionNote { get; set; }
}
