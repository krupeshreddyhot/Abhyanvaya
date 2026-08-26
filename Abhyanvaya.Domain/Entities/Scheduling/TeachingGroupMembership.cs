using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Domain.Entities.Scheduling;

/// <summary>
/// AI-SCHED-TG.3 — Explicit operational teaching membership (Include/Exclude).
/// Does not replace StudentSection or StudentSubject and does not copy student master data.
/// </summary>
public class TeachingGroupMembership : BaseEntity
{
    public int TeachingGroupId { get; set; }
    public TeachingGroup? TeachingGroup { get; set; }
    public int StudentId { get; set; }
    public TeachingGroupMembershipInclusion Inclusion { get; set; } = TeachingGroupMembershipInclusion.Include;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsCurrent { get; set; } = true;
}
