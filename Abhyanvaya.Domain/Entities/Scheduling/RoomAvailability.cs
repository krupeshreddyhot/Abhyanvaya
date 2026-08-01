using Abhyanvaya.Domain.Common;

using Abhyanvaya.Domain.Enums.Scheduling;



namespace Abhyanvaya.Domain.Entities.Scheduling;



public class RoomAvailability : BaseEntity

{

    public int RoomId { get; set; }

    public Room? Room { get; set; }

    public int AcademicYearId { get; set; }

    public AcademicYear? AcademicYear { get; set; }

    public RoomAvailabilityType AvailabilityType { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public int? StartSlotId { get; set; }

    public TimeSlot? StartSlot { get; set; }

    public int? EndSlotId { get; set; }

    public TimeSlot? EndSlot { get; set; }

    public string? Reason { get; set; }

}

