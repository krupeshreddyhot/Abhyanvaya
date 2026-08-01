using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class AcademicTerm : BaseEntity
{
    public int AcademicYearId { get; set; }
    public AcademicYear? AcademicYear { get; set; }
    public string Name { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int Sequence { get; set; }
}
