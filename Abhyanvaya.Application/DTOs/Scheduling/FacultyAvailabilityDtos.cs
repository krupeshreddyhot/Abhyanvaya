using Abhyanvaya.Domain.Enums.Scheduling;



namespace Abhyanvaya.Application.DTOs.Scheduling;



public sealed class FacultyAvailabilityDto

{

    public int Id { get; init; }

    public int StaffId { get; init; }

    public int AcademicYearId { get; init; }

    public FacultyAvailabilityType AvailabilityType { get; init; }

    public DateOnly StartDate { get; init; }

    public DateOnly EndDate { get; init; }

    public int? StartSlotId { get; init; }

    public int? EndSlotId { get; init; }

    public string? Reason { get; init; }

    public string? Remarks { get; init; }

}



public sealed class CreateFacultyAvailabilityRequest

{

    public int StaffId { get; init; }

    public int AcademicYearId { get; init; }

    public FacultyAvailabilityType AvailabilityType { get; init; }

    public DateOnly StartDate { get; init; }

    public DateOnly EndDate { get; init; }

    public int? StartSlotId { get; init; }

    public int? EndSlotId { get; init; }

    public string? Reason { get; init; }

    public string? Remarks { get; init; }

}



public sealed class UpdateFacultyAvailabilityRequest

{

    public int Id { get; init; }

    public int StaffId { get; init; }

    public int AcademicYearId { get; init; }

    public FacultyAvailabilityType AvailabilityType { get; init; }

    public DateOnly StartDate { get; init; }

    public DateOnly EndDate { get; init; }

    public int? StartSlotId { get; init; }

    public int? EndSlotId { get; init; }

    public string? Reason { get; init; }

    public string? Remarks { get; init; }

}

