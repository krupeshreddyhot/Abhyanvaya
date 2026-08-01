using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class WorkingDay : BaseEntity
{
    public int AcademicYearId { get; set; }
    public AcademicYear? AcademicYear { get; set; }
    /// <summary>0=Sunday through 6=Saturday.</summary>
    public byte DayOfWeek { get; set; }
    public bool IsWorking { get; set; }
}
