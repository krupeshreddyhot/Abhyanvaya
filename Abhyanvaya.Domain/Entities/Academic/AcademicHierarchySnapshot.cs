using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Academic;

/// <summary>
/// AI29.1A.6 — Daily hierarchy snapshot for future analytics.
/// Feature-flagged; disabled by default. Read-only after generation.
/// </summary>
public class AcademicHierarchySnapshot : BaseEntity
{
    public DateOnly SnapshotDate { get; set; }
    public int Programs { get; set; }
    public int Courses { get; set; }
    public int Groups { get; set; }
    public int Semesters { get; set; }
    public int Sections { get; set; }
    public int Subjects { get; set; }
    public string HierarchyJson { get; set; } = "[]";
    public DateTime GeneratedDate { get; set; }
}
