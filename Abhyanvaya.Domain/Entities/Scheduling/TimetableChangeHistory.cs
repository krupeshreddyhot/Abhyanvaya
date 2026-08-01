using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class TimetableChangeHistory : BaseEntity
{
    public int TimetableId { get; set; }
    public Timetable? Timetable { get; set; }
    public int? EntryId { get; set; }
    public int? UserId { get; set; }
    public DateTime OccurredUtc { get; set; }
    public TimetableChangeOperation Operation { get; set; }
    public string? OldValueJson { get; set; }
    public string? NewValueJson { get; set; }
    public string? Reason { get; set; }
}
