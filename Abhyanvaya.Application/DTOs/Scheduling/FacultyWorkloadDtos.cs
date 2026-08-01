using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.DTOs.Scheduling;

public sealed class FacultyWorkloadDto
{
    public int Id { get; init; }
    public int StaffId { get; init; }
    public int MaxPeriodsPerDay { get; init; }
    public int MaxPeriodsPerWeek { get; init; }
    public decimal TeachingLoadHours { get; init; }
    public decimal LabLoadHours { get; init; }
    public decimal MentoringLoadHours { get; init; }
    public decimal AdministrativeLoadHours { get; init; }
    public bool IsGuestFaculty { get; init; }
    public bool IsAdjunctFaculty { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<FacultyDayPreferenceDto> DayPreferences { get; init; } = [];
    public IReadOnlyList<FacultyTimeSlotPreferenceDto> TimeSlotPreferences { get; init; } = [];
}

public sealed class UpsertFacultyWorkloadRequest
{
    public int StaffId { get; init; }
    public int MaxPeriodsPerDay { get; init; }
    public int MaxPeriodsPerWeek { get; init; }
    public decimal TeachingLoadHours { get; init; }
    public decimal LabLoadHours { get; init; }
    public decimal MentoringLoadHours { get; init; }
    public decimal AdministrativeLoadHours { get; init; }
    public bool IsGuestFaculty { get; init; }
    public bool IsAdjunctFaculty { get; init; }
    public string? Notes { get; init; }
}

public sealed class FacultyDayPreferenceDto
{
    public int Id { get; init; }
    public int FacultyWorkloadId { get; init; }
    public byte DayOfWeek { get; init; }
    public FacultyDayPreferenceType PreferenceType { get; init; }
}

public sealed class UpsertFacultyDayPreferenceRequest
{
    public int? Id { get; init; }
    public int FacultyWorkloadId { get; init; }
    public byte DayOfWeek { get; init; }
    public FacultyDayPreferenceType PreferenceType { get; init; }
}

public sealed class FacultyTimeSlotPreferenceDto
{
    public int Id { get; init; }
    public int FacultyWorkloadId { get; init; }
    public int TimeSlotId { get; init; }
    public bool IsPreferred { get; init; }
}

public sealed class UpsertFacultyTimeSlotPreferenceRequest
{
    public int? Id { get; init; }
    public int FacultyWorkloadId { get; init; }
    public int TimeSlotId { get; init; }
    public bool IsPreferred { get; init; }
}
