using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Domain.Entities.Scheduling;

/// <summary>
/// Persisted enterprise optimization run. Results are sandbox-bound until user approval
/// creates a new draft schedule version. Never mutates published timetables.
/// </summary>
public class OptimizationEngineRun : BaseEntity
{
    public Guid RunId { get; set; }
    public Guid SessionId { get; set; }
    public OptimizationEngineRunStatus Status { get; set; } = OptimizationEngineRunStatus.Queued;
    public OptimizationStrategyKind StrategyKind { get; set; } = OptimizationStrategyKind.Pipeline;
    public string StrategyPipelineCsv { get; set; } = "Greedy,WorkloadBalancing,RoomOptimization,PreferenceOptimization";
    public int AcademicYearId { get; set; }
    public int? DepartmentId { get; set; }
    public int? TimetableId { get; set; }
    public int? SourceScheduleVersionId { get; set; }
    public Guid? SandboxScenarioId { get; set; }
    public int? ResultDraftScheduleVersionId { get; set; }
    public decimal BaselineScore { get; set; }
    public decimal ProjectedScore { get; set; }
    public decimal ImprovementDelta { get; set; }
    public int BaselineConflictCount { get; set; }
    public int ProjectedConflictCount { get; set; }
    public string CurrentStrategy { get; set; } = "";
    public int ProgressPercent { get; set; }
    public long ElapsedMs { get; set; }
    public long? EstimatedRemainingMs { get; set; }
    public string CandidatesJson { get; set; } = "[]";
    public string ComparisonJson { get; set; } = "{}";
    public string MetricsJson { get; set; } = "{}";
    public string IntermediateResultsJson { get; set; } = "[]";
    public string? ErrorMessage { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public DateTime? ApprovedUtc { get; set; }
    public int? ApprovedByUserId { get; set; }
    public bool ModifiesProductionTimetable => false;
}
