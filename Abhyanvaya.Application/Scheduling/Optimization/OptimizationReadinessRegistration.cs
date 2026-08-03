using Abhyanvaya.Application.Scheduling.Optimization.Approval;
using Abhyanvaya.Application.Scheduling.Optimization.Dashboard;
using Abhyanvaya.Application.Scheduling.Optimization.Engine;
using Abhyanvaya.Application.Scheduling.Optimization.Metrics;
using Abhyanvaya.Application.Scheduling.Optimization.Pipeline;
using Abhyanvaya.Application.Scheduling.Optimization.Plugins;
using Abhyanvaya.Application.Scheduling.Optimization.Progress;
using Abhyanvaya.Application.Scheduling.Optimization.Sandbox;
using Abhyanvaya.Application.Scheduling.Optimization.Scoring;
using Abhyanvaya.Application.Scheduling.Optimization.Simulation;
using Abhyanvaya.Application.Scheduling.Optimization.Strategies;
using Abhyanvaya.Application.Scheduling.Optimization.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace Abhyanvaya.Application.Scheduling.Optimization;

public static class OptimizationReadinessRegistration
{
    public static IServiceCollection AddOptimizationReadiness(this IServiceCollection services)
    {
        // Strategy pattern — Phase 3 registers real IOptimizationStrategy implementations (pluggable).
        services.AddScoped<IOptimizationStrategy, NoOpOptimizationStrategy>();
        services.AddScoped<IOptimizationStrategy, GreedyOptimizationStrategy>();
        services.AddScoped<IOptimizationStrategy, FacultyWorkloadOptimizationStrategy>();
        services.AddScoped<IOptimizationStrategy, RoomOptimizationStrategy>();
        services.AddScoped<IOptimizationStrategy, PreferenceOptimizationStrategy>();

        services.AddScoped<IOptimizationScoreCalculator, OptimizationScoreCalculator>();
        services.AddScoped<IOptimizationMetricsService, OptimizationMetricsService>();
        services.AddScoped<IOptimizationTelemetryService, OptimizationTelemetryService>();
        services.AddScoped<IOptimizationSimulationService, OptimizationSimulationService>();
        services.AddScoped<IOptimizationReadinessService, OptimizationReadinessService>();

        services.AddScoped<IOptimizationProvider, ReadinessOptimizationProvider>();
        services.AddScoped<IOptimizationProvider, PipelineOptimizationProvider>();
        services.AddScoped<IScoringProvider, DefaultScoringProvider>();
        services.AddScoped<ISimulationProvider, DefaultSimulationProvider>();
        services.AddScoped<IMetricProvider, DefaultMetricProvider>();
        services.AddScoped<IOptimizationPluginRegistry, OptimizationPluginRegistry>();

        // AI30 Phase 2B.7 — Optimization Sandbox (no production edits)
        services.AddScoped<IScenarioHistoryService, ScenarioHistoryService>();
        services.AddScoped<IReplayService, ReplayService>();
        services.AddScoped<IScenarioComparisonService, ScenarioComparisonService>();
        services.AddScoped<IScenarioCollaborationService, ScenarioCollaborationService>();
        services.AddScoped<IMetricsEvolutionService, MetricsEvolutionService>();
        services.AddScoped<ISandboxService, SandboxService>();

        // AI30 Phase 3 — Enterprise Optimization Engine
        services.AddScoped<IOptimizationContextBuilder, OptimizationContextBuilder>();
        services.AddScoped<IOptimizationPipeline, OptimizationPipeline>();
        services.AddScoped<IOptimizationEngine, OptimizationEngine>();
        services.AddScoped<IOptimizationExecutionService, OptimizationExecutionService>();
        services.AddScoped<IOptimizationApprovalService, OptimizationApprovalService>();
        services.AddScoped<IOptimizationDashboardService, OptimizationDashboardService>();
        services.AddScoped<IOptimizationProgressPublisher, NoOpOptimizationProgressPublisher>();

        return services;
    }
}
