using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class TimetableApprovalStep : BaseEntity
{
    public int RequestId { get; set; }
    public TimetableApprovalRequest? Request { get; set; }
    public int StepOrder { get; set; }
    public string RoleKey { get; set; } = null!;
    public TimetableApprovalRequestStatus Status { get; set; } = TimetableApprovalRequestStatus.Pending;
    public int? AssignedTo { get; set; }
    public int? DecidedBy { get; set; }
    public DateTime? DecidedUtc { get; set; }
    public ApprovalDecision? Decision { get; set; }
    public string? Comments { get; set; }
}
