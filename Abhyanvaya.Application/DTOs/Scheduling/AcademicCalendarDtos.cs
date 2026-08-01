using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.DTOs.Scheduling;

public sealed class AcademicYearDto
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public string Code { get; init; } = null!;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public bool IsCurrent { get; init; }
}

public sealed class CreateAcademicYearRequest
{
    public string Name { get; init; } = null!;
    public string Code { get; init; } = null!;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public bool IsCurrent { get; init; }
}

public sealed class UpdateAcademicYearRequest
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public string Code { get; init; } = null!;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public bool IsCurrent { get; init; }
}

public sealed class ClonePreviousYearRequest
{
    public int SourceYearId { get; init; }
    public string Name { get; init; } = null!;
    public string Code { get; init; } = null!;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public bool SetAsCurrent { get; init; }
}

public sealed class AcademicTermDto
{
    public int Id { get; init; }
    public int AcademicYearId { get; init; }
    public string Name { get; init; } = null!;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public int Sequence { get; init; }
}

public sealed class CreateAcademicTermRequest
{
    public int AcademicYearId { get; init; }
    public string Name { get; init; } = null!;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public int Sequence { get; init; }
}

public sealed class UpdateAcademicTermRequest
{
    public int Id { get; init; }
    public int AcademicYearId { get; init; }
    public string Name { get; init; } = null!;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public int Sequence { get; init; }
}

public sealed class WorkingDayDto
{
    public int Id { get; init; }
    public int AcademicYearId { get; init; }
    public byte DayOfWeek { get; init; }
    public bool IsWorking { get; init; }
}

public sealed class UpsertWorkingDayRequest
{
    public int? Id { get; init; }
    public int AcademicYearId { get; init; }
    public byte DayOfWeek { get; init; }
    public bool IsWorking { get; init; }
}

public sealed class HolidayDto
{
    public int Id { get; init; }
    public int AcademicYearId { get; init; }
    public string Name { get; init; } = null!;
    public DateOnly Date { get; init; }
    public HolidayType HolidayType { get; init; }
    public string? Description { get; init; }
    public int? HolidayTypeCatalogId { get; init; }
    public bool IsWorkingDayOverride { get; init; }
    public bool RequiresRescheduling { get; init; }
    public string? Colour { get; init; }
    public int? Priority { get; init; }
}

public sealed class CreateHolidayRequest
{
    public int AcademicYearId { get; init; }
    public string Name { get; init; } = null!;
    public DateOnly Date { get; init; }
    public HolidayType HolidayType { get; init; }
    public string? Description { get; init; }
    public int? HolidayTypeCatalogId { get; init; }
    public bool IsWorkingDayOverride { get; init; }
    public bool RequiresRescheduling { get; init; }
    public string? Colour { get; init; }
    public int? Priority { get; init; }
}

public sealed class UpdateHolidayRequest
{
    public int Id { get; init; }
    public int AcademicYearId { get; init; }
    public string Name { get; init; } = null!;
    public DateOnly Date { get; init; }
    public HolidayType HolidayType { get; init; }
    public string? Description { get; init; }
    public int? HolidayTypeCatalogId { get; init; }
    public bool IsWorkingDayOverride { get; init; }
    public bool RequiresRescheduling { get; init; }
    public string? Colour { get; init; }
    public int? Priority { get; init; }
}
