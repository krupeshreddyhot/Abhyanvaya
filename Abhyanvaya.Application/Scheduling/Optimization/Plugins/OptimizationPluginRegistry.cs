using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Application.Scheduling.Conflicts.Intelligence;
using Abhyanvaya.Application.Scheduling.Optimization.Metrics;
using Abhyanvaya.Application.Scheduling.Optimization.Scoring;
using Abhyanvaya.Application.Scheduling.Optimization.Simulation;
using Abhyanvaya.Application.Scheduling.Optimization.Telemetry;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling.Optimization.Plugins;

public interface IOptimizationProvider
{
    string ProviderCode { get; }
    string ProviderName { get; }
    OptimizationStrategyKind StrategyKind { get; }
    bool IsImplemented { get; }
}

public interface IScoringProvider
{
    string ProviderCode { get; }
    bool IsImplemented { get; }
}

public interface ISimulationProvider
{
    string ProviderCode { get; }
    bool IsImplemented { get; }
}

public interface IMetricProvider
{
    string ProviderCode { get; }
    bool IsImplemented { get; }
}

public interface IRecommendationProviderMarker
{
    string ProviderCode { get; }
}

public interface IConflictProviderMarker
{
    string ProviderCode { get; }
}

public sealed class OptimizationPluginDescriptor
{
    public required string Category { get; init; }
    public required string ProviderCode { get; init; }
    public required string ProviderName { get; init; }
    public bool IsImplemented { get; init; }
    public string Notes { get; init; } = "";
}

public interface IOptimizationPluginRegistry
{
    IReadOnlyList<OptimizationPluginDescriptor> ListPlugins();
    IReadOnlyList<IOptimizationStrategy> ListStrategies();
}

/// <summary>
/// Registration surface only. Phase 2B.6 does not ship optimizer implementations.
/// Conflict/recommendation providers are discovered for isolation verification.
/// </summary>
public sealed class OptimizationPluginRegistry : IOptimizationPluginRegistry
{
    private readonly IEnumerable<IOptimizationStrategy> _strategies;
    private readonly IEnumerable<IOptimizationProvider> _optimizationProviders;
    private readonly IEnumerable<IScoringProvider> _scoringProviders;
    private readonly IEnumerable<ISimulationProvider> _simulationProviders;
    private readonly IEnumerable<IMetricProvider> _metricProviders;
    private readonly IEnumerable<IConflictRecommendationProvider> _recommendationProviders;
    private readonly IEnumerable<IConflictRule> _conflictRules;

    public OptimizationPluginRegistry(
        IEnumerable<IOptimizationStrategy> strategies,
        IEnumerable<IOptimizationProvider> optimizationProviders,
        IEnumerable<IScoringProvider> scoringProviders,
        IEnumerable<ISimulationProvider> simulationProviders,
        IEnumerable<IMetricProvider> metricProviders,
        IEnumerable<IConflictRecommendationProvider> recommendationProviders,
        IEnumerable<IConflictRule> conflictRules)
    {
        _strategies = strategies;
        _optimizationProviders = optimizationProviders;
        _scoringProviders = scoringProviders;
        _simulationProviders = simulationProviders;
        _metricProviders = metricProviders;
        _recommendationProviders = recommendationProviders;
        _conflictRules = conflictRules;
    }

    public IReadOnlyList<IOptimizationStrategy> ListStrategies() => _strategies.ToList();

    public IReadOnlyList<OptimizationPluginDescriptor> ListPlugins()
    {
        var list = new List<OptimizationPluginDescriptor>();

        list.AddRange(_strategies.Select(s => new OptimizationPluginDescriptor
        {
            Category = "OptimizationStrategy",
            ProviderCode = s.StrategyCode,
            ProviderName = s.StrategyName,
            IsImplemented = s.IsImplemented,
            Notes = s.IsImplemented ? "" : "Contract only — Phase 3"
        }));

        list.AddRange(_optimizationProviders.Select(p => new OptimizationPluginDescriptor
        {
            Category = "OptimizationProvider",
            ProviderCode = p.ProviderCode,
            ProviderName = p.ProviderName,
            IsImplemented = p.IsImplemented
        }));

        list.AddRange(_scoringProviders.Select(p => new OptimizationPluginDescriptor
        {
            Category = "ScoringProvider",
            ProviderCode = p.ProviderCode,
            ProviderName = p.ProviderCode,
            IsImplemented = p.IsImplemented
        }));

        list.AddRange(_simulationProviders.Select(p => new OptimizationPluginDescriptor
        {
            Category = "SimulationProvider",
            ProviderCode = p.ProviderCode,
            ProviderName = p.ProviderCode,
            IsImplemented = p.IsImplemented
        }));

        list.AddRange(_metricProviders.Select(p => new OptimizationPluginDescriptor
        {
            Category = "MetricProvider",
            ProviderCode = p.ProviderCode,
            ProviderName = p.ProviderCode,
            IsImplemented = p.IsImplemented
        }));

        list.AddRange(_recommendationProviders.Select(p => new OptimizationPluginDescriptor
        {
            Category = "RecommendationProvider",
            ProviderCode = p.ProviderCode,
            ProviderName = p.ProviderCode,
            IsImplemented = true,
            Notes = "Conflict intelligence (2B.5) — advisory only"
        }));

        list.AddRange(_conflictRules.Select(r => new OptimizationPluginDescriptor
        {
            Category = "ConflictProvider",
            ProviderCode = r.RuleCode,
            ProviderName = r.RuleName,
            IsImplemented = true,
            Notes = "Conflict detection (2B) — detect only"
        }));

        // Future strategy placeholders (not yet implemented)
        foreach (var kind in new[]
                 {
                     OptimizationStrategyKind.Genetic,
                     OptimizationStrategyKind.SimulatedAnnealing,
                     OptimizationStrategyKind.TabuSearch,
                     OptimizationStrategyKind.AiAssisted
                 })
        {
            if (list.Any(x => x.Category == "OptimizationStrategy" &&
                              x.ProviderCode.Equals(kind.ToString(), StringComparison.OrdinalIgnoreCase)))
                continue;
            list.Add(new OptimizationPluginDescriptor
            {
                Category = "OptimizationStrategy",
                ProviderCode = kind.ToString().ToUpperInvariant(),
                ProviderName = kind.ToString(),
                IsImplemented = false,
                Notes = "Reserved for later phases — implement IOptimizationStrategy to plug in"
            });
        }

        return list;
    }
}

public sealed class DefaultScoringProvider : IScoringProvider
{
    public string ProviderCode => "DEFAULT_SCORING";
    public bool IsImplemented => true;
}

public sealed class DefaultSimulationProvider : ISimulationProvider
{
    public string ProviderCode => "DEFAULT_SIMULATION";
    public bool IsImplemented => true;
}

public sealed class DefaultMetricProvider : IMetricProvider
{
    public string ProviderCode => "DEFAULT_METRICS";
    public bool IsImplemented => true;
}

public sealed class ReadinessOptimizationProvider : IOptimizationProvider
{
    public string ProviderCode => "READINESS";
    public string ProviderName => "Optimization Readiness Provider";
    public OptimizationStrategyKind StrategyKind => OptimizationStrategyKind.None;
    public bool IsImplemented => false;
}

public sealed class PipelineOptimizationProvider : IOptimizationProvider
{
    public string ProviderCode => "PIPELINE";
    public string ProviderName => "Enterprise Optimization Pipeline";
    public OptimizationStrategyKind StrategyKind => OptimizationStrategyKind.Pipeline;
    public bool IsImplemented => true;
}
