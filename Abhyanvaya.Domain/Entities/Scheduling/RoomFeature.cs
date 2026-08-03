using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class RoomFeature : BaseEntity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Category { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<RoomFeatureAssignment> Assignments { get; set; } = [];
}
