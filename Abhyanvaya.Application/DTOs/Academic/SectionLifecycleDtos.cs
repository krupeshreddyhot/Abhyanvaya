namespace Abhyanvaya.Application.DTOs.Academic;

public sealed class SectionLifecycleTransitionRequest
{
    public string TargetStatus { get; init; } = "";
    public string? Reason { get; init; }
}

public sealed class SectionLifecycleHistoryDto
{
    public int Id { get; init; }
    public int SectionId { get; init; }
    public string FromStatus { get; init; } = "";
    public string ToStatus { get; init; } = "";
    public string? Reason { get; init; }
    public DateTime TransitionedUtc { get; init; }
    public int? TransitionedByUserId { get; init; }
}

public sealed class SectionTypeOptionDto
{
    public string Code { get; init; } = "";
    public string DisplayName { get; init; } = "";
}
