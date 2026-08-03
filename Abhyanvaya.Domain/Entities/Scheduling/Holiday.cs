using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class Holiday : BaseEntity
{
    public int AcademicYearId { get; set; }
    public AcademicYear? AcademicYear { get; set; }
    public string Name { get; set; } = null!;
    public DateOnly Date { get; set; }
    public HolidayType HolidayType { get; set; }
    public string? Description { get; set; }
    public int? HolidayTypeCatalogId { get; set; }
    public HolidayTypeCatalog? HolidayTypeCatalog { get; set; }
    public bool IsWorkingDayOverride { get; set; }
    public bool RequiresRescheduling { get; set; }
    public string? Colour { get; set; }
    public int? Priority { get; set; }
}
