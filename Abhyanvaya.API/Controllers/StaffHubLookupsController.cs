using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Staff;
using Abhyanvaya.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.API.Controllers;

/// <summary>CRUD for tenant lookup rows used by Staff (and department/college role labels).</summary>
[ApiController]
[Route("api/staff-hub-lookups")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class StaffHubLookupsController : ControllerBase
{
    private static readonly HashSet<string> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "staff-types",
        "person-titles",
        "designations",
        "qualifications",
        "employment-statuses",
        "department-roles",
        "college-roles",
    };

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public StaffHubLookupsController(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    private async Task<int> ResolveLookupTenantIdAsync(CancellationToken ct)
    {
        if (_currentUser.TenantId > 0)
            return _currentUser.TenantId;
        var c = await _context.Colleges.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        return c?.TenantId ?? 0;
    }

    [HttpGet("{kind}")]
    public async Task<IActionResult> List(string kind, CancellationToken ct)
    {
        if (!Kinds.Contains(kind))
            return BadRequest("Unknown lookup kind.");

        var tenantId = await ResolveLookupTenantIdAsync(ct);
        if (tenantId <= 0)
            return BadRequest("No tenant context for lookups.");

        return kind.ToLowerInvariant() switch
        {
            "staff-types" => Ok(await ListStaffTypes(tenantId, ct)),
            "person-titles" => Ok(await ListPersonTitles(tenantId, ct)),
            "designations" => Ok(await ListDesignations(tenantId, ct)),
            "qualifications" => Ok(await ListQualifications(tenantId, ct)),
            "employment-statuses" => Ok(await ListEmploymentStatuses(tenantId, ct)),
            "department-roles" => Ok(await ListDepartmentRoles(tenantId, ct)),
            "college-roles" => Ok(await ListCollegeRoles(tenantId, ct)),
            _ => BadRequest(),
        };
    }

    [HttpPost("{kind}")]
    public async Task<IActionResult> Create(string kind, [FromBody] StaffLookupWriteRequest request, CancellationToken ct)
    {
        if (!Kinds.Contains(kind))
            return BadRequest("Unknown lookup kind.");

        var validation = ValidateWrite(request, kind);
        if (validation != null)
            return validation;

        var tenantId = await ResolveLookupTenantIdAsync(ct);
        if (tenantId <= 0)
            return BadRequest("No tenant context for lookups.");

        var name = request.Name.Trim();
        var code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim();

        return kind.ToLowerInvariant() switch
        {
            "staff-types" => await CreateStaffType(tenantId, name, code, request, ct),
            "person-titles" => await CreatePersonTitle(tenantId, name, code, request, ct),
            "designations" => await CreateDesignation(tenantId, name, code, request, ct),
            "qualifications" => await CreateQualification(tenantId, name, code, request, ct),
            "employment-statuses" => await CreateEmploymentStatus(tenantId, name, code, request, ct),
            "department-roles" => await CreateDepartmentRole(tenantId, name, code, request, ct),
            "college-roles" => await CreateCollegeRole(tenantId, name, code, request, ct),
            _ => BadRequest(),
        };
    }

    [HttpPut("{kind}/{id:int}")]
    public async Task<IActionResult> Update(string kind, int id, [FromBody] StaffLookupWriteRequest request, CancellationToken ct)
    {
        if (!Kinds.Contains(kind))
            return BadRequest("Unknown lookup kind.");

        var validation = ValidateWrite(request, kind);
        if (validation != null)
            return validation;

        var tenantId = await ResolveLookupTenantIdAsync(ct);
        if (tenantId <= 0)
            return BadRequest("No tenant context for lookups.");

        var name = request.Name.Trim();
        var code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim();

        return kind.ToLowerInvariant() switch
        {
            "staff-types" => await UpdateStaffType(id, tenantId, name, code, request, ct),
            "person-titles" => await UpdatePersonTitle(id, tenantId, name, code, request, ct),
            "designations" => await UpdateDesignation(id, tenantId, name, code, request, ct),
            "qualifications" => await UpdateQualification(id, tenantId, name, code, request, ct),
            "employment-statuses" => await UpdateEmploymentStatus(id, tenantId, name, code, request, ct),
            "department-roles" => await UpdateDepartmentRole(id, tenantId, name, code, request, ct),
            "college-roles" => await UpdateCollegeRole(id, tenantId, name, code, request, ct),
            _ => BadRequest(),
        };
    }

    [HttpDelete("{kind}/{id:int}")]
    public async Task<IActionResult> Delete(string kind, int id, CancellationToken ct)
    {
        if (!Kinds.Contains(kind))
            return BadRequest("Unknown lookup kind.");

        var tenantId = await ResolveLookupTenantIdAsync(ct);
        if (tenantId <= 0)
            return BadRequest("No tenant context for lookups.");

        return kind.ToLowerInvariant() switch
        {
            "staff-types" => await SoftDeleteStaffType(id, tenantId, ct),
            "person-titles" => await SoftDeletePersonTitle(id, tenantId, ct),
            "designations" => await SoftDeleteDesignation(id, tenantId, ct),
            "qualifications" => await SoftDeleteQualification(id, tenantId, ct),
            "employment-statuses" => await SoftDeleteEmploymentStatus(id, tenantId, ct),
            "department-roles" => await SoftDeleteDepartmentRole(id, tenantId, ct),
            "college-roles" => await SoftDeleteCollegeRole(id, tenantId, ct),
            _ => BadRequest(),
        };
    }

    private IActionResult? ValidateWrite(StaffLookupWriteRequest request, string kind)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Name is required.");

        var k = kind.ToLowerInvariant();
        if (k != "department-roles" && k != "college-roles")
        {
            if (request.IsExclusivePerDepartment || request.IsExclusivePerCollege)
                return BadRequest("Exclusive flags apply only to department or college roles.");
        }

        if (k == "department-roles" && request.IsExclusivePerCollege)
            return BadRequest("Invalid exclusive flag for department roles.");

        if (k == "college-roles" && request.IsExclusivePerDepartment)
            return BadRequest("Invalid exclusive flag for college roles.");

        return null;
    }

    private Task<List<StaffLookupAdminDto>> ListStaffTypes(int tenantId, CancellationToken ct) =>
        _context.StaffTypeLookups.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new StaffLookupAdminDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                SortOrder = x.SortOrder,
                IsActive = x.IsActive,
                IsExclusivePerDepartment = false,
                IsExclusivePerCollege = false,
            }).ToListAsync(ct);

    private Task<List<StaffLookupAdminDto>> ListPersonTitles(int tenantId, CancellationToken ct) =>
        _context.PersonTitleLookups.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new StaffLookupAdminDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                SortOrder = x.SortOrder,
                IsActive = x.IsActive,
                IsExclusivePerDepartment = false,
                IsExclusivePerCollege = false,
            }).ToListAsync(ct);

    private Task<List<StaffLookupAdminDto>> ListDesignations(int tenantId, CancellationToken ct) =>
        _context.DesignationLookups.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new StaffLookupAdminDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                SortOrder = x.SortOrder,
                IsActive = x.IsActive,
                IsExclusivePerDepartment = false,
                IsExclusivePerCollege = false,
            }).ToListAsync(ct);

    private Task<List<StaffLookupAdminDto>> ListQualifications(int tenantId, CancellationToken ct) =>
        _context.QualificationLookups.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new StaffLookupAdminDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                SortOrder = x.SortOrder,
                IsActive = x.IsActive,
                IsExclusivePerDepartment = false,
                IsExclusivePerCollege = false,
            }).ToListAsync(ct);

    private Task<List<StaffLookupAdminDto>> ListEmploymentStatuses(int tenantId, CancellationToken ct) =>
        _context.EmploymentStatusLookups.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new StaffLookupAdminDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                SortOrder = x.SortOrder,
                IsActive = x.IsActive,
                IsExclusivePerDepartment = false,
                IsExclusivePerCollege = false,
            }).ToListAsync(ct);

    private Task<List<StaffLookupAdminDto>> ListDepartmentRoles(int tenantId, CancellationToken ct) =>
        _context.DepartmentRoleLookups.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new StaffLookupAdminDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                SortOrder = x.SortOrder,
                IsActive = x.IsActive,
                IsExclusivePerDepartment = x.IsExclusivePerDepartment,
                IsExclusivePerCollege = false,
            }).ToListAsync(ct);

    private Task<List<StaffLookupAdminDto>> ListCollegeRoles(int tenantId, CancellationToken ct) =>
        _context.CollegeRoleLookups.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new StaffLookupAdminDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                SortOrder = x.SortOrder,
                IsActive = x.IsActive,
                IsExclusivePerDepartment = false,
                IsExclusivePerCollege = x.IsExclusivePerCollege,
            }).ToListAsync(ct);

    private async Task<IActionResult> CreateStaffType(int tenantId, string name, string? code, StaffLookupWriteRequest request, CancellationToken ct)
    {
        if (await NameConflictStaffType(tenantId, name, null, ct))
            return BadRequest("A staff type with this name already exists.");

        var entity = new StaffTypeLookup
        {
            TenantId = tenantId,
            Name = name,
            Code = code,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            CreatedDate = DateTime.UtcNow,
        };
        await _context.AddAsync(entity);
        await _context.SaveChangesAsync(ct);
        return Ok(new { id = entity.Id });
    }

    private async Task<bool> NameConflictStaffType(int tenantId, string name, int? excludeId, CancellationToken ct)
    {
        var q = _context.StaffTypeLookups.Where(x => x.TenantId == tenantId && x.Name.ToLower() == name.ToLower());
        if (excludeId.HasValue)
            q = q.Where(x => x.Id != excludeId.Value);
        return await q.AnyAsync(ct);
    }

    private async Task<IActionResult> UpdateStaffType(int id, int tenantId, string name, string? code, StaffLookupWriteRequest request, CancellationToken ct)
    {
        var entity = await _context.StaffTypeLookups.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (entity == null)
            return NotFound();

        if (await NameConflictStaffType(tenantId, name, id, ct))
            return BadRequest("A staff type with this name already exists.");

        entity.Name = name;
        entity.Code = code;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<IActionResult> SoftDeleteStaffType(int id, int tenantId, CancellationToken ct)
    {
        var entity = await _context.StaffTypeLookups.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (entity == null)
            return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<IActionResult> CreatePersonTitle(int tenantId, string name, string? code, StaffLookupWriteRequest request, CancellationToken ct)
    {
        if (await _context.PersonTitleLookups.AnyAsync(x => x.TenantId == tenantId && x.Name.ToLower() == name.ToLower(), ct))
            return BadRequest("This title already exists.");

        var entity = new PersonTitleLookup { TenantId = tenantId, Name = name, Code = code, SortOrder = request.SortOrder, IsActive = request.IsActive, CreatedDate = DateTime.UtcNow };
        await _context.AddAsync(entity);
        await _context.SaveChangesAsync(ct);
        return Ok(new { id = entity.Id });
    }

    private async Task<IActionResult> UpdatePersonTitle(int id, int tenantId, string name, string? code, StaffLookupWriteRequest request, CancellationToken ct)
    {
        var entity = await _context.PersonTitleLookups.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (entity == null)
            return NotFound();

        if (await _context.PersonTitleLookups.AnyAsync(x => x.TenantId == tenantId && x.Id != id && x.Name.ToLower() == name.ToLower(), ct))
            return BadRequest("This title already exists.");

        entity.Name = name;
        entity.Code = code;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<IActionResult> SoftDeletePersonTitle(int id, int tenantId, CancellationToken ct)
    {
        var entity = await _context.PersonTitleLookups.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (entity == null)
            return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<IActionResult> CreateDesignation(int tenantId, string name, string? code, StaffLookupWriteRequest request, CancellationToken ct)
    {
        if (await _context.DesignationLookups.AnyAsync(x => x.TenantId == tenantId && x.Name.ToLower() == name.ToLower(), ct))
            return BadRequest("This designation already exists.");

        var entity = new DesignationLookup { TenantId = tenantId, Name = name, Code = code, SortOrder = request.SortOrder, IsActive = request.IsActive, CreatedDate = DateTime.UtcNow };
        await _context.AddAsync(entity);
        await _context.SaveChangesAsync(ct);
        return Ok(new { id = entity.Id });
    }

    private async Task<IActionResult> UpdateDesignation(int id, int tenantId, string name, string? code, StaffLookupWriteRequest request, CancellationToken ct)
    {
        var entity = await _context.DesignationLookups.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (entity == null)
            return NotFound();

        if (await _context.DesignationLookups.AnyAsync(x => x.TenantId == tenantId && x.Id != id && x.Name.ToLower() == name.ToLower(), ct))
            return BadRequest("This designation already exists.");

        entity.Name = name;
        entity.Code = code;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<IActionResult> SoftDeleteDesignation(int id, int tenantId, CancellationToken ct)
    {
        var entity = await _context.DesignationLookups.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (entity == null)
            return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<IActionResult> CreateQualification(int tenantId, string name, string? code, StaffLookupWriteRequest request, CancellationToken ct)
    {
        if (await _context.QualificationLookups.AnyAsync(x => x.TenantId == tenantId && x.Name.ToLower() == name.ToLower(), ct))
            return BadRequest("This qualification already exists.");

        var entity = new QualificationLookup { TenantId = tenantId, Name = name, Code = code, SortOrder = request.SortOrder, IsActive = request.IsActive, CreatedDate = DateTime.UtcNow };
        await _context.AddAsync(entity);
        await _context.SaveChangesAsync(ct);
        return Ok(new { id = entity.Id });
    }

    private async Task<IActionResult> UpdateQualification(int id, int tenantId, string name, string? code, StaffLookupWriteRequest request, CancellationToken ct)
    {
        var entity = await _context.QualificationLookups.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (entity == null)
            return NotFound();

        if (await _context.QualificationLookups.AnyAsync(x => x.TenantId == tenantId && x.Id != id && x.Name.ToLower() == name.ToLower(), ct))
            return BadRequest("This qualification already exists.");

        entity.Name = name;
        entity.Code = code;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<IActionResult> SoftDeleteQualification(int id, int tenantId, CancellationToken ct)
    {
        var entity = await _context.QualificationLookups.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (entity == null)
            return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<IActionResult> CreateEmploymentStatus(int tenantId, string name, string? code, StaffLookupWriteRequest request, CancellationToken ct)
    {
        if (await _context.EmploymentStatusLookups.AnyAsync(x => x.TenantId == tenantId && x.Name.ToLower() == name.ToLower(), ct))
            return BadRequest("This status already exists.");

        var entity = new EmploymentStatusLookup { TenantId = tenantId, Name = name, Code = code, SortOrder = request.SortOrder, IsActive = request.IsActive, CreatedDate = DateTime.UtcNow };
        await _context.AddAsync(entity);
        await _context.SaveChangesAsync(ct);
        return Ok(new { id = entity.Id });
    }

    private async Task<IActionResult> UpdateEmploymentStatus(int id, int tenantId, string name, string? code, StaffLookupWriteRequest request, CancellationToken ct)
    {
        var entity = await _context.EmploymentStatusLookups.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (entity == null)
            return NotFound();

        if (await _context.EmploymentStatusLookups.AnyAsync(x => x.TenantId == tenantId && x.Id != id && x.Name.ToLower() == name.ToLower(), ct))
            return BadRequest("This status already exists.");

        entity.Name = name;
        entity.Code = code;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<IActionResult> SoftDeleteEmploymentStatus(int id, int tenantId, CancellationToken ct)
    {
        var entity = await _context.EmploymentStatusLookups.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (entity == null)
            return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<IActionResult> CreateDepartmentRole(int tenantId, string name, string? code, StaffLookupWriteRequest request, CancellationToken ct)
    {
        if (await _context.DepartmentRoleLookups.AnyAsync(x => x.TenantId == tenantId && x.Name.ToLower() == name.ToLower(), ct))
            return BadRequest("This department role already exists.");

        var entity = new DepartmentRoleLookup
        {
            TenantId = tenantId,
            Name = name,
            Code = code,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            IsExclusivePerDepartment = request.IsExclusivePerDepartment,
            CreatedDate = DateTime.UtcNow,
        };
        await _context.AddAsync(entity);
        await _context.SaveChangesAsync(ct);
        return Ok(new { id = entity.Id });
    }

    private async Task<IActionResult> UpdateDepartmentRole(int id, int tenantId, string name, string? code, StaffLookupWriteRequest request, CancellationToken ct)
    {
        var entity = await _context.DepartmentRoleLookups.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (entity == null)
            return NotFound();

        if (await _context.DepartmentRoleLookups.AnyAsync(x => x.TenantId == tenantId && x.Id != id && x.Name.ToLower() == name.ToLower(), ct))
            return BadRequest("This department role already exists.");

        entity.Name = name;
        entity.Code = code;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.IsExclusivePerDepartment = request.IsExclusivePerDepartment;
        entity.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<IActionResult> SoftDeleteDepartmentRole(int id, int tenantId, CancellationToken ct)
    {
        var entity = await _context.DepartmentRoleLookups.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (entity == null)
            return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<IActionResult> CreateCollegeRole(int tenantId, string name, string? code, StaffLookupWriteRequest request, CancellationToken ct)
    {
        if (await _context.CollegeRoleLookups.AnyAsync(x => x.TenantId == tenantId && x.Name.ToLower() == name.ToLower(), ct))
            return BadRequest("This college role already exists.");

        var entity = new CollegeRoleLookup
        {
            TenantId = tenantId,
            Name = name,
            Code = code,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            IsExclusivePerCollege = request.IsExclusivePerCollege,
            CreatedDate = DateTime.UtcNow,
        };
        await _context.AddAsync(entity);
        await _context.SaveChangesAsync(ct);
        return Ok(new { id = entity.Id });
    }

    private async Task<IActionResult> UpdateCollegeRole(int id, int tenantId, string name, string? code, StaffLookupWriteRequest request, CancellationToken ct)
    {
        var entity = await _context.CollegeRoleLookups.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (entity == null)
            return NotFound();

        if (await _context.CollegeRoleLookups.AnyAsync(x => x.TenantId == tenantId && x.Id != id && x.Name.ToLower() == name.ToLower(), ct))
            return BadRequest("This college role already exists.");

        entity.Name = name;
        entity.Code = code;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.IsExclusivePerCollege = request.IsExclusivePerCollege;
        entity.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<IActionResult> SoftDeleteCollegeRole(int id, int tenantId, CancellationToken ct)
    {
        var entity = await _context.CollegeRoleLookups.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (entity == null)
            return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }
}
