using Abhyanvaya.API.Common;
using Abhyanvaya.Application.DTOs.Admin;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Abhyanvaya.Infrastructure.Persistence;

namespace Abhyanvaya.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AuthorizationPolicies.SuperAdminOnly)]
public sealed class OrganizationController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public OrganizationController(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>Colleges under a university (for parent selection when provisioning). Super Admin only.</summary>
    [HttpGet("parent-college-options")]
    public async Task<IActionResult> GetParentCollegeOptions([FromQuery] int universityId, CancellationToken cancellationToken)
    {
        if (universityId <= 0)
            return BadRequest("University is required.");

        var exists = await _db.Universities.AsNoTracking().AnyAsync(u => u.Id == universityId, cancellationToken);
        if (!exists)
            return BadRequest("Invalid university.");

        var options = await _db.Colleges.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => !c.IsDeleted && c.UniversityId == universityId)
            .OrderBy(c => c.Name)
            .Select(c => new { id = c.Id, name = c.Name, code = c.Code, shortName = c.ShortName })
            .ToListAsync(cancellationToken);

        return Ok(options);
    }

    /// <summary>Create a new tenant college and its first Admin user (programme/group scaffolding included).</summary>
    [HttpPost("colleges")]
    public async Task<IActionResult> ProvisionCollege([FromBody] CreateTenantCollegeRequest request, CancellationToken cancellationToken)
    {
        var collegeName = request.CollegeName.Trim();
        var collegeCode = request.CollegeCode.Trim().ToUpperInvariant();
        var adminUser = request.AdminUsername.Trim().ToLowerInvariant();

        if (request.UniversityId <= 0 || string.IsNullOrWhiteSpace(collegeName) || string.IsNullOrWhiteSpace(collegeCode))
            return BadRequest("University, college name and code are required.");

        if (string.IsNullOrWhiteSpace(adminUser) || string.IsNullOrWhiteSpace(request.AdminPassword))
            return BadRequest("Admin username and password are required.");

        var university = await _db.Universities.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.UniversityId, cancellationToken);
        if (university == null)
            return BadRequest("University not found.");

        var dupCode = await _db.Colleges.IgnoreQueryFilters()
            .AnyAsync(c => !c.IsDeleted && c.UniversityId == request.UniversityId && c.Code.ToUpper() == collegeCode, cancellationToken);
        if (dupCode)
            return BadRequest("Another college already uses this code under the selected university.");

        var dupUserGlobal = await _db.Users.IgnoreQueryFilters()
            .AnyAsync(u => !u.IsDeleted && u.Username.ToLower() == adminUser, cancellationToken);
        if (dupUserGlobal)
            return BadRequest("That admin username is already taken. Choose another.");

        int? parentCollegeId = request.ParentCollegeId;
        if (parentCollegeId.HasValue)
        {
            var parent = await _db.Colleges.IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == parentCollegeId.Value && !c.IsDeleted, cancellationToken);
            if (parent == null || parent.UniversityId != request.UniversityId)
                return BadRequest("Invalid parent college for this university.");
        }

        var nextTenantId = await ComputeNextTenantIdAsync(cancellationToken);

        var course = new Course
        {
            Name = "Default Programme",
            TenantId = nextTenantId,
            CreatedDate = DateTime.UtcNow,
        };
        await _db.AddAsync(course, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var group = new Group
        {
            Name = "Default Section",
            CourseId = course.Id,
            TenantId = nextTenantId,
            CreatedDate = DateTime.UtcNow,
        };
        await _db.AddAsync(group, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var college = new College
        {
            Name = collegeName,
            Code = collegeCode,
            UniversityId = request.UniversityId,
            ParentCollegeId = parentCollegeId,
            TenantId = nextTenantId,
            CreatedDate = DateTime.UtcNow,
        };
        await _db.AddAsync(college, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var hasher = new PasswordHasher<User>();
        var adminEntity = new User
        {
            Username = adminUser,
            PasswordHash = "_",
            Role = UserRole.Admin,
            TenantId = nextTenantId,
            CourseId = course.Id,
            GroupId = group.Id,
            MustChangePassword = false,
            CreatedDate = DateTime.UtcNow,
        };
        adminEntity.PasswordHash = hasher.HashPassword(adminEntity, request.AdminPassword);
        await _db.AddAsync(adminEntity, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new ProvisionedCollegeDto
        {
            TenantId = nextTenantId,
            CollegeId = college.Id,
            CollegeCode = college.Code,
            CollegeName = college.Name,
            UniversityCode = university.Code,
        });
    }

    private async Task<int> ComputeNextTenantIdAsync(CancellationToken cancellationToken)
    {
        var fromColleges = await _db.Colleges.IgnoreQueryFilters().MaxAsync(c => (int?)c.TenantId, cancellationToken) ?? 0;
        var fromUsers = await _db.Users.IgnoreQueryFilters().MaxAsync(u => (int?)u.TenantId, cancellationToken) ?? 0;
        var fromStudents = await _db.Students.IgnoreQueryFilters().MaxAsync(s => (int?)s.TenantId, cancellationToken) ?? 0;
        var max = Math.Max(Math.Max(fromColleges, fromUsers), fromStudents);
        return max + 1;
    }
}
