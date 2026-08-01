using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class Floor : BaseEntity
{
    public int BuildingId { get; set; }
    public Building? Building { get; set; }
    public string Name { get; set; } = null!;
    public int LevelNumber { get; set; }

    public ICollection<Room> Rooms { get; set; } = [];
}
