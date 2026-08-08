using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Academic;

/// <summary>AI29.1B — Membership of a section in a <see cref="SectionGroup"/> with history.</summary>
public class SectionGroupMember : BaseEntity
{
    public int SectionGroupId { get; set; }
    public int SectionId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsCurrent { get; set; } = true;
}
