namespace Abhyanvaya.Application.Scheduling.Optimization.Engine;

using Abhyanvaya.Application.Scheduling.Optimization.Pipeline;

/// <summary>
/// Executes registered optimization strategies through the enterprise pipeline.
/// Never edits production timetables.
/// </summary>
public sealed class OptimizationEngine : IOptimizationEngine
{
    private readonly IOptimizationPipeline _pipeline;

    public OptimizationEngine(IOptimizationPipeline pipeline) => _pipeline = pipeline;

    public Task<OptimizationExecutionResult> ExecuteAsync(
        OptimizationExecutionContext context,
        CancellationToken cancellationToken = default) =>
        _pipeline.RunAsync(context, cancellationToken);
}
