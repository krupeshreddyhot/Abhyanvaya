using Abhyanvaya.Application.Academic.Allocation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Abhyanvaya.API.Hubs;

[Authorize]
public sealed class AllocationHub : Hub
{
    public Task SubscribeRun(Guid sessionId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, AllocationSignalRGroups.Run(sessionId));

    public Task SubscribeTenant(int tenantId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, AllocationSignalRGroups.Tenant(tenantId));
}

public static class AllocationSignalRGroups
{
    public static string Run(Guid sessionId) => $"allocation-run:{sessionId:D}";
    public static string Tenant(int tenantId) => $"allocation-tenant:{tenantId}";
}

public sealed class AllocationSignalRPublisher : IAllocationProgressPublisher
{
    private readonly IHubContext<AllocationHub> _hub;

    public AllocationSignalRPublisher(IHubContext<AllocationHub> hub) => _hub = hub;

    public Task PublishProgressAsync(int tenantId, AllocationProgress progress, CancellationToken cancellationToken = default)
        => _hub.Clients.Groups(AllocationSignalRGroups.Tenant(tenantId), AllocationSignalRGroups.Run(progress.SessionId))
            .SendAsync("AllocationProgress", progress, cancellationToken);

    public Task PublishCompletedAsync(int tenantId, AllocationExecutionResult result, CancellationToken cancellationToken = default)
        => _hub.Clients.Groups(AllocationSignalRGroups.Tenant(tenantId), AllocationSignalRGroups.Run(result.SessionId))
            .SendAsync("AllocationCompleted", result, cancellationToken);

    public Task PublishFailedAsync(int tenantId, Guid sessionId, string message, CancellationToken cancellationToken = default)
        => _hub.Clients.Groups(AllocationSignalRGroups.Tenant(tenantId), AllocationSignalRGroups.Run(sessionId))
            .SendAsync("AllocationFailed", new { sessionId, message }, cancellationToken);
}
