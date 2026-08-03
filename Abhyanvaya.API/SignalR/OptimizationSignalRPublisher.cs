using Abhyanvaya.API.Hubs;
using Abhyanvaya.Application.Scheduling.Optimization.Engine;
using Abhyanvaya.Application.Scheduling.Optimization.Progress;
using Microsoft.AspNetCore.SignalR;

namespace Abhyanvaya.API.SignalR;

public sealed class OptimizationSignalRPublisher : IOptimizationProgressPublisher
{
    private readonly IHubContext<OptimizationHub> _hub;

    public OptimizationSignalRPublisher(IHubContext<OptimizationHub> hub) => _hub = hub;

    public Task PublishProgressAsync(int tenantId, OptimizationProgress progress, CancellationToken cancellationToken = default) =>
        Task.WhenAll(
            _hub.Clients.Group(OptimizationSignalRGroups.Run(progress.RunId))
                .SendAsync("OptimizationProgress", progress, cancellationToken),
            _hub.Clients.Group(OptimizationSignalRGroups.Tenant(tenantId))
                .SendAsync("OptimizationProgress", progress, cancellationToken));

    public Task PublishCompletedAsync(int tenantId, Guid runId, CancellationToken cancellationToken = default) =>
        Task.WhenAll(
            _hub.Clients.Group(OptimizationSignalRGroups.Run(runId))
                .SendAsync("OptimizationCompleted", new { runId, tenantId }, cancellationToken),
            _hub.Clients.Group(OptimizationSignalRGroups.Tenant(tenantId))
                .SendAsync("OptimizationCompleted", new { runId, tenantId }, cancellationToken));

    public Task PublishFailedAsync(int tenantId, Guid runId, string reason, CancellationToken cancellationToken = default) =>
        Task.WhenAll(
            _hub.Clients.Group(OptimizationSignalRGroups.Run(runId))
                .SendAsync("OptimizationFailed", new { runId, tenantId, reason }, cancellationToken),
            _hub.Clients.Group(OptimizationSignalRGroups.Tenant(tenantId))
                .SendAsync("OptimizationFailed", new { runId, tenantId, reason }, cancellationToken));
}
