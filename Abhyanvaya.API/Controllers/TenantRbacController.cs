using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Rbac;
using Abhyanvaya.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.API.Controllers;

/// <summary>Tenant application roles, permission matrix, and user–role assignment. College <c>Admin</c> only.</summary>
[ApiController]
[Route("api/tenant-rbac")]
[Authorize(Policy = AuthorizationPolicies.TenantCollegeAdminOnly)]
public class TenantRbacController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public TenantRbacController(IApplicationDbContext context)
    {
        _context = context;
    }

    private static readonly HashSet<string> SystemRoleCodes = new(StringComparer.OrdinalIgnoreCase) { "ADMIN", "FACULTY" };

    [HttpGet("permissions")]
    public async Task<IActionResult> ListPermissions(CancellationToken ct)
    {
        var list = await _context.Permissions.AsNoTracking()
            .OrderBy(p => p.Resource).ThenBy(p => p.Action)
            .Select(p => new PermissionCatalogDto
            {
                Id = p.Id,
                Key = p.Key,
                Resource = p.Resource,
                Action = p.Action
            })
            .ToListAsync(ct);

        return Ok(list);
    }

    [HttpGet("roles")]
    public async Task<IActionResult> ListRoles(CancellationToken ct)
    {
        var roleIds = await _context.ApplicationRoles.AsNoTracking().Select(r => r.Id).ToListAsync(ct);

        var permCounts = await _context.ApplicationRolePermissions.AsNoTracking()
            .Where(x => roleIds.Contains(x.ApplicationRoleId))
            .GroupBy(x => x.ApplicationRoleId)
            .Select(g => new { RoleId = g.Key, Cnt = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Cnt, ct);

        var userCounts = await _context.UserApplicationRoles.AsNoTracking()
            .Where(x => roleIds.Contains(x.ApplicationRoleId))
            .GroupBy(x => x.ApplicationRoleId)
            .Select(g => new { RoleId = g.Key, Cnt = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Cnt, ct);

        var list = await _context.ApplicationRoles.AsNoTracking()
            .OrderBy(r => r.Code)
            .Select(r => new ApplicationRoleListDto
            {
                Id = r.Id,
                Name = r.Name,
                Code = r.Code,
                Description = r.Description,
                PermissionCount = 0,
                AssignedUserCount = 0
            })
            .ToListAsync(ct);

        foreach (var row in list)
        {
            row.PermissionCount = permCounts.GetValueOrDefault(row.Id);
            row.AssignedUserCount = userCounts.GetValueOrDefault(row.Id);
        }

        return Ok(list);
    }

    [HttpGet("roles/{id:int}")]
    public async Task<IActionResult> GetRole(int id, CancellationToken ct)
    {
        var role = await _context.ApplicationRoles.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (role == null)
            return NotFound();

        var permissionIds = await _context.ApplicationRolePermissions.AsNoTracking()
            .Where(x => x.ApplicationRoleId == id)
            .OrderBy(x => x.PermissionId)
            .Select(x => x.PermissionId)
            .ToListAsync(ct);

        return Ok(new ApplicationRoleDetailDto
        {
            Id = role.Id,
            Name = role.Name,
            Code = role.Code,
            Description = role.Description,
            PermissionIds = permissionIds
        });
    }

    [HttpPost("roles")]
    public async Task<IActionResult> CreateRole([FromBody] CreateApplicationRoleRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        var code = request.Code.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(code))
            return BadRequest("Name and code are required.");

        var dup = await _context.ApplicationRoles.AnyAsync(r => r.Code.ToUpper() == code, ct);
        if (dup)
            return BadRequest("A role with this code already exists.");

        var entity = new ApplicationRole
        {
            Name = name,
            Code = code,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CreatedDate = DateTime.UtcNow,
            IsDeleted = false
        };

        await _context.AddAsync(entity);
        await _context.SaveChangesAsync(ct);

        return Ok(new { id = entity.Id });
    }

    [HttpPut("roles/{id:int}")]
    public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateApplicationRoleRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrEmpty(name))
            return BadRequest("Name is required.");

        var role = await _context.ApplicationRoles.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (role == null)
            return NotFound();

        role.Name = name;
        role.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpPut("roles/{id:int}/permissions")]
    public async Task<IActionResult> SetRolePermissions(int id, [FromBody] SetApplicationRolePermissionsRequest request,
        CancellationToken ct)
    {
        var role = await _context.ApplicationRoles.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (role == null)
            return NotFound();

        var ids = request.PermissionIds?.Distinct().ToList() ?? [];
        if (ids.Count == 0)
        {
            var existingEmpty = await _context.ApplicationRolePermissions.Where(x => x.ApplicationRoleId == id).ToListAsync(ct);
            foreach (var row in existingEmpty)
                _context.Remove(row);
            await _context.SaveChangesAsync(ct);
            return NoContent();
        }

        var validIds = await _context.Permissions.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(ct);
        if (validIds.Count != ids.Count)
            return BadRequest("One or more permission ids are invalid.");

        var existing = await _context.ApplicationRolePermissions.Where(x => x.ApplicationRoleId == id).ToListAsync(ct);
        foreach (var row in existing)
            _context.Remove(row);

        foreach (var pid in validIds.OrderBy(x => x))
        {
            await _context.AddAsync(new ApplicationRolePermission
            {
                ApplicationRoleId = id,
                PermissionId = pid
            });
        }

        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("roles/{id:int}")]
    public async Task<IActionResult> DeleteRole(int id, CancellationToken ct)
    {
        var role = await _context.ApplicationRoles.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (role == null)
            return NotFound();

        if (SystemRoleCodes.Contains(role.Code))
            return BadRequest("Built-in roles (ADMIN, FACULTY) cannot be deleted.");

        var assigned = await _context.UserApplicationRoles.AnyAsync(u => u.ApplicationRoleId == id, ct);
        if (assigned)
            return BadRequest("Remove this role from all users before deleting.");

        role.IsDeleted = true;
        role.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("users")]
    public async Task<IActionResult> ListUsers(CancellationToken ct)
    {
        var list = await _context.Users.AsNoTracking()
            .OrderBy(u => u.Username)
            .Select(u => new TenantUserRbacDto
            {
                Id = u.Id,
                Username = u.Username,
                EnumRole = u.Role.ToString(),
                StaffId = u.StaffId,
                ApplicationRoleIds = u.UserApplicationRoles.Select(x => x.ApplicationRoleId).OrderBy(x => x).ToList()
            })
            .ToListAsync(ct);

        return Ok(list);
    }

    [HttpPut("users/{userId:int}/application-roles")]
    public async Task<IActionResult> SetUserApplicationRoles(int userId, [FromBody] SetUserApplicationRolesRequest request,
        CancellationToken ct)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null)
            return NotFound();

        var roleIds = request.ApplicationRoleIds?.Distinct().ToList() ?? [];
        if (roleIds.Count > 0)
        {
            var found = await _context.ApplicationRoles.AsNoTracking()
                .Where(r => roleIds.Contains(r.Id))
                .Select(r => r.Id)
                .ToListAsync(ct);
            if (found.Count != roleIds.Count)
                return BadRequest("One or more application role ids are invalid.");
        }

        var existing = await _context.UserApplicationRoles.Where(x => x.UserId == userId).ToListAsync(ct);
        foreach (var row in existing)
            _context.Remove(row);

        foreach (var rid in roleIds.OrderBy(x => x))
        {
            await _context.AddAsync(new UserApplicationRole
            {
                UserId = userId,
                ApplicationRoleId = rid
            });
        }

        await _context.SaveChangesAsync(ct);
        return NoContent();
    }
}
