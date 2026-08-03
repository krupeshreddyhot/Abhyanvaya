using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.DTOs.Scheduling;

public sealed class TimeSlotSetDto
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public string Code { get; init; } = null!;
    public int? AcademicYearId { get; init; }
    public string? Description { get; init; }
    public bool IsDefault { get; init; }
}

public sealed class CreateTimeSlotSetRequest
{
    public string Name { get; init; } = null!;
    public string Code { get; init; } = null!;
    public int? AcademicYearId { get; init; }
    public string? Description { get; init; }
    public bool IsDefault { get; init; }
}

public sealed class UpdateTimeSlotSetRequest
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public string Code { get; init; } = null!;
    public int? AcademicYearId { get; init; }
    public string? Description { get; init; }
    public bool IsDefault { get; init; }
}

public sealed class CloneTimeSlotSetRequest
{
    public int SourceSetId { get; init; }
    public string Name { get; init; } = null!;
    public string Code { get; init; } = null!;
    public int? AcademicYearId { get; init; }
    public bool IsDefault { get; init; }
}

public sealed class TimeSlotDto
{
    public int Id { get; init; }
    public int TimeSlotSetId { get; init; }
    public int? PeriodNumber { get; init; }
    public string Name { get; init; } = null!;
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }
    public int DurationMinutes { get; init; }
    public byte? DayOfWeek { get; init; }
    public SlotKind SlotKind { get; init; }
    public SessionKind SessionKind { get; init; }
}

public sealed class CreateTimeSlotRequest
{
    public int TimeSlotSetId { get; init; }
    public int? PeriodNumber { get; init; }
    public string Name { get; init; } = null!;
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }
    public int DurationMinutes { get; init; }
    public byte? DayOfWeek { get; init; }
    public SlotKind SlotKind { get; init; }
    public SessionKind SessionKind { get; init; } = SessionKind.None;
}

public sealed class UpdateTimeSlotRequest
{
    public int Id { get; init; }
    public int TimeSlotSetId { get; init; }
    public int? PeriodNumber { get; init; }
    public string Name { get; init; } = null!;
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }
    public int DurationMinutes { get; init; }
    public byte? DayOfWeek { get; init; }
    public SlotKind SlotKind { get; init; }
    public SessionKind SessionKind { get; init; }
}

public sealed record TimeSlotInterval(byte? DayOfWeek, int? PeriodNumber, TimeSpan StartTime, TimeSpan EndTime, int? ExcludeId = null);
