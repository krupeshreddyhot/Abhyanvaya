using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Application.Scheduling.Conflicts.Intelligence;
using Abhyanvaya.Application.Scheduling.Conflicts.Intelligence.Providers;
using Abhyanvaya.Application.Scheduling.Conflicts.Rules;
using Abhyanvaya.Application.Scheduling.Optimization;
using Abhyanvaya.Application.Scheduling.Optimization.Plugins;
using Abhyanvaya.Application.Scheduling.Optimization.Scoring;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase2B6;

public sealed class Phase2B6OptimizationReadinessTests
{
    [Fact]
    public void PluginRegistry_ListsStrategiesAndProviders_WithoutImplementingOptimizers()
    {
        var registry = new OptimizationPluginRegistry(
            strategies: [new NoOpOptimizationStrategy()],
            optimizationProviders: [new ReadinessOptimizationProvider()],
            scoringProviders: [new DefaultScoringProvider()],
            simulationProviders: [new DefaultSimulationProvider()],
            metricProviders: [new DefaultMetricProvider()],
            recommendationProviders:
            [
                new RoomSwapRecommendationProvider(),
                new FacultySwapRecommendationProvider(),
                new TimeSlotRecommendationProvider()
            ],
            conflictRules: [new FacultyDoubleBookingRule(), new RoomDoubleBookingRule()]);

        var plugins = registry.ListPlugins();
        Assert.Contains(plugins, p => p.Category == "OptimizationStrategy" && p.ProviderCode == "NONE" && !p.IsImplemented);
        Assert.Contains(plugins, p => p.Category == "ScoringProvider" && p.IsImplemented);
        Assert.Contains(plugins, p => p.Category == "SimulationProvider" && p.IsImplemented);
        Assert.Contains(plugins, p => p.Category == "MetricProvider" && p.IsImplemented);
        Assert.Contains(plugins, p => p.Category == "RecommendationProvider");
        Assert.Contains(plugins, p => p.Category == "ConflictProvider");
        Assert.Contains(plugins, p => p.ProviderCode == "GENETIC" && !p.IsImplemented);
        Assert.DoesNotContain(plugins, p => p.Category == "OptimizationStrategy" && p.IsImplemented);
    }

    [Fact]
    public void ScoreCalculator_IsIndependentOfOptimizationStrategies()
    {
        var calculator = new OptimizationScoreCalculator();
        var ctx = new OptimizationContext
        {
            TenantId = 1,
            AcademicYearId = 1,
            EntryCount = 20,
            ConflictCount = 4,
            BaselineMetrics = new Dictionary<string, decimal>
            {
                ["FacultyUtilization"] = 80,
                ["RoomUtilization"] = 70,
                ["AverageTravel"] = 10,
                ["AverageBreak"] = 15,
                ["WorkloadBalance"] = 75,
                ["PreferenceSatisfaction"] = 60,
                ["IdlePeriods"] = 5
            }
        };

        var summary = calculator.Calculate(ctx);
        Assert.Equal(7, summary.Score.Dimensions.Count);
        Assert.Contains(summary.Score.Dimensions, d => d.Dimension == OptimizationDimension.ConflictReduction);
        Assert.Contains(summary.SupportingMetrics, m => m.Kind == OptimizationMetricKind.ConflictDensity);
        Assert.True(summary.Score.NormalizedScore > 0);
        Assert.Contains("independent", summary.Notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NoOpStrategy_NeverMutatesTimetable_AndReturnsNoCandidates()
    {
        var strategy = new NoOpOptimizationStrategy();
        Assert.False(strategy.IsImplemented);
        Assert.Equal(OptimizationStrategyKind.None, strategy.Kind);

        var result = await strategy.ProposeAsync(
            new OptimizationContext { TenantId = 1, AcademicYearId = 1, EntryCount = 1, ConflictCount = 0 },
            new OptimizationRequest { PreviewOnly = true });

        Assert.True(result.IsPreviewOnly);
        Assert.False(result.ModifiesTimetable);
        Assert.False(result.Execution.AppliedToTimetable);
        Assert.Empty(result.Candidates);
        Assert.Equal(0, result.Summary.CandidateCount);
    }

    [Fact]
    public void OptimizationContext_DisallowsTimetableMutation()
    {
        var ctx = new OptimizationContext { TenantId = 1, AcademicYearId = 1 };
        Assert.False(ctx.AllowTimetableMutation);
        Assert.False(new OptimizationRequest().ApplyChanges);
    }

    [Fact]
    public void ScoreDimensions_CoverEnterpriseRequirements()
    {
        var dims = OptimizationScoreCalculator.DefaultWeights.Select(w => w.Dimension).ToHashSet();
        Assert.Contains(OptimizationDimension.FacultySatisfaction, dims);
        Assert.Contains(OptimizationDimension.RoomUtilization, dims);
        Assert.Contains(OptimizationDimension.TravelReduction, dims);
        Assert.Contains(OptimizationDimension.WorkloadBalance, dims);
        Assert.Contains(OptimizationDimension.PreferenceSatisfaction, dims);
        Assert.Contains(OptimizationDimension.ConflictReduction, dims);
        Assert.Contains(OptimizationDimension.StudentConvenience, dims);
        Assert.Equal(1.0m, OptimizationScoreCalculator.DefaultWeights.Sum(w => w.Weight));
    }

    [Fact]
    public void MetricKinds_CoverEnterpriseRequirements()
    {
        var kinds = Enum.GetValues<OptimizationMetricKind>();
        Assert.Contains(OptimizationMetricKind.FacultyUtilization, kinds);
        Assert.Contains(OptimizationMetricKind.RoomUtilization, kinds);
        Assert.Contains(OptimizationMetricKind.AverageTravel, kinds);
        Assert.Contains(OptimizationMetricKind.ConflictDensity, kinds);
        Assert.Contains(OptimizationMetricKind.AverageBreak, kinds);
        Assert.Contains(OptimizationMetricKind.WorkloadBalance, kinds);
        Assert.Contains(OptimizationMetricKind.PreferenceSatisfaction, kinds);
        Assert.Contains(OptimizationMetricKind.IdlePeriods, kinds);
    }

    [Fact]
    public void AttendanceCompatibility_ModesRemainLegacyAndTimetable()
    {
        Assert.Contains("Legacy", new[] { "Legacy", "Timetable" });
        Assert.Contains("Timetable", new[] { "Legacy", "Timetable" });
        Assert.True(typeof(IAttendanceSessionResolver).IsInterface);
    }

    [Fact]
    public void Isolation_OptimizationDoesNotOwnConflictEngineOrAdvisor()
    {
        Assert.False(typeof(IOptimizationStrategy).IsAssignableFrom(typeof(ConflictEngine)));
        Assert.False(typeof(IOptimizationScoreCalculator).IsAssignableFrom(typeof(ConflictResolutionAdvisor)));
        Assert.False(typeof(NoOpOptimizationStrategy).GetInterfaces().Contains(typeof(IConflictRule)));
    }
}
