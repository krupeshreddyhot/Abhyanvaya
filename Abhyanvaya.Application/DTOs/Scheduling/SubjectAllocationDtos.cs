namespace Abhyanvaya.Application.DTOs.Scheduling;

public sealed class SubjectAllocationDto
{
    public int Id { get; init; }
    public int AcademicYearId { get; init; }
    public int SubjectId { get; init; }
    public int StaffId { get; init; }
    public int CourseId { get; init; }
    public int GroupId { get; init; }
    public int SemesterId { get; init; }
    public int DepartmentId { get; init; }
    public decimal WeeklyHours { get; init; }
    public int? PreferredRoomId { get; init; }
    public bool LabRequired { get; init; }
    public bool AiAttendanceEnabled { get; init; }
    public bool AttendanceMandatory { get; init; }
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public string? Notes { get; init; }
}

public sealed class CreateSubjectAllocationRequest
{
    public int AcademicYearId { get; init; }
    public int SubjectId { get; init; }
    public int StaffId { get; init; }
    public int CourseId { get; init; }
    public int GroupId { get; init; }
    public int SemesterId { get; init; }
    public int DepartmentId { get; init; }
    public decimal WeeklyHours { get; init; }
    public int? PreferredRoomId { get; init; }
    public bool LabRequired { get; init; }
    public bool AiAttendanceEnabled { get; init; }
    public bool AttendanceMandatory { get; init; }
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public string? Notes { get; init; }
}

public sealed class UpdateSubjectAllocationRequest
{
    public int Id { get; init; }
    public int AcademicYearId { get; init; }
    public int SubjectId { get; init; }
    public int StaffId { get; init; }
    public int CourseId { get; init; }
    public int GroupId { get; init; }
    public int SemesterId { get; init; }
    public int DepartmentId { get; init; }
    public decimal WeeklyHours { get; init; }
    public int? PreferredRoomId { get; init; }
    public bool LabRequired { get; init; }
    public bool AiAttendanceEnabled { get; init; }
    public bool AttendanceMandatory { get; init; }
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public string? Notes { get; init; }
}
