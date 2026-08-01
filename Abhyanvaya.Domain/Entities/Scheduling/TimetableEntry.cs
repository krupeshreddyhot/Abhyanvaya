using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class TimetableEntry : BaseEntity
{
    public int TimetableId { get; set; }
    public Timetable? Timetable { get; set; }
    /// <summary>0=Sunday … 6=Saturday (matches WorkingDay).</summary>
    public byte DayOfWeek { get; set; }
    public int TimeSlotId { get; set; }
    public TimeSlot? TimeSlot { get; set; }
    public int SubjectAllocationId { get; set; }
    public SubjectAllocation? SubjectAllocation { get; set; }
    public int StaffId { get; set; }
    public Staff? Staff { get; set; }
    public int RoomId { get; set; }
    public Room? Room { get; set; }
    public int DepartmentId { get; set; }
    public Department? Department { get; set; }
    public int CourseId { get; set; }
    public Course? Course { get; set; }
    public int GroupId { get; set; }
    public Group? Group { get; set; }
    public int SemesterId { get; set; }
    public Semester? Semester { get; set; }
    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public string? Remarks { get; set; }
}
