using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class TimetableCloneJob : BaseEntity
{
    public TimetableCloneJobType JobType { get; set; }
    public int SourceTimetableId { get; set; }
    public Timetable? SourceTimetable { get; set; }
    public int? TargetTimetableId { get; set; }
    public Timetable? TargetTimetable { get; set; }
    public string? PayloadJson { get; set; }
    public TimetableCloneJobStatus Status { get; set; } = TimetableCloneJobStatus.Queued;
    public int ProgressPercent { get; set; }
    public string? Summary { get; set; }
    public string? Error { get; set; }
    public int RequestedBy { get; set; }
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
}
