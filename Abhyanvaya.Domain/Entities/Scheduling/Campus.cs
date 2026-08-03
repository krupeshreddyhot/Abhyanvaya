using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class Campus : BaseEntity
{
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Building> Buildings { get; set; } = [];
}
