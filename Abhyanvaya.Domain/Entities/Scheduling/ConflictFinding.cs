using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Domain.Entities.Scheduling;

/// <summary>Single explainable conflict finding. Never applied as an automatic fix.</summary>
public class ConflictFinding : BaseEntity
{
    public int ConflictDetectionRunId { get; set; }
    public ConflictDetectionRun? ConflictDetectionRun { get; set; }
    public string RuleCode { get; set; } = null!;
    public string RuleName { get; set; } = null!;
    public ConflictCategory Category { get; set; }
    public ConflictSeverity Severity { get; set; }
    public string Description { get; set; } = null!;
    public string WhyOccurred { get; set; } = null!;
    public string SuggestedResolution { get; set; } = null!;
    public int? TimetableId { get; set; }
    public int? TimetableEntryId { get; set; }
    public int? RelatedEntryId { get; set; }
    public byte? DayOfWeek { get; set; }
    public int? TimeSlotId { get; set; }
    public int? StaffId { get; set; }
    public int? RoomId { get; set; }
    public int? DepartmentId { get; set; }
    public int? CourseId { get; set; }
    public int? GroupId { get; set; }
    public int? SemesterId { get; set; }
    public int? SubjectId { get; set; }
    public string? NavigationPath { get; set; }
}
