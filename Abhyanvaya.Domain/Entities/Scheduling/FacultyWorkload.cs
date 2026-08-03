using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class FacultyWorkload : BaseEntity
{
    public int StaffId { get; set; }
    public Staff? Staff { get; set; }
    public int MaxPeriodsPerDay { get; set; }
    public int MaxPeriodsPerWeek { get; set; }
    public decimal TeachingLoadHours { get; set; }
    public decimal LabLoadHours { get; set; }
    public decimal MentoringLoadHours { get; set; }
    public decimal AdministrativeLoadHours { get; set; }
    public bool IsGuestFaculty { get; set; }
    public bool IsAdjunctFaculty { get; set; }
    public string? Notes { get; set; }

    public ICollection<FacultyDayPreference> DayPreferences { get; set; } = [];
    public ICollection<FacultyTimeSlotPreference> TimeSlotPreferences { get; set; } = [];
}
