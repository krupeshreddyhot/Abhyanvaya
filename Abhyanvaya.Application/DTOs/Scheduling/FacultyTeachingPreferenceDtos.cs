using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.DTOs.Scheduling;

public sealed class FacultyTeachingPreferenceDto
{
    public int Id { get; init; }
    public int StaffId { get; init; }
    public int AcademicYearId { get; init; }
    public int? PreferredCampusId { get; init; }
    public int? PreferredBuildingId { get; init; }
    public int? PreferredFloorId { get; init; }
    public int? PreferredRoomId { get; init; }
    public int? PreferredSubjectId { get; init; }
    public int? PreferredDepartmentId { get; init; }
    public int? PreferredCourseId { get; init; }
    public int? PreferredGroupId { get; init; }
    public int? PreferredSemesterId { get; init; }
    public int? PreferredFirstPeriod { get; init; }
    public int? PreferredLastPeriod { get; init; }
    public byte PreferredWorkingDaysFlags { get; init; }
    public int MaximumContinuousClasses { get; init; }
    public int MinimumBreakBetweenClasses { get; init; }
    public PreferredTeachingMode PreferredTeachingMode { get; init; }
    public int Priority { get; init; }
    public string? Remarks { get; init; }
    public bool IsActive { get; init; }
}

public sealed class CreateFacultyTeachingPreferenceRequest
{
    public int StaffId { get; init; }
    public int AcademicYearId { get; init; }
    public int? PreferredCampusId { get; init; }
    public int? PreferredBuildingId { get; init; }
    public int? PreferredFloorId { get; init; }
    public int? PreferredRoomId { get; init; }
    public int? PreferredSubjectId { get; init; }
    public int? PreferredDepartmentId { get; init; }
    public int? PreferredCourseId { get; init; }
    public int? PreferredGroupId { get; init; }
    public int? PreferredSemesterId { get; init; }
    public int? PreferredFirstPeriod { get; init; }
    public int? PreferredLastPeriod { get; init; }
    public byte PreferredWorkingDaysFlags { get; init; }
    public int MaximumContinuousClasses { get; init; }
    public int MinimumBreakBetweenClasses { get; init; }
    public PreferredTeachingMode PreferredTeachingMode { get; init; } = PreferredTeachingMode.Any;
    public int Priority { get; init; }
    public string? Remarks { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class UpdateFacultyTeachingPreferenceRequest
{
    public int Id { get; init; }
    public int StaffId { get; init; }
    public int AcademicYearId { get; init; }
    public int? PreferredCampusId { get; init; }
    public int? PreferredBuildingId { get; init; }
    public int? PreferredFloorId { get; init; }
    public int? PreferredRoomId { get; init; }
    public int? PreferredSubjectId { get; init; }
    public int? PreferredDepartmentId { get; init; }
    public int? PreferredCourseId { get; init; }
    public int? PreferredGroupId { get; init; }
    public int? PreferredSemesterId { get; init; }
    public int? PreferredFirstPeriod { get; init; }
    public int? PreferredLastPeriod { get; init; }
    public byte PreferredWorkingDaysFlags { get; init; }
    public int MaximumContinuousClasses { get; init; }
    public int MinimumBreakBetweenClasses { get; init; }
    public PreferredTeachingMode PreferredTeachingMode { get; init; }
    public int Priority { get; init; }
    public string? Remarks { get; init; }
    public bool IsActive { get; init; }
}
