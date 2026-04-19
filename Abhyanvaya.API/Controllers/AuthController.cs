using Microsoft.AspNetCore.Mvc;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Abhyanvaya.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Abhyanvaya.Application.DTOs.Login;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IJwtService _jwtService;

        public AuthController(ApplicationDbContext context, IJwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        /// <summary>Super Admin: username + password only (no university/college). TenantId claim is 0.</summary>
        [HttpPost("super-admin-login")]
        public async Task<IActionResult> SuperAdminLogin([FromBody] SuperAdminLoginRequest request)
        {
            var username = request.Username.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("Username and password are required");

            var user = await _context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x =>
                    !x.IsDeleted &&
                    x.Username.ToLower() == username &&
                    x.Role == UserRole.SuperAdmin);

            if (user == null)
                return Unauthorized("Invalid username or password");

            var hasher = new PasswordHasher<User>();
            var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (result == PasswordVerificationResult.Failed)
                return Unauthorized("Invalid username or password");

            var token = _jwtService.GenerateToken(user);
            return Ok(new { token });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var universityCode = request.UniversityCode.Trim().ToUpperInvariant();
            var collegeCode = request.CollegeCode.Trim().ToUpperInvariant();
            var username = request.Username.Trim().ToLower();

            if (string.IsNullOrWhiteSpace(universityCode) || string.IsNullOrWhiteSpace(collegeCode))
                return BadRequest("University and college are required");

            var college = await _context.Colleges
                .IgnoreQueryFilters()
                .Include(c => c.University)
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    !x.IsDeleted &&
                    x.University.Code.ToUpper() == universityCode &&
                    x.Code.ToUpper() == collegeCode);

            if (college == null)
                return Unauthorized("Invalid university or college code");

            var user = await _context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x =>
                    !x.IsDeleted &&
                    x.TenantId == college.TenantId &&
                    x.Username.ToLower() == username);

            if (user == null)
                return Unauthorized("Invalid username or tenant");

            var hasher = new PasswordHasher<User>();
            var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

            if (result == PasswordVerificationResult.Failed)
                return Unauthorized("Invalid password");

            var token = _jwtService.GenerateToken(user);
            return Ok(new { token });
        }

        [HttpGet("universities")]
        public async Task<IActionResult> GetUniversities()
        {
            var list = await _context.Universities
                .AsNoTracking()
                .OrderBy(u => u.Name)
                .Select(u => new { code = u.Code.ToUpper(), name = u.Name })
                .ToListAsync();

            return Ok(list);
        }
    }
}
