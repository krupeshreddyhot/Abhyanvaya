using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class TimeSlot : BaseEntity
{
    public int TimeSlotSetId { get; set; }
    public TimeSlotSet? TimeSlotSet { get; set; }
    public int? PeriodNumber { get; set; }
    public string Name { get; set; } = null!;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int DurationMinutes { get; set; }
    /// <summary>Null applies to all working days.</summary>
    public byte? DayOfWeek { get; set; }
    public SlotKind SlotKind { get; set; }
    public SessionKind SessionKind { get; set; } = SessionKind.None;
}
