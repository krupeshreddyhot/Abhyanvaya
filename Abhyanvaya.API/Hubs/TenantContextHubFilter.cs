using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Abhyanvaya.API.Hubs;

/// <summary>
/// SignalR hub invocations do not re-run HTTP middleware; load operational tenant context per hub call.
/// </summary>
public sealed class TenantContextHubFilter : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        if (invocationContext.Context.User.Identity?.IsAuthenticated == true)
        {
            var tenantContextService = invocationContext.ServiceProvider.GetRequiredService<ITenantContextService>();
            await tenantContextService.GetCurrentContextAsync(invocationContext.Hub.Context.ConnectionAborted);
            await tenantContextService.ApplyOperationalTenantAsync(invocationContext.Hub.Context.ConnectionAborted);
        }

        return await next(invocationContext);
    }
}
