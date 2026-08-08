using Abhyanvaya.Domain.Academic;
using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Academic;

/// <summary>
/// AI29 / AI29.1B — Operational section under Course → Group → Semester (not part of Subject curriculum).
/// Lifecycle transitions are owned by <c>ISectionLifecycleService</c>; capacity math by <c>ISectionCapacityEngine</c>.
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

    /// <summary>
    /// AI29 hard capacity / AI29.1B Maximum Capacity (same column for backward compatibility).
    /// </summary>
    public int MaximumStrength { get; set; } = 60;

    /// <summary>AI29.1B — Minimum operational capacity.</summary>
    public int MinimumCapacity { get; set; }

    /// <summary>AI29.1B — Recommended planning capacity.</summary>
    public int RecommendedCapacity { get; set; }

    /// <summary>AI29.1B — Seats held/reserved (not available for new allocation).</summary>
    public int ReservedSeats { get; set; }

    /// <summary>AI29.1B — Waiting-list count (informational; no auto-movement).</summary>
    public int WaitingListCount { get; set; }

    /// <summary>
    /// Lifecycle status: Draft | Planning | Open | Active | Locked | Merged | Split | Closed | Archived.
    /// Legacy Inactive → Closed, Unassigned → Draft (normalized by lifecycle service).
    /// </summary>
    public string Status { get; set; } = SectionLifecycleStates.Active;

    /// <summary>AI29.1B — Configurable type code (Regular, Honours, …). Not a domain enum switch.</summary>
    public string SectionTypeCode { get; set; } = SectionTypeCodes.Regular;

    /// <summary>AI29.1B — Lineage parent (set after split/merge child creation).</summary>
    public int? ParentSectionId { get; set; }

    /// <summary>AI29.1B — Optional convenience FK to current SectionGroup (membership history is authoritative).</summary>
    public int? SectionGroupId { get; set; }
}
