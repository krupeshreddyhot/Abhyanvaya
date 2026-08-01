using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class FacultyTimeSlotPreference : BaseEntity
{
    public int FacultyWorkloadId { get; set; }
    public FacultyWorkload? FacultyWorkload { get; set; }
    public int TimeSlotId { get; set; }
    public TimeSlot? TimeSlot { get; set; }
    public bool IsPreferred { get; set; }
}
