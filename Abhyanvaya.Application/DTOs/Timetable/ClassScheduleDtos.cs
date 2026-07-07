namespace Abhyanvaya.Application.DTOs.Timetable;

public sealed class ClassScheduleDto
{
    public Guid Id { get; set; }

    public int TenantId { get; set; }

    public int StaffId { get; set; }

    public string? StaffName { get; set; }

    public int CourseId { get; set; }

    public int GroupId { get; set; }

    public int SemesterId { get; set; }

    public int SubjectId { get; set; }

    public int PeriodNumber { get; set; }

    public DateOnly ScheduleDate { get; set; }

    public bool IsActive { get; set; }
}

public sealed class ClassScheduleQuery
{
    public DateOnly? ScheduleDate { get; set; }

    public int? StaffId { get; set; }

    public int? CourseId { get; set; }

    public int? GroupId { get; set; }

    public int? SemesterId { get; set; }

    public int? SubjectId { get; set; }

    public bool ActiveOnly { get; set; } = true;
}

public sealed class CreateClassScheduleRequest
{
    public int StaffId { get; set; }

    public int CourseId { get; set; }

    public int GroupId { get; set; }

    public int SemesterId { get; set; }

    public int SubjectId { get; set; }

    public int PeriodNumber { get; set; }

    public DateOnly ScheduleDate { get; set; }
}
