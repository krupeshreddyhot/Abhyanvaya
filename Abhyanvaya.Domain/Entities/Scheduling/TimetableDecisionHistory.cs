using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Domain.Entities.Scheduling;

/// <summary>Structured approval decision transition with old/new status.</summary>
public class TimetableDecisionHistory : BaseEntity
{
    public int RequestId { get; set; }
    public TimetableApprovalRequest? Request { get; set; }
    public int StepOrder { get; set; }
    public int ActorUserId { get; set; }
    public ApprovalDecision? Decision { get; set; }
    public string Action { get; set; } = null!;
    public string? Comment { get; set; }
    public string? DecisionNotes { get; set; }
    public string? ReviewerRemarks { get; set; }
    public TimetableApprovalRequestStatus? OldStatus { get; set; }
    public TimetableApprovalRequestStatus? NewStatus { get; set; }
    public DateTime OccurredUtc { get; set; }
}
