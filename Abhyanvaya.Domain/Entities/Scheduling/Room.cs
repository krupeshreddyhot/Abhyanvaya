using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class Room : BaseEntity
{
    public int FloorId { get; set; }
    public Floor? Floor { get; set; }
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!;
    public RoomType RoomType { get; set; }
    public int Capacity { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Available;
    public RoomFeatureFlags FeatureFlags { get; set; } = RoomFeatureFlags.None;
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public bool IsActive { get; set; } = true;
}
