using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.DTOs.Scheduling;

public sealed class ScenarioOwnerDto
{
    public int UserId { get; init; }
    public string DisplayName { get; init; } = "";
}

public sealed class ScenarioSummaryDto
{
    public Guid ScenarioId { get; init; }
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public ScenarioStatus Status { get; init; }
    public ScenarioOwnerDto Owner { get; init; } = new();
    public int AcademicYearId { get; init; }
    public int? DepartmentId { get; init; }
    public int? SemesterId { get; init; }
    public int? TimetableId { get; init; }
    public bool IsFavorite { get; init; }
    public bool IsPinned { get; init; }
    public bool IsTemplate { get; init; }
    public bool IsImmutable { get; init; }
    public string Category { get; init; } = "";
    public IReadOnlyList<string> Tags { get; init; } = [];
    public decimal CurrentScore { get; init; }
    public decimal ProjectedScore { get; init; }
    public int ConflictCount { get; init; }
    public int ReplayCount { get; init; }
    public int ComparisonCount { get; init; }
    public int ViewCount { get; init; }
    public int SnapshotCount { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateTime? LastReplayedUtc { get; init; }
    public bool ModifiesProductionTimetable { get; init; }
    public bool CanApply => false;
}

public sealed class OptimizationSnapshotDto
{
    public Guid SnapshotId { get; init; }
    public int Sequence { get; init; }
    public string Label { get; init; } = "";
    public Guid? SimulationId { get; init; }
    public string TimetableSummaryJson { get; init; } = "{}";
    public string SimulationJson { get; init; } = "{}";
    public string ScoresJson { get; init; } = "{}";
    public string ConflictSummaryJson { get; init; } = "{}";
    public string MetricsJson { get; init; } = "{}";
    public string RecommendationsJson { get; init; } = "[]";
    public DateTime CapturedUtc { get; init; }
    public bool IsImmutable { get; init; } = true;
}

public sealed class OptimizationScenarioDetailDto
{
    public ScenarioSummaryDto Summary { get; init; } = new();
    public IReadOnlyList<OptimizationSnapshotDto> Snapshots { get; init; } = [];
    public IReadOnlyList<ScenarioHistoryDto> History { get; init; } = [];
    public IReadOnlyList<ScenarioNoteDto> Notes { get; init; } = [];
    public IReadOnlyList<ScenarioCommentDto> Comments { get; init; } = [];
    public IReadOnlyList<ScenarioBookmarkDto> Bookmarks { get; init; } = [];
    public IReadOnlyList<ScenarioApprovalDto> Approvals { get; init; } = [];
}

public sealed class CreateScenarioRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int? AcademicYearId { get; set; }
    public int? DepartmentId { get; set; }
    public int? SemesterId { get; set; }
    public int? TimetableId { get; set; }
    public Guid? SourceSimulationId { get; set; }
    public string? Category { get; set; }
    public string? TagsCsv { get; set; }
    public bool CaptureFromLatestSimulation { get; set; } = true;
}

/// <summary>AI30 Phase 3 — create sandbox scenario from optimization engine output (never production edits).</summary>
public sealed class CreateOptimizationScenarioRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int AcademicYearId { get; set; }
    public int? DepartmentId { get; set; }
    public int? TimetableId { get; set; }
    public string? Category { get; set; }
    public string? TagsCsv { get; set; }
    public decimal BaselineScore { get; set; }
    public decimal ProjectedScore { get; set; }
    public int ConflictCount { get; set; }
    public string CandidatesJson { get; set; } = "[]";
    public string ComparisonJson { get; set; } = "{}";
    public string IntermediateResultsJson { get; set; } = "[]";
    public string MetricsJson { get; set; } = "{}";
    public Guid RunId { get; set; }
}

public sealed class RenameScenarioRequest
{
    public Guid ScenarioId { get; set; }
    public string Name { get; set; } = "";
}

public sealed class TagScenarioRequest
{
    public Guid ScenarioId { get; set; }
    public string TagsCsv { get; set; } = "";
    public string? Category { get; set; }
}

public sealed class DuplicateScenarioRequest
{
    public Guid ScenarioId { get; set; }
    public string? NewName { get; set; }
}

public sealed class ReplayTimelineDto
{
    public Guid ScenarioId { get; init; }
    public IReadOnlyList<ReplaySnapshotDto> Steps { get; init; } = [];
    public bool IsReadOnly { get; init; } = true;
}

public sealed class ReplaySnapshotDto
{
    public Guid SnapshotId { get; init; }
    public int Sequence { get; init; }
    public string Label { get; init; } = "";
    public decimal Score { get; init; }
    public int ConflictCount { get; init; }
    public DateTime CapturedUtc { get; init; }
}

