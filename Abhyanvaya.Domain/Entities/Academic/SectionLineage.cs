using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Academic;

/// <summary>AI29.1B — Parent/child lineage for merge and split (historical reporting).</summary>
public class SectionLineage : BaseEntity
{
    public int ParentSectionId { get; set; }
    public int ChildSectionId { get; set; }
    /// <summary>Merge | Split</summary>
    public string RelationKind { get; set; } = null!;
    public Guid? TransactionId { get; set; }
    public DateOnly EffectiveDate { get; set; }
}
