using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class SubjectAllocation : BaseEntity
{
    public int AcademicYearId { get; set; }
    public AcademicYear? AcademicYear { get; set; }
    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public int StaffId { get; set; }
    public Staff? Staff { get; set; }
    public int CourseId { get; set; }
    public Course? Course { get; set; }
    public int GroupId { get; set; }
    public Group? Group { get; set; }
    public int SemesterId { get; set; }
    public Semester? Semester { get; set; }
    public int DepartmentId { get; set; }
    public Department? Department { get; set; }
    public decimal WeeklyHours { get; set; }
    public int? PreferredRoomId { get; set; }
    public Room? PreferredRoom { get; set; }
    public bool LabRequired { get; set; }
    public bool AiAttendanceEnabled { get; set; }
    public bool AttendanceMandatory { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string? Notes { get; set; }
}
