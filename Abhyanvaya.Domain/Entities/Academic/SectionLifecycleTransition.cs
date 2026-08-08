using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Academic;

/// <summary>AI29.1B — Audit row for each validated lifecycle transition.</summary>
public class SectionLifecycleTransition : BaseEntity
{
    public int SectionId { get; set; }
    public string FromStatus { get; set; } = null!;
    public string ToStatus { get; set; } = null!;
    public string? Reason { get; set; }
    public DateTime TransitionedUtc { get; set; }
    public int? TransitionedByUserId { get; set; }
}
