using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Academic;

/// <summary>
/// AI29.1B — Reversible split transaction. Source section is marked Split (not deleted);
/// child sections retain <c>ParentSectionId</c> and lineage rows.
/// </summary>
public class SectionSplitTransaction : BaseEntity
{
    public Guid TransactionId { get; set; }
    public int SourceSectionId { get; set; }
    public string ChildSectionIdsCsv { get; set; } = "";
    /// <summary>Strategy key for AI29.1C allocation (Alphabetical, CapacityBased, …) — stored only.</summary>
    public string StrategyCode { get; set; } = "Manual";
    public DateOnly EffectiveDate { get; set; }
    public string Status { get; set; } = "Committed";
    public string? Notes { get; set; }
    public bool IsReversed { get; set; }
    public DateTime? ReversedUtc { get; set; }
}
