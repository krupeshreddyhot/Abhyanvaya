using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.DTOs.Scheduling;

public sealed class OptimizationScoreDto
{
    public decimal TotalScore { get; init; }
    public decimal NormalizedScore { get; init; }
    public IReadOnlyList<OptimizationDimensionScoreDto> Dimensions { get; init; } = [];
}

public sealed class OptimizationDimensionScoreDto
{
    public OptimizationDimension Dimension { get; init; }
    public string DimensionName { get; init; } = "";
    public decimal RawValue { get; init; }
    public decimal Weight { get; init; }
    public decimal WeightedScore { get; init; }
}

public sealed class OptimizationMetricDto
{
    public OptimizationMetricKind MetricKind { get; init; }
    public string MetricName { get; init; } = "";
    public decimal Value { get; init; }
    public string Unit { get; init; } = "";
    public DateTime CapturedUtc { get; init; }
    public int? TimetableId { get; init; }
    public int AcademicYearId { get; init; }
}

public sealed class OptimizationCandidateDto
{
    public string CandidateId { get; init; } = "";
    public string Description { get; init; } = "";
    public IReadOnlyList<string> ProposedChangeSummaries { get; init; } = [];
    public bool IsAdvisoryOnly { get; init; } = true;
    public bool ModifiesLiveTimetable { get; init; }
}

public sealed class OptimizationSimulationDto
{
    public Guid SimulationId { get; init; }
    public string ScenarioName { get; init; } = "";
    public OptimizationStrategyKind StrategyKind { get; init; }
    public OptimizationSimulationStatus Status { get; init; }
    public decimal CurrentScore { get; init; }
    public decimal ProjectedScore { get; init; }
    public decimal ScoreDelta { get; init; }
    public int CurrentConflictCount { get; init; }
    public int ProjectedConflictCount { get; init; }
    public OptimizationScoreDto BaselineScore { get; init; } = new();
    public OptimizationScoreDto ProjectedScoreDetail { get; init; } = new();
    public IReadOnlyList<OptimizationMetricDto> Metrics { get; init; } = [];
    public IReadOnlyList<OptimizationCandidateDto> Candidates { get; init; } = [];
    public IReadOnlyList<string> ProposedChanges { get; init; } = [];
    public bool CanApply { get; init; }
    public bool ModifiesTimetable { get; init; }
    public string Message { get; init; } = "";
    public long ScoringTimeMs { get; init; }
    public long ExecutionTimeMs { get; init; }
}

public sealed class RunOptimizationSimulationRequest
{
    public int? TimetableId { get; set; }
    public int? AcademicYearId { get; set; }
    public int? DepartmentId { get; set; }
    public OptimizationStrategyKind StrategyKind { get; set; } = OptimizationStrategyKind.None;
    public string? ScenarioName { get; set; }
}

public sealed class CompareSimulationsRequest
{
    public Guid LeftSimulationId { get; set; }
    public Guid RightSimulationId { get; set; }
}

public sealed class SimulationComparisonDto
{
    public OptimizationSimulationDto Left { get; init; } = new();
    public OptimizationSimulationDto Right { get; init; } = new();
    public decimal ScoreDelta { get; init; }
    public int ConflictDelta { get; init; }
    public string Recommendation { get; init; } = "Preview only — no apply.";
}

public sealed class OptimizationPreviewDto
{
    public OptimizationSimulationDto Simulation { get; init; } = new();
    public ConflictDashboardDto? ConflictSnapshot { get; init; }
    public IReadOnlyList<HeatMapDto> HeatMaps { get; init; } = [];
    public OptimizationTelemetryDto Telemetry { get; init; } = new();
    public bool ShowApplyButton => false;
}

public sealed class OptimizationTelemetryDto
{
    public long SimulationCount { get; init; }
    public long ExecutionTimeMs { get; init; }
    public long ScoringTimeMs { get; init; }
    public decimal AverageImprovement { get; init; }
    public long RejectedSimulations { get; init; }
    public long AcceptedSimulations { get; init; }
    public IReadOnlyList<OptimizationNamedCountDto> MostUsedMetrics { get; init; } = [];
}

public sealed class OptimizationNamedCountDto
{
    public string Name { get; init; } = "";
    public int Count { get; init; }
}

public sealed class OptimizationPluginDto
{
    public string Category { get; init; } = "";
    public string ProviderCode { get; init; } = "";
    public string ProviderName { get; init; } = "";
    public bool IsImplemented { get; init; }
    public string Notes { get; init; } = "";
}

public sealed class RejectSimulationRequest
{
    public Guid SimulationId { get; set; }
    public string? Reason { get; set; }
}

public sealed class AcceptSimulationRequest
{
    public Guid SimulationId { get; set; }
    /// <summary>Accepted for future Phase 3 apply pipeline only. Never mutates timetable in 2B.6.</summary>
    public bool AcknowledgePreviewOnly { get; set; } = true;
}
