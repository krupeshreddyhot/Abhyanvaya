using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class Timetable : BaseEntity
{
    public string Name { get; set; } = null!;
    public string? Code { get; set; }
    public int AcademicYearId { get; set; }
    public AcademicYear? AcademicYear { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public int? TimeSlotSetId { get; set; }
    public TimeSlotSet? TimeSlotSet { get; set; }
    public TimetableStatus Status { get; set; } = TimetableStatus.Draft;
    public int? ScheduleVersionId { get; set; }
    public ScheduleVersion? ScheduleVersion { get; set; }
    public string? Notes { get; set; }

    /// <summary>Post-publish governance freeze (distinct from designer Locked status).</summary>
    public bool IsFrozen { get; set; }
    public DateTime? FrozenDate { get; set; }
    public int? FrozenBy { get; set; }
    public string? FreezeReason { get; set; }
    public DateTime? UnlockDate { get; set; }
    public int? UnlockedBy { get; set; }
    public string? UnlockReason { get; set; }

    public int? ArchiveReasonId { get; set; }
    public ArchiveReasonLookup? ArchiveReason { get; set; }
    public string? ArchiveComments { get; set; }
    public int? ArchivedBy { get; set; }
    public DateTime? ArchivedDate { get; set; }
    public int? ReferenceVersionId { get; set; }
    public ScheduleVersion? ReferenceVersion { get; set; }

    public ICollection<TimetableEntry> Entries { get; set; } = [];
}
