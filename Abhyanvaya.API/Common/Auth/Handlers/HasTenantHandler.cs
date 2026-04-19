using Abhyanvaya.API.Common.Auth.Requirements;
using Microsoft.AspNetCore.Authorization;

namespace Abhyanvaya.API.Common.Auth.Handlers
{
    public sealed class HasTenantHandler : AuthorizationHandler<HasTenantRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, HasTenantRequirement requirement)
        {
            var claimValue = context.User.FindFirst("TenantId")?.Value;
            if (int.TryParse(claimValue, out var tenantId) && tenantId > 0)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
