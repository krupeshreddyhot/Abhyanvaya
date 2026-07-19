using Abhyanvaya.Application.Common.Interfaces;

namespace Abhyanvaya.API.Middleware;

/// <summary>
/// Loads operational tenant context per request and binds effective tenant to the ambient accessor.
/// </summary>
public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContextService tenantContextService)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            await tenantContextService.GetCurrentContextAsync(context.RequestAborted);
            await tenantContextService.ApplyOperationalTenantAsync(context.RequestAborted);
        }

        await _next(context);
    }
}

public static class TenantContextMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantContext(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantContextMiddleware>();
}
