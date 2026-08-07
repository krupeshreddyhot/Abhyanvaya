using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Academic;

/// <summary>
/// AI29.1A — Optional top-level academic grouping within a college (Commerce, Arts, Science, …).
/// AI29.1A.5 — First concrete Academic Organizational Unit (AOU); Program is not renamed.
/// Lifecycle remains simple master status: Active | Inactive | Archived.
/// </summary>
public class Program : BaseEntity
{
    public int CollegeId { get; set; }
    public string ProgramCode { get; set; } = null!;
    public string ProgramName { get; set; } = null!;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Active | Inactive | Archived — operational lifecycles belong to Year/Offering/Section/Timetable.</summary>
    public string Status { get; set; } = "Active";

    /// <summary>AI29.1A.5 — optional branding metadata for future dashboards.</summary>
    public string? Icon { get; set; }

    /// <summary>AI29.1A.5 — optional theme color (e.g. #1B4F72).</summary>
    public string? ThemeColor { get; set; }

    /// <summary>AI29.1A.5 — nullable link for future Academic Calendar; no enforcement yet.</summary>
    public int? AcademicCalendarId { get; set; }
}
