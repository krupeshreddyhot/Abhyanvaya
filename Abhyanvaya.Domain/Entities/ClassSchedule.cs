using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities;

/// <summary>
/// A scheduled class slot linking faculty, academic context, period, and calendar date.
/// Attendance sessions may be created from an active schedule row.
/// </summary>
public class ClassSchedule : ITenantScoped
{
    public Guid Id { get; set; }

    public int TenantId { get; set; }

    public int StaffId { get; set; }

    public int CourseId { get; set; }

    public int GroupId { get; set; }

    public int SemesterId { get; set; }

    public int SubjectId { get; set; }

    public int PeriodNumber { get; set; }

    public DateOnly ScheduleDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedUtc { get; set; }

    public Staff Staff { get; set; } = null!;

    public Course Course { get; set; } = null!;

    public Group Group { get; set; } = null!;

    public Semester Semester { get; set; } = null!;

    public Subject Subject { get; set; } = null!;

    public ICollection<AttendanceSession> AttendanceSessions { get; set; } = [];
}
