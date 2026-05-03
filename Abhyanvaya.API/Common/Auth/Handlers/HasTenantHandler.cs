using System.Security.Claims;
using Abhyanvaya.API.Common.Auth.Requirements;
using Abhyanvaya.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Abhyanvaya.API.Common.Auth.Handlers
{
    public sealed class HasTenantHandler : AuthorizationHandler<HasTenantRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, HasTenantRequirement requirement)
        {
            var role = context.User.FindFirst(ClaimTypes.Role)?.Value;
            if (string.Equals(role, nameof(UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            var claimValue = context.User.FindFirst("TenantId")?.Value;
            if (int.TryParse(claimValue, out var tenantId) && tenantId > 0)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
