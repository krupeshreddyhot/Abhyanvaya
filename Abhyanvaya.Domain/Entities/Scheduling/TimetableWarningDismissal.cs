using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class TimetableWarningDismissal : BaseEntity
{
    public int TimetableId { get; set; }
    public Timetable? Timetable { get; set; }
    public string WarningCode { get; set; } = null!;
    public int? EntryId { get; set; }
    public int? StaffId { get; set; }
    public int? RoomId { get; set; }
    public byte? DayOfWeek { get; set; }
    public int? TimeSlotId { get; set; }
    public int DismissedBy { get; set; }
    public DateTime DismissedUtc { get; set; }
}
