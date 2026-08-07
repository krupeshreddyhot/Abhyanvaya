using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Academic;

/// <summary>
/// AI29 — Operational section under Course → Group → Semester (not part of Subject curriculum).
/// </summary>
public class Section : BaseEntity
{
    public int CollegeId { get; set; }
    public int AcademicYearId { get; set; }
    public int CourseId { get; set; }
    public int GroupId { get; set; }
    public int SemesterId { get; set; }

    public string SectionCode { get; set; } = null!;
    public string SectionName { get; set; } = null!;
    public int DisplayOrder { get; set; }
    public int MaximumStrength { get; set; } = 60;
    /// <summary>Active | Inactive | Unassigned</summary>
    public string Status { get; set; } = "Active";
}
