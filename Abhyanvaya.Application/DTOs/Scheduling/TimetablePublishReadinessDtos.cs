using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.DTOs.Scheduling;

/// <summary>AI-SCHED-CAP Prompt 6 — Read-only publish readiness evaluation result.</summary>
public sealed class TimetablePublishReadinessResultDto
{
    public int TimetableId { get; init; }
    public TimetableStatus LifecycleState { get; init; }
    public bool IsFrozen { get; init; }
    /// <summary>True when there are no blocking findings (conflict + lifecycle).</summary>
    public bool IsReady { get; init; }
    public int BlockingFindingCount { get; init; }
    public int WarningFindingCount { get; init; }
    public int InformationalFindingCount { get; init; }
    public DateTime EvaluatedAtUtc { get; init; }
    public IReadOnlyList<PublishReadinessFindingDto> Findings { get; init; } = [];
}

public sealed class PublishReadinessFindingDto
{
    public string Code { get; init; } = null!;
    /// <summary>Critical | Error | Warning | Information</summary>
    public string Severity { get; init; } = null!;
    public bool IsBlocking { get; init; }
    public string Title { get; init; } = null!;
    public string Why { get; init; } = null!;
    public string RecommendedAction { get; init; } = null!;
    public int? TimetableEntryId { get; init; }
    public byte? DayOfWeek { get; init; }
    public int? TimeSlotId { get; init; }
    public int? RoomId { get; init; }
    public int? TeachingGroupId { get; init; }
    public string? TeachingGroupCode { get; init; }
    public string? TeachingGroupName { get; init; }
    public string? TeachingGroupStatus { get; init; }
    public int? PlacementSize { get; init; }
    public string? PlacementSizeSource { get; init; }
    public int? RoomCapacity { get; init; }
    public decimal? CapacityMarginPercent { get; init; }
    public decimal? EffectiveRoomCapacity { get; init; }
    public int? ResolvedStudentCount { get; init; }
    public int? MaxTeachingCapacity { get; init; }
}
