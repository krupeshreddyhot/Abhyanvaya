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
            var httpContext = invocationContext.Context.GetHttpContext();
            if (httpContext is not null
                && invocationContext.Context.User.Identity.IsAuthenticated
                && httpContext.User?.Identity?.IsAuthenticated != true)
            {
                httpContext.User = invocationContext.Context.User;
            }

            var tenantContextService = invocationContext.ServiceProvider.GetRequiredService<ITenantContextService>();
            await tenantContextService.GetCurrentContextAsync(invocationContext.Context.ConnectionAborted);
            await tenantContextService.ApplyOperationalTenantAsync(invocationContext.Context.ConnectionAborted);
        }

        return await next(invocationContext);
    }
}
