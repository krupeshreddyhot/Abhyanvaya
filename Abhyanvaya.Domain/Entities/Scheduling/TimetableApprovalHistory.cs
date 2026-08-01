using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class TimetableApprovalHistory : BaseEntity
{
    public int RequestId { get; set; }
    public TimetableApprovalRequest? Request { get; set; }
    public int StepOrder { get; set; }
    public int ActorUserId { get; set; }
    public ApprovalDecision? Decision { get; set; }
    public string? Comments { get; set; }
    public TimetableApprovalRequestStatus? OldStatus { get; set; }
    public TimetableApprovalRequestStatus? NewStatus { get; set; }
    public DateTime OccurredUtc { get; set; }
}
