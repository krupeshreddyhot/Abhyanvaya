using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Academic;

/// <summary>
/// AI29.1B — First-class combined-section aggregate (A+B+C). One timetable entry / one attendance session
/// may map to multiple sections via existing <see cref="TimetableSection"/> rows; this entity owns
/// membership history and audit for the operational group.
/// </summary>
public class SectionGroup : BaseEntity
{
    public int CollegeId { get; set; }
    public int AcademicYearId { get; set; }
    public int CourseId { get; set; }
    public int GroupId { get; set; }
    public int SemesterId { get; set; }

    public string GroupCode { get; set; } = null!;
    public string GroupName { get; set; } = null!;
    public string Status { get; set; } = "Active";
    public string? Notes { get; set; }
}
