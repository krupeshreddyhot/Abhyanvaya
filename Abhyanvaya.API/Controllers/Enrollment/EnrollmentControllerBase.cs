using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.TenantContext;
using Abhyanvaya.API.Common;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers.Enrollment;

public abstract class EnrollmentControllerBase : ControllerBase
{
    protected ActionResult? RequireTenantContext(
        ITenantContextService tenantContextService,
        out TenantContextResolution resolution) =>
        TenantContextApiExtensions.RequireTenantContext(this, tenantContextService, out resolution);

    protected static (int TenantId, int UserId, int? CollegeId) MapResolution(TenantContextResolution resolution) =>
        (resolution.EffectiveTenantId, resolution.UserId, resolution.CollegeId);
}
