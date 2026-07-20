using System.Security.Claims;
using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Abhyanvaya.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor,
        ITenantContextAccessor tenantContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        _tenantContextAccessor = tenantContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public int UserId => TryParseInt(User?.FindFirst("UserId")?.Value);

    public string Role => User?.FindFirst(ClaimTypes.Role)?.Value ?? "";

    /// <summary>
    /// Resolves the tenant in priority order: HTTP JWT claim when &gt; 0, then the ambient
    /// operational context / <see cref="ITenantContextAccessor"/> (SuperAdmin selection or
    /// non-HTTP workers), then <c>0</c> when no tenant is established.
    /// </summary>
    public int TenantId
    {
        get
        {
            var httpTenantId = TryParseNullableInt(User?.FindFirst("TenantId")?.Value);
            return httpTenantId is > 0
                ? httpTenantId.Value
                : _tenantContextAccessor.CurrentTenantId ?? 0;
        }
    }

    public int StaffId => TryParseInt(User?.FindFirst("StaffId")?.Value);

    public int CourseId { get; set; }

    public int GroupId { get; set; }

    private static int TryParseInt(string? value) =>
        int.TryParse(value, out var result) ? result : 0;

    private static int? TryParseNullableInt(string? value) =>
        int.TryParse(value, out var result) ? result : null;
}
