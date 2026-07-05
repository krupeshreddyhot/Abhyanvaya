using System.Security.Claims;
using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Abhyanvaya.Infrastructure.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly int? _httpTenantId;
        private readonly ITenantContextAccessor _tenantContextAccessor;

        public CurrentUserService(
            IHttpContextAccessor httpContextAccessor,
            ITenantContextAccessor tenantContextAccessor)
        {
            _tenantContextAccessor = tenantContextAccessor;

            var user = httpContextAccessor.HttpContext?.User;

            UserId = TryParseInt(user?.FindFirst("UserId")?.Value);
            Role = user?.FindFirst(ClaimTypes.Role)?.Value ?? "";
            _httpTenantId = TryParseNullableInt(user?.FindFirst("TenantId")?.Value);
            StaffId = TryParseInt(user?.FindFirst("StaffId")?.Value);
            CourseId = TryParseInt(user?.FindFirst("CourseId")?.Value);
            GroupId = TryParseInt(user?.FindFirst("GroupId")?.Value);
        }

        public int UserId { get; }
        public string Role { get; }

        /// <summary>
        /// Resolves the tenant in priority order: HTTP JWT claim first, then the ambient
        /// <see cref="ITenantContextAccessor"/> (set by non-HTTP entry points such as background
        /// workers), then <c>0</c> when no tenant is established.
        /// </summary>
        public int TenantId => _httpTenantId ?? _tenantContextAccessor.CurrentTenantId ?? 0;

        public int StaffId { get; }
        public int CourseId { get; set; }
        public int GroupId { get; set; }

        private static int TryParseInt(string? value)
        {
            return int.TryParse(value, out var result) ? result : 0;
        }

        private static int? TryParseNullableInt(string? value)
        {
            return int.TryParse(value, out var result) ? result : null;
        }
    }
}
