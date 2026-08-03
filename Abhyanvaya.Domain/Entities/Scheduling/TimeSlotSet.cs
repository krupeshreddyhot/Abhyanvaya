using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class TimeSlotSet : BaseEntity
{
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!;
    public int? AcademicYearId { get; set; }
    public AcademicYear? AcademicYear { get; set; }
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public int? TimeSlotTemplateId { get; set; }
    public TimeSlotTemplate? TimeSlotTemplate { get; set; }

    public ICollection<TimeSlot> TimeSlots { get; set; } = [];
}
