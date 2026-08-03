using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class RoomFeatureAssignment : BaseEntity
{
    public int RoomId { get; set; }
    public Room? Room { get; set; }
    public int RoomFeatureId { get; set; }
    public RoomFeature? RoomFeature { get; set; }
}
