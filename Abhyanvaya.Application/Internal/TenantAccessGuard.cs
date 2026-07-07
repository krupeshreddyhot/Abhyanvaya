using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.Internal;

/// <summary>
/// Centralizes multi-tenant access checks for AI attendance application services.
/// </summary>
internal static class TenantAccessGuard
{
    internal static void EnsureTenantAccess(ICurrentUserService currentUser, int tenantId)
    {
        if (IsSuperAdmin(currentUser))
        {
            return;
        }

        if (tenantId != currentUser.TenantId)
        {
            throw new UnauthorizedAccessException("Access denied for this tenant.");
        }
    }

    private static bool IsSuperAdmin(ICurrentUserService currentUser) =>
        string.Equals(currentUser.Role, nameof(UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase);
}
