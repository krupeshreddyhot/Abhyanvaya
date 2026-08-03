using Abhyanvaya.Application.Scheduling.Optimization.Engine;

namespace Abhyanvaya.Application.Scheduling.Optimization.Progress;

public interface IOptimizationProgressPublisher
{
    Task PublishProgressAsync(int tenantId, OptimizationProgress progress, CancellationToken cancellationToken = default);
    Task PublishCompletedAsync(int tenantId, Guid runId, CancellationToken cancellationToken = default);
    Task PublishFailedAsync(int tenantId, Guid runId, string reason, CancellationToken cancellationToken = default);
}

public sealed class NoOpOptimizationProgressPublisher : IOptimizationProgressPublisher
{
    public Task PublishProgressAsync(int tenantId, OptimizationProgress progress, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task PublishCompletedAsync(int tenantId, Guid runId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task PublishFailedAsync(int tenantId, Guid runId, string reason, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
