using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Scheduling;

/// <summary>
/// AI-SCHED-TG.3 — TeachingGroup ↔ academic Section link.
/// Not student membership; does not mutate StudentSection.
/// </summary>
public class TeachingGroupSection : BaseEntity
{
    public int TeachingGroupId { get; set; }
    public TeachingGroup? TeachingGroup { get; set; }
    public int SectionId { get; set; }
    public bool IsPrimary { get; set; }
}
