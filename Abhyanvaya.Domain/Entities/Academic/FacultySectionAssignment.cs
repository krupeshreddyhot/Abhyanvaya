using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Academic;

/// <summary>AI29 — Faculty (Staff) ↔ Section teaching assignment.</summary>
public class FacultySectionAssignment : BaseEntity
{
    public int FacultyId { get; set; }
    public int SectionId { get; set; }
    public int AcademicYearId { get; set; }
    /// <summary>Primary | Secondary</summary>
    public string Role { get; set; } = "Primary";
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsCurrent { get; set; } = true;
}
