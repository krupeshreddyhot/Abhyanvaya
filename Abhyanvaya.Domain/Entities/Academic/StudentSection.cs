using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Academic;

/// <summary>AI29 — Student ↔ Section allocation with history (never overwrite prior rows).</summary>
public class StudentSection : BaseEntity
{
    public int StudentId { get; set; }
    public int SectionId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsCurrent { get; set; } = true;
    public string? TransferReason { get; set; }
}
