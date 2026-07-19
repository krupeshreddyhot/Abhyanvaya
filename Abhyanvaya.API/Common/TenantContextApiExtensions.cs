using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.TenantContext;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Common;

public static class TenantContextApiExtensions
{
    public static ActionResult? RequireTenantContext(
        this ControllerBase controller,
        ITenantContextService tenantContextService,
        out TenantContextResolution resolution)
    {
        resolution = tenantContextService.ResolveForOperation();
        if (resolution.IsResolved)
        {
            return null;
        }

        return controller.BadRequest(new
        {
            errorCode = resolution.ErrorCode ?? "ContextRequired",
            message = resolution.Message ?? "A college context is required for this operation.",
        });
    }
}
