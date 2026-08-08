using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Academic;

/// <summary>
/// AI29.1B — Reversible merge transaction. Source sections are marked Merged (not deleted);
/// target section receives lineage links via <see cref="SectionLineage"/>.
/// </summary>
public class SectionMergeTransaction : BaseEntity
{
    public Guid TransactionId { get; set; }
    public int TargetSectionId { get; set; }
    public string SourceSectionIdsCsv { get; set; } = "";
    public DateOnly EffectiveDate { get; set; }
    public string Status { get; set; } = "Committed";
    public string? Notes { get; set; }
    public bool IsReversed { get; set; }
    public DateTime? ReversedUtc { get; set; }
}
