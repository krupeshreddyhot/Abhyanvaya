using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.TenantUsers;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.API.Controllers;

/// <summary>Create tenant user accounts and admin password reset. College <c>Admin</c> only.</summary>
[ApiController]
[Route("api/tenant-users")]
[Authorize(Policy = AuthorizationPolicies.TenantCollegeAdminOnly)]
public sealed class TenantUsersController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public TenantUsersController(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    private async Task<bool> StaffBelongsToTenantAsync(int staffId, CancellationToken ct)
    {
        return await (
            from s in _context.StaffMembers.AsNoTracking()
            join c in _context.Colleges.AsNoTracking() on s.CollegeId equals c.Id
            where s.Id == staffId && c.TenantId == _currentUser.TenantId
            select s).AnyAsync(ct);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateTenantUserRequest request, CancellationToken ct)
    {
        var username = request.Username.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(request.Password))
            return BadRequest("Username and password are required.");

        if (request.Password.Length < 8)
            return BadRequest("Password must be at least 8 characters.");

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role) || role is UserRole.SuperAdmin)
            return BadRequest("Role must be Admin or Faculty.");

        var dup = await _context.Users.AnyAsync(u => u.Username.ToLower() == username, ct);
        if (dup)
            return BadRequest("That username is already in use in this college.");

        var course = await _context.Courses.AsNoTracking()
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(c => c.TenantId == _currentUser.TenantId, ct);
        var group = await _context.Groups.AsNoTracking()
            .OrderBy(g => g.Id)
            .FirstOrDefaultAsync(g => g.TenantId == _currentUser.TenantId, ct);

        if (course == null || group == null)
            return BadRequest("This tenant has no course/section yet. Add catalog data first.");

        int courseId;
        int groupId;
        int? staffId = null;

        if (role == UserRole.Faculty)
        {
            if (request.StaffId is not > 0)
                return BadRequest("Faculty logins must be linked to a staff profile.");

            if (!await StaffBelongsToTenantAsync(request.StaffId.Value, ct))
                return BadRequest("Staff record not found in this college.");

            staffId = request.StaffId.Value;

            var anchorSubject = await _context.StaffSubjectAssignments.AsNoTracking()
                .Where(x => x.StaffId == staffId.Value)
                .Select(x => x.Subject)
                .FirstOrDefaultAsync(ct);

            if (anchorSubject != null)
            {
                courseId = anchorSubject.CourseId;
                groupId = anchorSubject.GroupId;
            }
            else if (request.CourseId is > 0 && request.GroupId is > 0)
            {
                var cOk = await _context.Courses.AnyAsync(
                    c => c.Id == request.CourseId && c.TenantId == _currentUser.TenantId, ct);
                var gOk = await _context.Groups.AnyAsync(
                    g => g.Id == request.GroupId && g.TenantId == _currentUser.TenantId, ct);
                if (!cOk || !gOk)
                    return BadRequest("Invalid course or section for this college.");

                courseId = request.CourseId.Value;
                groupId = request.GroupId.Value;
            }
            else
                return BadRequest(
                    "Assign at least one subject to this staff member on the Staff page (Teaching subjects), or provide course and section.");
        }
        else
        {
            courseId = course.Id;
            groupId = group.Id;
        }

        var hasher = new PasswordHasher<User>();
        var user = new User
        {
            Username = username,
            PasswordHash = "_",
            Role = role,
            CourseId = courseId,
            GroupId = groupId,
            StaffId = staffId,
            MustChangePassword = true,
            CreatedDate = DateTime.UtcNow
        };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        await _context.AddAsync(user);
        await _context.SaveChangesAsync(ct);

        var appRoleIds = request.ApplicationRoleIds?.Distinct().Where(x => x > 0).ToList() ?? [];
        if (appRoleIds.Count > 0)
        {
            var valid = await _context.ApplicationRoles
                .Where(r => appRoleIds.Contains(r.Id))
                .Select(r => r.Id)
                .ToListAsync(ct);
            if (valid.Count != appRoleIds.Count)
                return BadRequest("One or more application role ids are invalid.");

            foreach (var rid in valid.OrderBy(x => x))
                await _context.AddAsync(new UserApplicationRole { UserId = user.Id, ApplicationRoleId = rid });
            await _context.SaveChangesAsync(ct);
        }

        return Ok(new { id = user.Id });
    }

    /// <summary>Link or unlink a Faculty user to a staff directory row (updates JWT StaffId on next login).</summary>
    [HttpPut("{userId:int}/staff")]
    public async Task<IActionResult> LinkStaff(int userId, [FromBody] LinkUserStaffRequest request, CancellationToken ct)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null)
            return NotFound();

        if (user.Role != UserRole.Faculty)
            return BadRequest("Only Faculty users can be linked to a staff profile.");

        if (request.StaffId is > 0)
        {
            if (!await StaffBelongsToTenantAsync(request.StaffId.Value, ct))
                return BadRequest("Staff record not found in this college.");

            user.StaffId = request.StaffId.Value;

            var anchor = await _context.StaffSubjectAssignments.AsNoTracking()
                .Where(x => x.StaffId == request.StaffId.Value)
                .Select(x => x.Subject)
                .FirstOrDefaultAsync(ct);
            if (anchor != null)
            {
                user.CourseId = anchor.CourseId;
                user.GroupId = anchor.GroupId;
            }
        }
        else
            user.StaffId = null;

        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{userId:int}/reset-password")]
    public async Task<IActionResult> AdminResetPassword(int userId, [FromBody] AdminResetUserPasswordRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < 8)
            return BadRequest("Password must be at least 8 characters.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null)
            return NotFound();

        if (user.Id == _currentUser.UserId)
            return BadRequest("Use Change password to update your own password.");

        var hasher = new PasswordHasher<User>();
        user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
        user.MustChangePassword = true;
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpires = null;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }
}
