using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Academic;

/// <summary>
/// AI29.1A.5 — Configuration-only academic rules for a Program.
/// No enforcement in this phase.
/// </summary>
public class ProgramPolicy : BaseEntity
{
    public int ProgramId { get; set; }
    public decimal? MinimumAttendancePercent { get; set; }
    public decimal? CreditsRequired { get; set; }
    public decimal? PassMarks { get; set; }
    public int? MaximumBacklogs { get; set; }
    public int? MaximumSubjects { get; set; }

    /// <summary>Free-form / JSON academic rules text for future engines.</summary>
    public string? AcademicRules { get; set; }
}
