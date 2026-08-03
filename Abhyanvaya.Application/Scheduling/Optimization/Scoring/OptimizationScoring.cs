using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling.Optimization.Scoring;

public sealed class OptimizationWeight
{
    public OptimizationDimension Dimension { get; init; }
    public decimal Weight { get; init; }
}

public sealed class OptimizationMetric
{
    public OptimizationMetricKind Kind { get; init; }
    public required string Name { get; init; }
    public decimal Value { get; init; }
    public string Unit { get; init; } = "score";
}

public sealed class OptimizationScoreSummary
{
    public required OptimizationScore Score { get; init; }
    public IReadOnlyList<OptimizationMetric> SupportingMetrics { get; init; } = [];
    public string Notes { get; init; } = "Scoring is independent of optimization strategies.";
}

public interface IOptimizationScoreCalculator
{
    OptimizationScoreSummary Calculate(OptimizationContext context, IReadOnlyList<OptimizationWeight>? weights = null);
}

/// <summary>
/// Pure scoring engine. Does not know how optimizers work; strategies consume scores.
/// </summary>
public sealed class OptimizationScoreCalculator : IOptimizationScoreCalculator
{
    public static IReadOnlyList<OptimizationWeight> DefaultWeights { get; } =
    [
        new() { Dimension = OptimizationDimension.FacultySatisfaction, Weight = 0.15m },
        new() { Dimension = OptimizationDimension.RoomUtilization, Weight = 0.15m },
        new() { Dimension = OptimizationDimension.TravelReduction, Weight = 0.10m },
        new() { Dimension = OptimizationDimension.WorkloadBalance, Weight = 0.15m },
        new() { Dimension = OptimizationDimension.PreferenceSatisfaction, Weight = 0.15m },
        new() { Dimension = OptimizationDimension.ConflictReduction, Weight = 0.20m },
        new() { Dimension = OptimizationDimension.StudentConvenience, Weight = 0.10m },
    ];

    public OptimizationScoreSummary Calculate(OptimizationContext context, IReadOnlyList<OptimizationWeight>? weights = null)
    {
        var w = weights ?? DefaultWeights;
        var metrics = BuildMetrics(context);
        var byName = metrics.ToDictionary(m => m.Kind, m => m.Value);

        var dimensions = w.Select(weight =>
        {
            var raw = ResolveDimension(weight.Dimension, byName, context);
            return new OptimizationDimensionScore
            {
                Dimension = weight.Dimension,
                RawValue = raw,
                Weight = weight.Weight,
                WeightedScore = Math.Round(raw * weight.Weight, 4)
            };
        }).ToList();

        var total = dimensions.Sum(d => d.WeightedScore);
        var normalized = Math.Clamp(total, 0m, 100m);

        return new OptimizationScoreSummary
        {
            Score = new OptimizationScore
            {
                TotalScore = Math.Round(total, 4),
                NormalizedScore = Math.Round(normalized, 4),
                Dimensions = dimensions
            },
            SupportingMetrics = metrics,
            Notes = "Scoring independent of optimization. No timetable mutation."
        };
    }

    private static IReadOnlyList<OptimizationMetric> BuildMetrics(OptimizationContext context)
    {
        var entryCount = Math.Max(context.EntryCount, 1);
        var conflictDensity = Math.Round((decimal)context.ConflictCount / entryCount * 100m, 2);
        var facultyUtil = context.BaselineMetrics.GetValueOrDefault("FacultyUtilization", 70m);
        var roomUtil = context.BaselineMetrics.GetValueOrDefault("RoomUtilization", 65m);
        var travel = context.BaselineMetrics.GetValueOrDefault("AverageTravel", 20m);
        var avgBreak = context.BaselineMetrics.GetValueOrDefault("AverageBreak", 15m);
        var workload = context.BaselineMetrics.GetValueOrDefault("WorkloadBalance", 60m);
        var preference = context.BaselineMetrics.GetValueOrDefault("PreferenceSatisfaction", 55m);
        var idle = context.BaselineMetrics.GetValueOrDefault("IdlePeriods", 10m);

        return
        [
            new OptimizationMetric { Kind = OptimizationMetricKind.FacultyUtilization, Name = "Faculty Utilization", Value = facultyUtil, Unit = "%" },
            new OptimizationMetric { Kind = OptimizationMetricKind.RoomUtilization, Name = "Room Utilization", Value = roomUtil, Unit = "%" },
            new OptimizationMetric { Kind = OptimizationMetricKind.AverageTravel, Name = "Average Travel", Value = travel, Unit = "minutes" },
            new OptimizationMetric { Kind = OptimizationMetricKind.ConflictDensity, Name = "Conflict Density", Value = conflictDensity, Unit = "%" },
            new OptimizationMetric { Kind = OptimizationMetricKind.AverageBreak, Name = "Average Break", Value = avgBreak, Unit = "minutes" },
            new OptimizationMetric { Kind = OptimizationMetricKind.WorkloadBalance, Name = "Workload Balance", Value = workload, Unit = "score" },
            new OptimizationMetric { Kind = OptimizationMetricKind.PreferenceSatisfaction, Name = "Preference Satisfaction", Value = preference, Unit = "score" },
            new OptimizationMetric { Kind = OptimizationMetricKind.IdlePeriods, Name = "Idle Periods", Value = idle, Unit = "count" },
        ];
    }

    private static decimal ResolveDimension(
        OptimizationDimension dimension,
        IReadOnlyDictionary<OptimizationMetricKind, decimal> metrics,
        OptimizationContext context)
    {
        return dimension switch
        {
            OptimizationDimension.FacultySatisfaction => metrics[OptimizationMetricKind.FacultyUtilization],
            OptimizationDimension.RoomUtilization => metrics[OptimizationMetricKind.RoomUtilization],
            OptimizationDimension.TravelReduction => Math.Max(0, 100m - metrics[OptimizationMetricKind.AverageTravel]),
            OptimizationDimension.WorkloadBalance => metrics[OptimizationMetricKind.WorkloadBalance],
            OptimizationDimension.PreferenceSatisfaction => metrics[OptimizationMetricKind.PreferenceSatisfaction],
            OptimizationDimension.ConflictReduction => Math.Max(0, 100m - metrics[OptimizationMetricKind.ConflictDensity]),
            OptimizationDimension.StudentConvenience => Math.Max(0, 100m - metrics[OptimizationMetricKind.IdlePeriods]),
            _ => 50m
        };
    }
}
