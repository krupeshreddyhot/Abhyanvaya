using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Scheduling;

/// <summary>Persisted snapshot of a conflict detection run (Phase 2B). Does not modify timetables.</summary>
public class ConflictDetectionRun : BaseEntity
{
    public int? TimetableId { get; set; }
    public Timetable? Timetable { get; set; }
    public int AcademicYearId { get; set; }
    public AcademicYear? AcademicYear { get; set; }
    public int? DepartmentId { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public string Status { get; set; } = "Completed";
    public int TotalConflicts { get; set; }
    public int FacultyCount { get; set; }
    public int RoomCount { get; set; }
    public int StudentCount { get; set; }
    public int CalendarCount { get; set; }
    public int CriticalCount { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int InformationCount { get; set; }
    public string TriggerSource { get; set; } = "Manual";

    public ICollection<ConflictFinding> Findings { get; set; } = [];
}
