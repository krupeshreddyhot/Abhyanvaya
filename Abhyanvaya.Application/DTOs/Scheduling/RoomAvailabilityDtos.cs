using Abhyanvaya.Domain.Enums.Scheduling;



namespace Abhyanvaya.Application.DTOs.Scheduling;



public sealed class RoomAvailabilityDto

{

    public int Id { get; init; }

    public int RoomId { get; init; }

    public int AcademicYearId { get; init; }

    public RoomAvailabilityType AvailabilityType { get; init; }

    public DateOnly StartDate { get; init; }

    public DateOnly EndDate { get; init; }

    public int? StartSlotId { get; init; }

    public int? EndSlotId { get; init; }

    public string? Reason { get; init; }

}



public sealed class CreateRoomAvailabilityRequest

{

    public int RoomId { get; init; }

    public int AcademicYearId { get; init; }

    public RoomAvailabilityType AvailabilityType { get; init; }

    public DateOnly StartDate { get; init; }

    public DateOnly EndDate { get; init; }

    public int? StartSlotId { get; init; }

    public int? EndSlotId { get; init; }

    public string? Reason { get; init; }

}



public sealed class UpdateRoomAvailabilityRequest

{

    public int Id { get; init; }

    public int RoomId { get; init; }

    public int AcademicYearId { get; init; }

    public RoomAvailabilityType AvailabilityType { get; init; }

    public DateOnly StartDate { get; init; }

    public DateOnly EndDate { get; init; }

    public int? StartSlotId { get; init; }

    public int? EndSlotId { get; init; }

    public string? Reason { get; init; }

}