public sealed class ReplayComparisonDto
{
    public ReplaySnapshotDto Left { get; init; } = new();
    public ReplaySnapshotDto Right { get; init; } = new();
    public decimal ScoreDelta { get; init; }
    public int ConflictDelta { get; init; }
    public string Notes { get; init; } = "Read-only replay comparison.";
}

public sealed class ScenarioComparisonResultDto
{
    public ScenarioSummaryDto Left { get; init; } = new();
    public ScenarioSummaryDto Right { get; init; } = new();
    public DifferenceSummaryDto Differences { get; init; } = new();
    public OptimizationScoreDto? LeftScore { get; init; }
    public OptimizationScoreDto? RightScore { get; init; }
    public IReadOnlyList<OptimizationMetricDto> LeftMetrics { get; init; } = [];
    public IReadOnlyList<OptimizationMetricDto> RightMetrics { get; init; } = [];
    public string LeftConflictSummaryJson { get; init; } = "{}";
    public string RightConflictSummaryJson { get; init; } = "{}";
    public string LeftRecommendationsJson { get; init; } = "[]";
    public string RightRecommendationsJson { get; init; } = "[]";
    public IReadOnlyList<string> ImprovementHighlights { get; init; } = [];
    public bool CanApply => false;
}

public sealed class DifferenceSummaryDto
{
    public decimal ScoreDelta { get; init; }
    public int ConflictDelta { get; init; }
    public decimal ProjectedScoreDelta { get; init; }
    public string Verdict { get; init; } = "";
}

public sealed class CompareScenariosRequest
{
    public Guid LeftScenarioId { get; set; }
    public Guid RightScenarioId { get; set; }
}

public sealed class ScenarioHistoryDto
{
    public ScenarioHistoryAction Action { get; init; }
    public string ActionName { get; init; } = "";
    public int? ActorUserId { get; init; }
    public string? Details { get; init; }
    public DateTime OccurredUtc { get; init; }
}

public sealed class ScenarioNoteDto
{
    public int Id { get; init; }
    public int UserId { get; init; }
    public string NoteText { get; init; } = "";
    public DateTime CreatedUtc { get; init; }
}

public sealed class ScenarioCommentDto
{
    public int Id { get; init; }
    public int UserId { get; init; }
    public string CommentText { get; init; } = "";
    public DateTime CreatedUtc { get; init; }
}

public sealed class ScenarioBookmarkDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
}

public sealed class ScenarioApprovalDto
{
    public int Id { get; init; }
    public string Status { get; init; } = "";
    public string? Message { get; init; }
    public int RequestedByUserId { get; init; }
    public DateTime RequestedUtc { get; init; }
}

public sealed class AddScenarioNoteRequest
{
    public Guid ScenarioId { get; set; }
    public string NoteText { get; set; } = "";
}

public sealed class AddScenarioCommentRequest
{
    public Guid ScenarioId { get; set; }
    public string CommentText { get; set; } = "";
}

public sealed class ShareScenarioRequest
{
    public Guid ScenarioId { get; set; }
    public int SharedWithUserId { get; set; }
}

public sealed class RequestScenarioApprovalRequest
{
    public Guid ScenarioId { get; set; }
    public string? Message { get; set; }
}

public sealed class MetricsEvolutionDto
{
    public IReadOnlyList<MetricsEvolutionPointDto> ScoreEvolution { get; init; } = [];
    public IReadOnlyList<MetricsEvolutionPointDto> ConflictEvolution { get; init; } = [];
    public IReadOnlyList<MetricsEvolutionPointDto> Utilization { get; init; } = [];
    public IReadOnlyList<MetricsEvolutionPointDto> FacultySatisfaction { get; init; } = [];
    public IReadOnlyList<MetricsEvolutionPointDto> RoomUsage { get; init; } = [];
    public IReadOnlyList<MetricsEvolutionPointDto> Travel { get; init; } = [];
    public IReadOnlyList<MetricsEvolutionPointDto> BreakCompliance { get; init; } = [];
    public string Notes { get; init; } = "Historical charts only — no predictions.";
}

public sealed class MetricsEvolutionPointDto
{
    public DateTime DateUtc { get; init; }
    public string Label { get; init; } = "";
    public decimal Value { get; init; }
}

public sealed class OptimizationWorkspaceDto
{
    public IReadOnlyList<ScenarioSummaryDto> Scenarios { get; init; } = [];
    public IReadOnlyList<ScenarioSummaryDto> Favorites { get; init; } = [];
    public IReadOnlyList<ScenarioSummaryDto> Templates { get; init; } = [];
    public MetricsEvolutionDto Evolution { get; init; } = new();
    public bool ShowApplyButton => false;
}
