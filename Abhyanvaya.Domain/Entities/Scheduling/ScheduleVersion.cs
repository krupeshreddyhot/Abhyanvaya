using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Domain.Entities.Scheduling;

public class ScheduleVersion : BaseEntity
{
    public int AcademicYearId { get; set; }
    public AcademicYear? AcademicYear { get; set; }
    public int? AcademicTermId { get; set; }
    public AcademicTerm? AcademicTerm { get; set; }
    public int VersionNumber { get; set; }
    public string VersionName { get; set; } = null!;
    public ScheduleVersionStatus Status { get; set; } = ScheduleVersionStatus.Draft;
    public bool IsCurrent { get; set; }
    public DateTime? PublishedDate { get; set; }
    public int? PublishedBy { get; set; }
    public DateTime? ArchivedDate { get; set; }
    public int? ArchivedBy { get; set; }
    public int? ArchiveReasonId { get; set; }
    public ArchiveReasonLookup? ArchiveReason { get; set; }
    public string? ArchiveComments { get; set; }
    public int? ReferenceVersionId { get; set; }
    public ScheduleVersion? ReferenceVersion { get; set; }
    public int? ParentVersionId { get; set; }
    public ScheduleVersion? ParentVersion { get; set; }
    public string? Remarks { get; set; }

    public ICollection<Timetable> Timetables { get; set; } = [];
}
