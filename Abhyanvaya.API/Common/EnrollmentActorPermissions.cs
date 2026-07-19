using System.Security.Claims;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Authorization;
using Abhyanvaya.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Abhyanvaya.API.Common;

public sealed class EnrollmentActorPermissions : IEnrollmentActorPermissions
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EnrollmentActorPermissions(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool CanViewEnrollment => EvaluateViewPermission();

    public bool CanManageEnrollment => EvaluateManagePermission();

    private bool EvaluateViewPermission()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var role = user.FindFirst(ClaimTypes.Role)?.Value;
        if (string.Equals(role, nameof(UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return user.HasClaim("permission", PermissionKeys.EnrollmentView)
               || user.HasClaim("permission", PermissionKeys.EnrollmentManage)
               || user.HasClaim("permission", PermissionKeys.StudentsView);
    }

    private bool EvaluateManagePermission()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var role = user.FindFirst(ClaimTypes.Role)?.Value;
        if (string.Equals(role, nameof(UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return user.HasClaim("permission", PermissionKeys.EnrollmentManage)
               || user.HasClaim("permission", PermissionKeys.StudentsManage);
    }
}
