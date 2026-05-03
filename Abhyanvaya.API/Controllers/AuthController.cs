using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Abhyanvaya.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Abhyanvaya.Application.DTOs.Login;
using Abhyanvaya.Application.DTOs.Auth;
using Abhyanvaya.Domain.Enums;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

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

            var token = await _jwtService.GenerateTokenAsync(user);
            return Ok(new { token, mustChangePassword = user.MustChangePassword });
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

            var token = await _jwtService.GenerateTokenAsync(user);
            return Ok(new { token, mustChangePassword = user.MustChangePassword });
        }

        /// <summary>Request a one-time reset secret (shown once). College tenant login scope.</summary>
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var universityCode = request.UniversityCode.Trim().ToUpperInvariant();
            var collegeCode = request.CollegeCode.Trim().ToUpperInvariant();
            var username = request.Username.Trim().ToLowerInvariant();

            var college = await _context.Colleges
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    !x.IsDeleted &&
                    x.University.Code.ToUpper() == universityCode &&
                    x.Code.ToUpper() == collegeCode);

            if (college == null)
                return Ok(new { resetToken = (string?)null, message = "College or university not found." });

            var user = await _context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x =>
                    !x.IsDeleted &&
                    x.TenantId == college.TenantId &&
                    x.Username.ToLower() == username &&
                    x.Role != UserRole.SuperAdmin);

            if (user == null)
                return Ok(new
                {
                    resetToken = (string?)null,
                    message = "If this username exists for the college, a reset token was generated."
                });

            var secret = PasswordResetCrypto.CreateResetSecret();
            user.PasswordResetTokenHash = PasswordResetCrypto.Sha256Hex(secret);
            user.PasswordResetTokenExpires = DateTime.UtcNow.AddHours(1);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                resetToken = secret,
                expiresAtUtc = user.PasswordResetTokenExpires,
                message = "Use this token on the Reset password page within one hour."
            });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ResetToken) || string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest("Reset token and new password are required.");

            if (request.NewPassword.Length < 8)
                return BadRequest("Password must be at least 8 characters.");

            var hash = PasswordResetCrypto.Sha256Hex(request.ResetToken.Trim());
            var user = await _context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u =>
                    !u.IsDeleted &&
                    u.PasswordResetTokenHash == hash &&
                    u.PasswordResetTokenExpires > DateTime.UtcNow);

            if (user == null)
                return BadRequest("Invalid or expired reset token.");

            var hasher = new PasswordHasher<User>();
            user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
            user.MustChangePassword = false;
            user.PasswordResetTokenHash = null;
            user.PasswordResetTokenExpires = null;
            await _context.SaveChangesAsync();

            var token = await _jwtService.GenerateTokenAsync(user);
            return Ok(new { token, mustChangePassword = false });
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest("Current and new passwords are required.");

            if (request.NewPassword.Length < 8)
                return BadRequest("New password must be at least 8 characters.");

            var userIdStr = User.FindFirstValue("UserId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId) || userId <= 0)
                return Unauthorized();

            var user = await _context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

            if (user == null)
                return Unauthorized();

            var hasher = new PasswordHasher<User>();
            if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) ==
                PasswordVerificationResult.Failed)
                return BadRequest("Current password is incorrect.");

            user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
            user.MustChangePassword = false;
            user.PasswordResetTokenHash = null;
            user.PasswordResetTokenExpires = null;
            await _context.SaveChangesAsync();

            var token = await _jwtService.GenerateTokenAsync(user);
            return Ok(new { token, mustChangePassword = false });
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

    internal static class PasswordResetCrypto
    {
        internal static string Sha256Hex(string plain)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(plain));
            return Convert.ToHexString(hash);
        }

        internal static string CreateResetSecret()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
