using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Academic;

/// <summary>
/// AI29.1B.5 — Hierarchical section policy (configuration + validation/warnings only).
/// Scope: Tenant → Program → Course → SectionType (most specific wins).
/// </summary>
public class SectionPolicy : BaseEntity
{
    public int CollegeId { get; set; }

    /// <summary>Tenant | Program | Course | SectionType</summary>
    public string ScopeLevel { get; set; } = "Tenant";

    public int? ProgramId { get; set; }
    public int? CourseId { get; set; }
    public string? SectionTypeCode { get; set; }

    public int? MaximumCapacity { get; set; }
    public int? MinimumCapacity { get; set; }
    public int? RecommendedCapacity { get; set; }
    public int? MaximumCombinedSections { get; set; }
    public int? MaximumFaculty { get; set; }
    public int? MaximumRoomOccupancy { get; set; }
    public bool? AllowMerge { get; set; }
    public bool? AllowSplit { get; set; }

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}
