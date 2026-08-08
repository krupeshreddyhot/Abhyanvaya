using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Academic;

/// <summary>
/// AI29.1B.5 — Immutable operational snapshot. Never edit after creation; always append a new version.
/// </summary>
public class SectionVersion : BaseEntity
{
    public int SectionId { get; set; }
    public int VersionNumber { get; set; }
    public DateTime VersionDate { get; set; }
    public int? ChangedBy { get; set; }
    public string? Reason { get; set; }

    /// <summary>Create | Update | Merge | Split | CapacityChange | LifecycleChange</summary>
    public string Operation { get; set; } = null!;

    public int? PreviousVersionId { get; set; }

    // Snapshot payload (immutable copy of operational state at version time)
    public string SectionCode { get; set; } = "";
    public string SectionName { get; set; } = "";
    public string Status { get; set; } = "";
    public string SectionTypeCode { get; set; } = "";
    public int MaximumCapacity { get; set; }
    public int MinimumCapacity { get; set; }
    public int RecommendedCapacity { get; set; }
    public int ReservedSeats { get; set; }
    public int WaitingListCount { get; set; }
    public int CurrentStrength { get; set; }
    public double OccupancyPercent { get; set; }
    public int? ParentSectionId { get; set; }
    public int? SectionGroupId { get; set; }
}
