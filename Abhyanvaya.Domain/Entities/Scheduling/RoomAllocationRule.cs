using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class RoomAllocationRule : BaseEntity
{
    public string Name { get; set; } = null!;
    public int? AcademicYearId { get; set; }
    public AcademicYear? AcademicYear { get; set; }
    public RoomType? RoomType { get; set; }
    public int? MinCapacity { get; set; }
    public int? MaxCapacity { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public int? CourseId { get; set; }
    public Course? Course { get; set; }
    public bool RequireComputerLab { get; set; }
    public bool RequireScienceLab { get; set; }
    public bool RequireCommerceLab { get; set; }
    public bool RequireAiCamera { get; set; }
    public bool RequireProjector { get; set; }
    public bool RequireSmartBoard { get; set; }
    public int? PreferredRoomId { get; set; }
    public Room? PreferredRoom { get; set; }
    public int Priority { get; set; }
    public string? Notes { get; set; }
}
