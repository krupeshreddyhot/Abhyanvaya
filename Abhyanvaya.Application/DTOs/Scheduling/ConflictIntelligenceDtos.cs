using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.DTOs.Scheduling;

public sealed class ResolutionOptionDto
{
    public string OptionCode { get; init; } = "";
    public string Label { get; init; } = "";
    public string Description { get; init; } = "";
    public string ActionHint { get; init; } = "Manual";
    public int? SuggestedRoomId { get; init; }
    public int? SuggestedStaffId { get; init; }
    public int? SuggestedTimeSlotId { get; init; }
    public byte? SuggestedDayOfWeek { get; init; }
    public string? NavigationPath { get; init; }
}

public sealed class ResolutionScoreDto
{
    public decimal Confidence { get; init; }
    public ResolutionImpactLevel Impact { get; init; }
    public ResolutionDifficulty Difficulty { get; init; }
    public int Rank { get; init; }
}

public sealed class ResolutionReasonDto
{
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
}

public sealed class ConflictResolutionDto
{
    public string RecommendationId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Summary { get; init; } = "";
    public string ProviderCode { get; init; } = "";
    public IReadOnlyList<ResolutionOptionDto> Options { get; init; } = [];
    public ResolutionScoreDto Score { get; init; } = new();
    public IReadOnlyList<ResolutionReasonDto> Reasons { get; init; } = [];
    public string? EstimatedResolution { get; init; }
    public string? NavigationPath { get; init; }
    public bool IsAdvisoryOnly { get; init; } = true;
    public bool ModifiesTimetable { get; init; }
}

public sealed class ConflictGuidanceDto
{
    public ConflictResultDto Conflict { get; init; } = new();
    public IReadOnlyList<ConflictResolutionDto> SuggestedResolutions { get; init; } = [];
    public ConflictExplanationDto Explanation { get; init; } = new();
    public ImpactGraphDto Impact { get; init; } = new();
}

public sealed class ConflictExplanationDto
{
    public string RuleCode { get; init; } = "";
    public string RuleName { get; init; } = "";
    public string RuleCategory { get; init; } = "";
    public string RuleDescription { get; init; } = "";
    public string BusinessReason { get; init; } = "";
    public ConflictSeverity Severity { get; init; }
    public int Priority { get; init; }
    public string WhyTriggered { get; init; } = "";
    public string SuggestedAction { get; init; } = "";
    public string Impact { get; init; } = "";
    public IReadOnlyList<string> References { get; init; } = [];
    public string? NavigationPath { get; init; }
    public int? TimetableId { get; init; }
    public int? TimetableEntryId { get; init; }
}

public sealed class ImpactNodeDto
{
    public string NodeId { get; init; } = "";
    public ImpactCategory Category { get; init; }
    public string Label { get; init; } = "";
    public int? EntityId { get; init; }
    public ConflictSeverity Severity { get; init; }
    public string? Detail { get; init; }
}

public sealed class ImpactEdgeDto
{
    public string FromNodeId { get; init; } = "";
    public string ToNodeId { get; init; } = "";
    public string Relation { get; init; } = "";
}

public sealed class ImpactSummaryDto
{
    public int FacultyAffected { get; init; }
    public int StudentsAffected { get; init; }
    public int RoomsAffected { get; init; }
    public int DepartmentsAffected { get; init; }
    public int PublishedVersionsAffected { get; init; }
    public int WorkloadSignals { get; init; }
    public int AvailabilitySignals { get; init; }
    public int AttendanceSignals { get; init; }
    public ConflictSeverity MaxSeverity { get; init; }
    public string RiskLevel { get; init; } = "Low";
}

public sealed class ImpactGraphDto
{
    public ImpactSummaryDto Summary { get; init; } = new();
    public IReadOnlyList<ImpactNodeDto> Nodes { get; init; } = [];
    public IReadOnlyList<ImpactEdgeDto> Edges { get; init; } = [];
    public string? NavigationPath { get; init; }
    public bool IsAdvisoryOnly { get; init; } = true;
}

public sealed class DependencyNodeDto
{
    public string NodeId { get; init; } = "";
    public string RuleCode { get; init; } = "";
    public string Label { get; init; } = "";
    public ConflictSeverity Severity { get; init; }
    public int? TimetableEntryId { get; init; }
    public int? RelatedEntryId { get; init; }
    public string? NavigationPath { get; init; }
    public string? ClusterKey { get; init; }
}

public sealed class DependencyEdgeDto
{
    public string FromNodeId { get; init; } = "";
    public string ToNodeId { get; init; } = "";
    public string Relation { get; init; } = "";
    public string Reason { get; init; } = "";
}

public sealed class DependencyGraphDto
{
    public int NodeCount { get; init; }
    public int EdgeCount { get; init; }
    public int ClusterCount { get; init; }
    public int RootConflictCount { get; init; }
    public IReadOnlyList<DependencyNodeDto> Nodes { get; init; } = [];
    public IReadOnlyList<DependencyEdgeDto> Edges { get; init; } = [];
    public string Mermaid { get; init; } = "";
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Clusters { get; init; } = new Dictionary<string, IReadOnlyList<string>>();
}

