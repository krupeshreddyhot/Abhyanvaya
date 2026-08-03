using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class FacultyDayPreference : BaseEntity
{
    public int FacultyWorkloadId { get; set; }
    public FacultyWorkload? FacultyWorkload { get; set; }
    public byte DayOfWeek { get; set; }
    public FacultyDayPreferenceType PreferenceType { get; set; }
}
