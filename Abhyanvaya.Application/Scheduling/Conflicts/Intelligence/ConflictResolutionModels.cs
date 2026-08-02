using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling.Conflicts.Intelligence;

/// <summary>Advisory recommendation for a single conflict. Never applied automatically.</summary>
public sealed class ConflictRecommendation
{
    public required string RecommendationId { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required string ProviderCode { get; init; }
    public required IReadOnlyList<ResolutionOption> Options { get; init; }
    public required ResolutionScore Score { get; init; }
    public required IReadOnlyList<ResolutionReason> Reasons { get; init; }
    public string? EstimatedResolution { get; init; }
    public string? NavigationPath { get; init; }
    public bool IsAdvisoryOnly => true;
    public bool ModifiesTimetable => false;
}

public sealed class ResolutionOption
{
    public required string OptionCode { get; init; }
    public required string Label { get; init; }
    public required string Description { get; init; }
    public string ActionHint { get; init; } = "Manual";
    public int? SuggestedRoomId { get; init; }
    public int? SuggestedStaffId { get; init; }
    public int? SuggestedTimeSlotId { get; init; }
    public byte? SuggestedDayOfWeek { get; init; }
    public string? NavigationPath { get; init; }
}

public sealed class ResolutionScore
{
    public decimal Confidence { get; init; }
    public ResolutionImpactLevel Impact { get; init; }
    public ResolutionDifficulty Difficulty { get; init; }
    public int Rank { get; init; }
}

public sealed class ResolutionReason
{
    public required string Code { get; init; }
    public required string Message { get; init; }
}

public sealed class ConflictResolutionAdvice
{
    public required ConflictResult Conflict { get; init; }
    public required IReadOnlyList<ConflictRecommendation> Recommendations { get; init; }
}

public interface IConflictRecommendationProvider
{
    string ProviderCode { get; }
    bool CanHandle(ConflictResult conflict);
    Task<IReadOnlyList<ConflictRecommendation>> RecommendAsync(
        ConflictResult conflict,
        ConflictAnalysisContext context,
        CancellationToken cancellationToken = default);
}

public interface IConflictResolutionAdvisor
{
    Task<ConflictResolutionAdvice> AdviseAsync(
        ConflictResult conflict,
        ConflictAnalysisContext context,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConflictResolutionAdvice>> AdviseManyAsync(
        IReadOnlyList<ConflictResult> conflicts,
        ConflictAnalysisContext context,
        CancellationToken cancellationToken = default);
}