public sealed class ConflictRuleThresholdDto
{
    public string ThresholdKey { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string? Description { get; init; }
    public string Unit { get; init; } = "";
    public decimal Value { get; init; }
    public int Version { get; init; }
    public string Source { get; init; } = "AppSettings";
    public bool IsActive { get; init; } = true;
}

public sealed class UpdateConflictRuleThresholdRequest
{
    public string ThresholdKey { get; set; } = "";
    public decimal Value { get; set; }
    public string? ChangeReason { get; set; }
}

public sealed class ConflictRuleConfigHistoryDto
{
    public string ThresholdKey { get; init; } = "";
    public decimal OldValue { get; init; }
    public decimal NewValue { get; init; }
    public int Version { get; init; }
    public string? ChangeReason { get; init; }
    public int? ChangedByUserId { get; init; }
    public DateTime ChangedUtc { get; init; }
}

public sealed class ConflictAnalyticsNamedCountDto
{
    public string Name { get; init; } = "";
    public int Count { get; init; }
}

public sealed class ConflictAnalyticsDashboardDto
{
    public IReadOnlyList<ConflictAnalyticsNamedCountDto> TopConflictTypes { get; init; } = [];
    public IReadOnlyList<ConflictAnalyticsNamedCountDto> MostViolatedRules { get; init; } = [];
    public IReadOnlyList<ConflictAnalyticsNamedCountDto> FacultyConflictTrends { get; init; } = [];
    public IReadOnlyList<ConflictAnalyticsNamedCountDto> RoomConflictTrends { get; init; } = [];
    public IReadOnlyList<ConflictAnalyticsNamedCountDto> DepartmentConflictTrends { get; init; } = [];
    public IReadOnlyList<ConflictTrendPointDto> WeeklyComparison { get; init; } = [];
    public IReadOnlyList<ConflictTrendPointDto> MonthlyComparison { get; init; } = [];
    public decimal ConflictResolutionRatePercent { get; init; }
    public decimal AverageResolutionTimeHours { get; init; }
    public int TotalHistoricalFindings { get; init; }
    public int TotalRuns { get; init; }
}

public sealed class ConflictWorkspaceNoteDto
{
    public int Id { get; init; }
    public int ConflictDetectionRunId { get; init; }
    public string RuleCode { get; init; } = "";
    public int? TimetableEntryId { get; init; }
    public string NoteText { get; init; } = "";
    public int UserId { get; init; }
}

public sealed class ConflictWorkspacePinDto
{
    public int Id { get; init; }
    public int ConflictDetectionRunId { get; init; }
    public string RuleCode { get; init; } = "";
    public int? TimetableEntryId { get; init; }
}

public sealed class ConflictWorkspaceBookmarkDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string FilterJson { get; init; } = "{}";
}

public sealed class UpsertConflictNoteRequest
{
    public int ConflictDetectionRunId { get; set; }
    public string RuleCode { get; set; } = "";
    public int? TimetableEntryId { get; set; }
    public string NoteText { get; set; } = "";
}

public sealed class UpsertConflictPinRequest
{
    public int ConflictDetectionRunId { get; set; }
    public string RuleCode { get; set; } = "";
    public int? TimetableEntryId { get; set; }
}

public sealed class UpsertConflictBookmarkRequest
{
    public string Name { get; set; } = "";
    public string FilterJson { get; set; } = "{}";
}

public sealed class EnhancedConflictWorkspaceDto
{
    public ConflictWorkspaceDto Workspace { get; init; } = new();
    public IReadOnlyDictionary<string, IReadOnlyList<ConflictResultDto>> GroupedByRule { get; init; } = new Dictionary<string, IReadOnlyList<ConflictResultDto>>();
    public IReadOnlyDictionary<string, IReadOnlyList<ConflictResultDto>> GroupedByDepartment { get; init; } = new Dictionary<string, IReadOnlyList<ConflictResultDto>>();
    public IReadOnlyDictionary<string, IReadOnlyList<ConflictResultDto>> GroupedByFaculty { get; init; } = new Dictionary<string, IReadOnlyList<ConflictResultDto>>();
    public IReadOnlyDictionary<string, IReadOnlyList<ConflictResultDto>> GroupedBySeverity { get; init; } = new Dictionary<string, IReadOnlyList<ConflictResultDto>>();
    public IReadOnlyDictionary<string, IReadOnlyList<ConflictResultDto>> GroupedByRoom { get; init; } = new Dictionary<string, IReadOnlyList<ConflictResultDto>>();
    public IReadOnlyList<ConflictWorkspacePinDto> Pins { get; init; } = [];
    public IReadOnlyList<ConflictWorkspaceBookmarkDto> Bookmarks { get; init; } = [];
    public IReadOnlyList<ConflictWorkspaceNoteDto> Notes { get; init; } = [];
    public DependencyGraphDto DependencyGraph { get; init; } = new();
}
