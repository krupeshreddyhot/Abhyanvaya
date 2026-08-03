using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class HolidayTypeCatalog : BaseEntity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Colour { get; set; } = null!;
    public int Priority { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
