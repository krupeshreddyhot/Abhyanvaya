using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Academic;

/// <summary>AI29.12 — College-configurable auto-allocation strategy (optional).</summary>
public class SectionAllocationPreference : BaseEntity
{
    public int CollegeId { get; set; }
    public int AcademicYearId { get; set; }
    public int CourseId { get; set; }
    public int GroupId { get; set; }
    public int SemesterId { get; set; }
    /// <summary>Alphabetical | GenderBalance | Merit | Random | CapacityBased</summary>
    public string Strategy { get; set; } = "Alphabetical";
}
