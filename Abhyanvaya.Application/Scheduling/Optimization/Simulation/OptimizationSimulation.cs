using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling.Optimization.Simulation;

public sealed class SimulationScenario
{
    public Guid ScenarioId { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }
    public OptimizationStrategyKind StrategyKind { get; init; } = OptimizationStrategyKind.None;
    public int? TimetableId { get; init; }
    public int AcademicYearId { get; init; }
    public int? DepartmentId { get; init; }
    public IReadOnlyList<string> ProposedChangeSummaries { get; init; } = [];
    public bool PreviewOnly => true;
}

public sealed class SimulationSummary
{
    public Guid SimulationId { get; init; }
    public OptimizationSimulationStatus Status { get; init; }
    public decimal CurrentScore { get; init; }
    public decimal ProjectedScore { get; init; }
    public decimal ScoreDelta { get; init; }
    public int CurrentConflictCount { get; init; }
    public int ProjectedConflictCount { get; init; }
    public string Message { get; init; } = "";
    public bool CanApply => false;
}

public sealed class SimulationResult
{
    public required SimulationScenario Scenario { get; init; }
    public required SimulationSummary Summary { get; init; }
    public required OptimizationScore BaselineScore { get; init; }
    public required OptimizationScore ProjectedScore { get; init; }
    public IReadOnlyList<Optimization.Scoring.OptimizationMetric> Metrics { get; init; } = [];
    public IReadOnlyList<OptimizationCandidate> Candidates { get; init; } = [];
    public bool ModifiesTimetable => false;
}
