using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class FacultyTeachingPreference : BaseEntity
{
    public int StaffId { get; set; }
    public Staff? Staff { get; set; }
    public int AcademicYearId { get; set; }
    public AcademicYear? AcademicYear { get; set; }
    public int? PreferredCampusId { get; set; }
    public Campus? PreferredCampus { get; set; }
    public int? PreferredBuildingId { get; set; }
    public Building? PreferredBuilding { get; set; }
    public int? PreferredFloorId { get; set; }
    public Floor? PreferredFloor { get; set; }
    public int? PreferredRoomId { get; set; }
    public Room? PreferredRoom { get; set; }
    public int? PreferredSubjectId { get; set; }
    public Subject? PreferredSubject { get; set; }
    public int? PreferredDepartmentId { get; set; }
    public Department? PreferredDepartment { get; set; }
    public int? PreferredCourseId { get; set; }
    public Course? PreferredCourse { get; set; }
    public int? PreferredGroupId { get; set; }
    public Group? PreferredGroup { get; set; }
    public int? PreferredSemesterId { get; set; }
    public Semester? PreferredSemester { get; set; }
    public int? PreferredFirstPeriod { get; set; }
    public int? PreferredLastPeriod { get; set; }
    public byte PreferredWorkingDaysFlags { get; set; }
    public int MaximumContinuousClasses { get; set; }
    public int MinimumBreakBetweenClasses { get; set; }
    public PreferredTeachingMode PreferredTeachingMode { get; set; } = PreferredTeachingMode.Any;
    public int Priority { get; set; }
    public string? Remarks { get; set; }
    public bool IsActive { get; set; } = true;
}
