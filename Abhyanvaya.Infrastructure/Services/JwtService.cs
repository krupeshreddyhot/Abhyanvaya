using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Infrastructure.Services
{
    public class JwtService : IJwtService
    {
        private readonly IConfiguration _configuration;

        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateToken(User user)
        {
            var claims = new List<Claim>
            {
                new Claim("UserId", user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim("TenantId", user.TenantId.ToString()),
                new Claim("CourseId", user.CourseId.ToString() ?? ""),
                new Claim("GroupId", user.GroupId.ToString() ?? "")
            };

            var key = _configuration["Jwt:Key"]
                ?? throw new Exception("JWT Key not configured");

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));

            var creds = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(
                    Convert.ToInt32(_configuration["Jwt:ExpiryMinutes"])),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
