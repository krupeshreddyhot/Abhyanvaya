namespace Abhyanvaya.Application.DTOs.Scheduling;

/// <summary>AI-SCHED-TG.4A — TeachingGroup ↔ Section source-of-truth link.</summary>
public sealed class TeachingGroupSectionDto
{
    public int Id { get; init; }
    public int TeachingGroupId { get; init; }
    public int SectionId { get; init; }
    public bool IsPrimary { get; init; }
    public string? SectionCode { get; init; }
    public string? SectionName { get; init; }
}

public sealed class ReplaceTeachingGroupSectionsRequest
{
    public IReadOnlyList<int> SectionIds { get; init; } = [];
}
