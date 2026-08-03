using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Domain.Entities.Scheduling;

/// <summary>Persisted simulation (preview only). Never mutates live timetables in Phase 2B.6.</summary>
public class OptimizationSimulationRun : BaseEntity
{
    public Guid SimulationId { get; set; }
    public int? TimetableId { get; set; }
    public int AcademicYearId { get; set; }
    public int? DepartmentId { get; set; }
    public OptimizationStrategyKind StrategyKind { get; set; } = OptimizationStrategyKind.None;
    public OptimizationSimulationStatus Status { get; set; } = OptimizationSimulationStatus.Draft;
    public string ScenarioName { get; set; } = "Baseline";
    public decimal CurrentScore { get; set; }
    public decimal ProjectedScore { get; set; }
    public decimal ScoreDelta { get; set; }
    public int CurrentConflictCount { get; set; }
    public int ProjectedConflictCount { get; set; }
    public long ScoringTimeMs { get; set; }
    public long ExecutionTimeMs { get; set; }
    public string MetricsJson { get; set; } = "{}";
    public string ProposedChangesJson { get; set; } = "[]";
    public string? RejectionReason { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public bool AppliesTimetableChanges => false;
}

/// <summary>Independent metrics snapshot for optimization readiness (no optimizer).</summary>
public class OptimizationMetricSnapshot : BaseEntity
{
    public Guid SnapshotId { get; set; }
    public int? TimetableId { get; set; }
    public int AcademicYearId { get; set; }
    public int? DepartmentId { get; set; }
    public OptimizationMetricKind MetricKind { get; set; }
    public string MetricName { get; set; } = null!;
    public decimal Value { get; set; }
    public string Unit { get; set; } = "score";
    public DateTime CapturedUtc { get; set; }
}

/// <summary>Aggregate telemetry counters (no PII).</summary>
public class OptimizationTelemetryAggregate : BaseEntity
{
    public string MetricKey { get; set; } = null!;
    public long CounterValue { get; set; }
    public decimal AverageValue { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
}
