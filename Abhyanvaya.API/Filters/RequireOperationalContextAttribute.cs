using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.TenantContext;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Abhyanvaya.API.Filters;

/// <summary>
/// Ensures an operational college context is resolved before the action executes.
/// College admins proceed immediately; SuperAdmin without context receives 400 ContextRequired.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireOperationalContextAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var tenantContextService = context.HttpContext.RequestServices.GetRequiredService<ITenantContextService>();
        var resolution = tenantContextService.ResolveForOperation();

        if (!resolution.IsResolved)
        {
            context.Result = new BadRequestObjectResult(new
            {
                errorCode = resolution.ErrorCode ?? "ContextRequired",
                message = resolution.Message ?? "A college context is required for this operation.",
            });
            return;
        }

        context.HttpContext.Items["TenantContextResolution"] = resolution;
        await next();
    }
}
