using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling.Optimization.Engine;

public sealed class OptimizationSession
{
    public Guid SessionId { get; init; } = Guid.NewGuid();
    public Guid RunId { get; init; } = Guid.NewGuid();
    public DateTime StartedUtc { get; init; } = DateTime.UtcNow;
    public int TenantId { get; init; }
    public int AcademicYearId { get; init; }
    public int? TimetableId { get; init; }
    public int? DepartmentId { get; init; }
}

public sealed class OptimizationProgress
{
    public Guid RunId { get; init; }
    public Guid SessionId { get; init; }
    public string CurrentStrategy { get; init; } = "";
    public int ProgressPercent { get; init; }
    public long ElapsedMs { get; init; }
    public long? EstimatedRemainingMs { get; init; }
    public decimal CurrentScore { get; init; }
    public decimal ImprovementDelta { get; init; }
    public string StatusMessage { get; init; } = "";
    public OptimizationEngineRunStatus Status { get; init; }
}

public sealed class OptimizationExecutionContext
{
    public required OptimizationSession Session { get; init; }
    public required OptimizationContext WorkingContext { get; set; }
    public required OptimizationRequest Request { get; init; }
    public IList<OptimizationCandidate> AccumulatedCandidates { get; } = new List<OptimizationCandidate>();
    public IList<OptimizationIntermediateResult> IntermediateResults { get; } = new List<OptimizationIntermediateResult>();
    public OptimizationScore? BaselineScore { get; set; }
    public OptimizationScore? CurrentScore { get; set; }
    public int BaselineConflictCount { get; set; }
    public Action<OptimizationProgress>? ProgressCallback { get; init; }
}

public sealed class OptimizationIntermediateResult
{
    public required string StrategyCode { get; init; }
    public required string StrategyName { get; init; }
    public OptimizationStrategyKind Kind { get; init; }
    public int CandidateCount { get; init; }
    public decimal ScoreAfter { get; init; }
    public int ConflictCountAfter { get; init; }
    public long ElapsedMs { get; init; }
    public string Message { get; init; } = "";
}

public sealed class OptimizationExecutionResult
{
    public required Guid RunId { get; init; }
    public required Guid SessionId { get; init; }
    public required OptimizationEngineRunStatus Status { get; init; }
    public required OptimizationResult CombinedResult { get; init; }
    public IReadOnlyList<OptimizationIntermediateResult> IntermediateResults { get; init; } = [];
    public Guid? SandboxScenarioId { get; set; }
    public OptimizationComparisonDto? Comparison { get; init; }
    public long ElapsedMs { get; init; }
    public string? ErrorMessage { get; init; }
    public bool ModifiesProductionTimetable => false;
}

public sealed class OptimizationComparisonDto
{
    public decimal OriginalScore { get; init; }
    public decimal OptimizedScore { get; init; }
    public decimal ScoreImprovement { get; init; }
    public int OriginalConflicts { get; init; }
    public int OptimizedConflicts { get; init; }
    public int ConflictReduction { get; init; }
    public decimal FacultySatisfactionDelta { get; init; }
    public decimal RoomUsageDelta { get; init; }
    public decimal TravelDelta { get; init; }
    public decimal BreaksDelta { get; init; }
    public IReadOnlyList<string> Highlights { get; init; } = [];
}

public interface IOptimizationEngine
{
    Task<OptimizationExecutionResult> ExecuteAsync(
        OptimizationExecutionContext context,
        CancellationToken cancellationToken = default);
}

public interface IOptimizationExecutionService
{
    Task<OptimizationExecutionResult> RunPipelineAsync(
        OptimizationRequest request,
        CancellationToken cancellationToken = default);

    Task<OptimizationExecutionResult?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OptimizationRunSummaryDto>> ListRunsAsync(
        int? academicYearId,
        int? departmentId,
        CancellationToken cancellationToken = default);
}

public sealed class OptimizationRunSummaryDto
{
    public Guid RunId { get; init; }
    public Guid SessionId { get; init; }
    public OptimizationEngineRunStatus Status { get; init; }
    public OptimizationStrategyKind StrategyKind { get; init; }
    public int AcademicYearId { get; init; }
    public int? TimetableId { get; init; }
    public decimal BaselineScore { get; init; }
    public decimal ProjectedScore { get; init; }
    public decimal ImprovementDelta { get; init; }
    public int BaselineConflictCount { get; init; }
    public int ProjectedConflictCount { get; init; }
    public Guid? SandboxScenarioId { get; init; }
    public int? ResultDraftScheduleVersionId { get; init; }
    public DateTime StartedUtc { get; init; }
    public DateTime? CompletedUtc { get; init; }
    public long ElapsedMs { get; init; }
    public bool ModifiesProductionTimetable { get; init; }
}
