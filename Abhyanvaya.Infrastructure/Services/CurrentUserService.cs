using System.Security.Claims;
using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Abhyanvaya.Infrastructure.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            var user = httpContextAccessor.HttpContext?.User;

            UserId = TryParseInt(user?.FindFirst("UserId")?.Value);
            Role = user?.FindFirst(ClaimTypes.Role)?.Value ?? "";
            TenantId = TryParseInt(user?.FindFirst("TenantId")?.Value);
            CourseId = TryParseInt(user?.FindFirst("CourseId")?.Value);            
            GroupId = TryParseInt(user?.FindFirst("GroupId")?.Value);
        }

        public int UserId { get; }
        public string Role { get; }
        public int TenantId { get; }
        public int CourseId { get; set; }
        public int GroupId { get; set; }

        private int TryParseInt(string? value)
        {
            return int.TryParse(value, out var result) ? result : 0;
        }
    }
}
