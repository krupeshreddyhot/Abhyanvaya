using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class Building : BaseEntity
{
    public int CampusId { get; set; }
    public Campus? Campus { get; set; }
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    public ICollection<Floor> Floors { get; set; } = [];
}
