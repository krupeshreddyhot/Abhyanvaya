using Abhyanvaya.Domain.Common;

using Abhyanvaya.Domain.Enums.Scheduling;



namespace Abhyanvaya.Domain.Entities.Scheduling;



public class TimeSlotTemplate : BaseEntity

{

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public TimeSlotTemplateType TemplateType { get; set; }

    public bool IsDefault { get; set; }



    public ICollection<TimeSlotSet> TimeSlotSets { get; set; } = [];

}

