using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class AcademicYear : BaseEntity
{
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsCurrent { get; set; }

    public ICollection<AcademicTerm> Terms { get; set; } = [];
    public ICollection<WorkingDay> WorkingDays { get; set; } = [];
    public ICollection<Holiday> Holidays { get; set; } = [];
}
