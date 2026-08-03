using Abhyanvaya.Domain.Common;

using Abhyanvaya.Domain.Entities;

using Abhyanvaya.Domain.Enums.Scheduling;



namespace Abhyanvaya.Domain.Entities.Scheduling;



public class FacultyAvailability : BaseEntity

{

    public int StaffId { get; set; }

    public Staff? Staff { get; set; }

    public int AcademicYearId { get; set; }

    public AcademicYear? AcademicYear { get; set; }

    public FacultyAvailabilityType AvailabilityType { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public int? StartSlotId { get; set; }

    public TimeSlot? StartSlot { get; set; }

    public int? EndSlotId { get; set; }

    public TimeSlot? EndSlot { get; set; }

    public string? Reason { get; set; }

    public string? Remarks { get; set; }

}

