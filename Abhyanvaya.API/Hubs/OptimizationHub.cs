using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Abhyanvaya.API.Hubs;

[Authorize]
public sealed class OptimizationHub : Hub
{
    public Task SubscribeRun(Guid runId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, OptimizationSignalRGroups.Run(runId));

    public Task SubscribeTenant(int tenantId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, OptimizationSignalRGroups.Tenant(tenantId));
}

public static class OptimizationSignalRGroups
{
    public static string Run(Guid runId) => $"optimization-run:{runId:D}";
    public static string Tenant(int tenantId) => $"optimization-tenant:{tenantId}";
}
