using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class TimetableApprovalRequest : BaseEntity
{
    public int ScheduleVersionId { get; set; }
    public ScheduleVersion? ScheduleVersion { get; set; }
    public int TimetableId { get; set; }
    public Timetable? Timetable { get; set; }
    public TimetableApprovalRequestStatus Status { get; set; } = TimetableApprovalRequestStatus.Pending;
    public int SubmittedBy { get; set; }
    public DateTime SubmittedUtc { get; set; }
    public int CurrentStepOrder { get; set; } = 1;

    public ICollection<TimetableApprovalStep> Steps { get; set; } = [];
    public ICollection<TimetableApprovalHistory> History { get; set; } = [];
}
