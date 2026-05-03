using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Authorization;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Abhyanvaya.Infrastructure.Services
{
    public class JwtService : IJwtService
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _db;

        public JwtService(IConfiguration configuration, ApplicationDbContext db)
        {
            _configuration = configuration;
            _db = db;
        }

        public async Task<string> GenerateTokenAsync(User user, CancellationToken cancellationToken = default)
        {
            var permissionKeys = await ResolvePermissionKeysAsync(user, cancellationToken).ConfigureAwait(false);

            var claims = new List<Claim>
            {
                new Claim("UserId", user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim("TenantId", user.TenantId.ToString()),
                new Claim("CourseId", user.CourseId.ToString()),
                new Claim("GroupId", user.GroupId.ToString()),
                new Claim("StaffId", (user.StaffId ?? 0).ToString()),
                new Claim("must_change_password", user.MustChangePassword ? "true" : "false")
            };

            foreach (var key in permissionKeys)
                claims.Add(new Claim("permission", key));

            var jwtKey = _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("JWT Key not configured");

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(
                    Convert.ToInt32(_configuration["Jwt:ExpiryMinutes"])),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private async Task<List<string>> ResolvePermissionKeysAsync(User user, CancellationToken cancellationToken)
        {
            if (user.Role == UserRole.SuperAdmin)
            {
                return await _db.Permissions
                    .AsNoTracking()
                    .OrderBy(p => p.Id)
                    .Select(p => p.Key)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            var fromAssignedRoles = await _db.UserApplicationRoles
                .AsNoTracking()
                .Where(u => u.UserId == user.Id)
                .SelectMany(u => u.ApplicationRole.ApplicationRolePermissions.Select(arp => arp.Permission.Key))
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (fromAssignedRoles.Count > 0)
                return fromAssignedRoles.OrderBy(k => k).ToList();

            return user.Role switch
            {
                UserRole.Admin => PermissionKeys.All.ToList(),
                UserRole.Faculty => PermissionKeys.LegacyFacultySet.ToList(),
                _ => new List<string>()
            };
        }
    }
}
