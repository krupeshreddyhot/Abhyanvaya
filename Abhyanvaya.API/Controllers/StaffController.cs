using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Staff;
using Abhyanvaya.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = AuthorizationPolicies.TenantScopedUser)]
    public class StaffController : ControllerBase
    {
        private const string TeachingStaffTypeCode = "TEACHING";

        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;

        public StaffController(IApplicationDbContext context, ICurrentUserService currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        [HttpGet("setup-metadata")]
        [Authorize(Policy = AuthorizationPolicies.TenantScopedAdmin)]
        public async Task<IActionResult> GetSetupMetadata()
        {
            var staffTypes = await _context.StaffTypeLookups.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
                .Select(x => new LookupItemDto { Id = x.Id, Name = x.Name, Code = x.Code, SortOrder = x.SortOrder })
                .ToListAsync();

            var personTitles = await _context.PersonTitleLookups.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
                .Select(x => new LookupItemDto { Id = x.Id, Name = x.Name, Code = x.Code, SortOrder = x.SortOrder })
                .ToListAsync();

            var designations = await _context.DesignationLookups.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
                .Select(x => new LookupItemDto { Id = x.Id, Name = x.Name, Code = x.Code, SortOrder = x.SortOrder })
                .ToListAsync();

            var qualifications = await _context.QualificationLookups.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
                .Select(x => new LookupItemDto { Id = x.Id, Name = x.Name, Code = x.Code, SortOrder = x.SortOrder })
                .ToListAsync();

            var employment = await _context.EmploymentStatusLookups.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
                .Select(x => new LookupItemDto { Id = x.Id, Name = x.Name, Code = x.Code, SortOrder = x.SortOrder })
                .ToListAsync();

            var deptRoles = await _context.DepartmentRoleLookups.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
                .Select(x => new LookupItemDto { Id = x.Id, Name = x.Name, Code = x.Code, SortOrder = x.SortOrder })
                .ToListAsync();

            var collegeRoles = await _context.CollegeRoleLookups.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
                .Select(x => new LookupItemDto { Id = x.Id, Name = x.Name, Code = x.Code, SortOrder = x.SortOrder })
                .ToListAsync();

            var colleges = await _context.Colleges.AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new CollegeSummaryDto { Id = c.Id, Name = c.Name, Code = c.Code })
                .ToListAsync();

            return Ok(new StaffSetupMetadataDto
            {
                StaffTypes = staffTypes,
                PersonTitles = personTitles,
                Designations = designations,
                Qualifications = qualifications,
                EmploymentStatuses = employment,
                DepartmentRoles = deptRoles,
                CollegeRoles = collegeRoles,
                Colleges = colleges
            });
        }

        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.TenantScopedAdmin)]
        public async Task<IActionResult> List(
            [FromQuery] int? collegeId = null,
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var q = _context.StaffMembers.AsNoTracking();

            if (collegeId is > 0)
                q = q.Where(s => s.CollegeId == collegeId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = "%" + search.Trim().Replace("%", "\\%").Replace("_", "\\_") + "%";
                q = q.Where(s =>
                    EF.Functions.ILike(s.FirstName, pattern, "\\")
                    || EF.Functions.ILike(s.LastName, pattern, "\\")
                    || (s.StaffCode != null && EF.Functions.ILike(s.StaffCode, pattern, "\\")));
            }

            var total = await q.CountAsync();

            var items = await q
                .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new StaffListItemDto
                {
                    Id = s.Id,
                    CollegeId = s.CollegeId,
                    StaffCode = s.StaffCode,
                    FirstName = s.FirstName,
                    LastName = s.LastName,
                    StaffTypeName = s.StaffType.Name,
                    DesignationName = s.Designation.Name,
                    Email = s.Email,
                    DateOfJoining = s.DateOfJoining
                })
                .ToListAsync();

            return Ok(new { total, page, pageSize, items });
        }

        [HttpGet("{id:int}")]
        [Authorize(Policy = AuthorizationPolicies.TenantScopedAdmin)]
        public async Task<IActionResult> Get(int id)
        {
            var staff = await _context.StaffMembers.AsNoTracking()
                .Include(s => s.StaffDepartments).ThenInclude(sd => sd.StaffDepartmentRoles)
                .Include(s => s.StaffCollegeRoles)
                .Include(s => s.StaffSubjectAssignments)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (staff == null)
                return NotFound();

            var deptAssignments = staff.StaffDepartments.Select(sd => new StaffDepartmentAssignmentDto
            {
                DepartmentId = sd.DepartmentId,
                DepartmentRoleLookupIds = sd.StaffDepartmentRoles.Select(r => r.DepartmentRoleLookupId).ToList()
            }).ToList();

            var dto = new StaffDetailDto
            {
                Id = staff.Id,
                CollegeId = staff.CollegeId,
                StaffCode = staff.StaffCode,
                StaffTypeId = staff.StaffTypeId,
                PersonTitleId = staff.PersonTitleId,
                DesignationId = staff.DesignationId,
                QualificationId = staff.QualificationId,
                GenderId = staff.GenderId,
                EmploymentStatusId = staff.EmploymentStatusId,
                FirstName = staff.FirstName,
                LastName = staff.LastName,
                Phone = staff.Phone,
                AltPhone = staff.AltPhone,
                Email = staff.Email,
                Website = staff.Website,
                DateOfJoining = staff.DateOfJoining,
                ContractEndDate = staff.ContractEndDate,
                DateOfBirth = staff.DateOfBirth,
                Departments = deptAssignments,
                CollegeRoleLookupIds = staff.StaffCollegeRoles.Select(r => r.CollegeRoleLookupId).ToList(),
                SubjectIds = staff.StaffSubjectAssignments.Select(a => a.SubjectId).ToList()
            };

            return Ok(dto);
        }

        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.TenantScopedAdmin)]
        public async Task<IActionResult> Create([FromBody] CreateStaffRequest request)
        {
            var validation = await ValidateLookupsAndCollegeAsync(request, CancellationToken.None);
            if (validation != null)
                return validation;

            var staffType = await _context.StaffTypeLookups.AsNoTracking()
                .FirstAsync(t => t.Id == request.StaffTypeId);
            var isTeaching = staffType.Code != null &&
                             string.Equals(staffType.Code, TeachingStaffTypeCode, StringComparison.OrdinalIgnoreCase);

            if (!isTeaching && HasTeachingOnlyPayload(request))
                return BadRequest("Non-teaching staff cannot have department roles, college roles, or subject assignments.");

            var staffCode = string.IsNullOrWhiteSpace(request.StaffCode) ? null : request.StaffCode.Trim();
            if (staffCode != null)
            {
                var dup = await _context.StaffMembers.AnyAsync(s =>
                    s.CollegeId == request.CollegeId && s.StaffCode == staffCode);
                if (dup)
                    return BadRequest("Staff code already exists for this college.");
            }

            var staff = new Staff
            {
                CollegeId = request.CollegeId,
                StaffCode = staffCode,
                StaffTypeId = request.StaffTypeId,
                PersonTitleId = request.PersonTitleId,
                DesignationId = request.DesignationId,
                QualificationId = request.QualificationId,
                GenderId = request.GenderId,
                EmploymentStatusId = request.EmploymentStatusId,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Phone = request.Phone?.Trim(),
                AltPhone = request.AltPhone?.Trim(),
                Email = request.Email?.Trim(),
                Website = request.Website?.Trim(),
                DateOfJoining = NormalizeUtcDate(request.DateOfJoining),
                ContractEndDate = NormalizeUtcDate(request.ContractEndDate),
                DateOfBirth = NormalizeUtcDate(request.DateOfBirth)
            };

            await _context.AddAsync(staff);
            await _context.SaveChangesAsync();

            if (isTeaching)
            {
                try
                {
                    await ApplyDepartmentAssignmentsAsync(staff.Id, staff.CollegeId, request.Departments, CancellationToken.None);
                    await ApplyCollegeRolesAsync(staff.Id, staff.CollegeId, request.CollegeRoleLookupIds, CancellationToken.None);
                    await ApplySubjectAssignmentsAsync(staff.Id, request.SubjectIds, CancellationToken.None);
                    await _context.SaveChangesAsync();
                }
                catch (InvalidOperationException ex)
                {
                    return BadRequest(ex.Message);
                }
            }

            return CreatedAtAction(nameof(Get), new { id = staff.Id }, new { staff.Id });
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = AuthorizationPolicies.TenantScopedAdmin)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateStaffRequest request)
        {
            var staff = await _context.StaffMembers
                .FirstOrDefaultAsync(s => s.Id == id);
            if (staff == null)
                return NotFound();

            var validation = await ValidateLookupsAndCollegeAsync(request, CancellationToken.None);
            if (validation != null)
                return validation;

            var staffType = await _context.StaffTypeLookups.AsNoTracking()
                .FirstAsync(t => t.Id == request.StaffTypeId);
            var isTeaching = staffType.Code != null &&
                             string.Equals(staffType.Code, TeachingStaffTypeCode, StringComparison.OrdinalIgnoreCase);

            if (!isTeaching && HasTeachingOnlyPayload(request))
                return BadRequest("Non-teaching staff cannot have department roles, college roles, or subject assignments.");

            var staffCode = string.IsNullOrWhiteSpace(request.StaffCode) ? null : request.StaffCode.Trim();
            if (staffCode != null)
            {
                var dup = await _context.StaffMembers.AnyAsync(s =>
                    s.CollegeId == request.CollegeId && s.StaffCode == staffCode && s.Id != id);
                if (dup)
                    return BadRequest("Staff code already exists for this college.");
            }

            staff.CollegeId = request.CollegeId;
            staff.StaffCode = staffCode;
            staff.StaffTypeId = request.StaffTypeId;
            staff.PersonTitleId = request.PersonTitleId;
            staff.DesignationId = request.DesignationId;
            staff.QualificationId = request.QualificationId;
            staff.GenderId = request.GenderId;
            staff.EmploymentStatusId = request.EmploymentStatusId;
            staff.FirstName = request.FirstName.Trim();
            staff.LastName = request.LastName.Trim();
            staff.Phone = request.Phone?.Trim();
            staff.AltPhone = request.AltPhone?.Trim();
            staff.Email = request.Email?.Trim();
            staff.Website = request.Website?.Trim();
            staff.DateOfJoining = NormalizeUtcDate(request.DateOfJoining);
            staff.ContractEndDate = NormalizeUtcDate(request.ContractEndDate);
            staff.DateOfBirth = NormalizeUtcDate(request.DateOfBirth);

            await _context.SaveChangesAsync();

            try
            {
                if (isTeaching)
                {
                    await ReplaceDepartmentAssignmentsAsync(staff.Id, staff.CollegeId, request.Departments, CancellationToken.None);
                    await ReplaceCollegeRolesAsync(staff.Id, staff.CollegeId, request.CollegeRoleLookupIds, CancellationToken.None);
                    await ReplaceSubjectAssignmentsAsync(staff.Id, request.SubjectIds, CancellationToken.None);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    await ClearTeachingAssociationsAsync(staff.Id, CancellationToken.None);
                    await _context.SaveChangesAsync();
                }
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }

            return NoContent();
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = AuthorizationPolicies.TenantScopedAdmin)]
        public async Task<IActionResult> Delete(int id)
        {
            var staff = await _context.StaffMembers.FirstOrDefaultAsync(s => s.Id == id);
            if (staff == null)
                return NotFound();

            staff.IsDeleted = true;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static bool HasTeachingOnlyPayload(CreateStaffRequest request)
        {
            return request.Departments is { Count: > 0 }
                   || request.CollegeRoleLookupIds is { Count: > 0 }
                   || request.SubjectIds is { Count: > 0 };
        }

        private async Task<IActionResult?> ValidateLookupsAndCollegeAsync(CreateStaffRequest request, CancellationToken ct)
        {
            var college = await _context.Colleges.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.CollegeId, ct);
            if (college == null)
                return BadRequest("College not found.");

            if (!await _context.StaffTypeLookups.AsNoTracking().AnyAsync(x => x.Id == request.StaffTypeId, ct))
                return BadRequest("Invalid staff type.");
            if (!await _context.DesignationLookups.AsNoTracking().AnyAsync(x => x.Id == request.DesignationId, ct))
                return BadRequest("Invalid designation.");
            if (request.PersonTitleId is int pt && !await _context.PersonTitleLookups.AsNoTracking().AnyAsync(x => x.Id == pt, ct))
                return BadRequest("Invalid person title.");
            if (request.QualificationId is int q && !await _context.QualificationLookups.AsNoTracking().AnyAsync(x => x.Id == q, ct))
                return BadRequest("Invalid qualification.");
            if (request.GenderId is int g && !await _context.Genders.AsNoTracking().AnyAsync(x => x.Id == g, ct))
                return BadRequest("Invalid gender.");
            if (request.EmploymentStatusId is int e && !await _context.EmploymentStatusLookups.AsNoTracking().AnyAsync(x => x.Id == e, ct))
                return BadRequest("Invalid employment status.");

            return null;
        }

        private async Task ApplyDepartmentAssignmentsAsync(int staffId, int collegeId,
            List<StaffDepartmentAssignmentDto>? departments, CancellationToken ct)
        {
            if (departments == null || departments.Count == 0)
                return;

            foreach (var block in departments)
            {
                var dept = await _context.Departments.AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == block.DepartmentId, ct);
                if (dept == null || dept.CollegeId != collegeId)
                    throw new InvalidOperationException("Invalid department for this college.");

                var sd = new StaffDepartment { StaffId = staffId, DepartmentId = block.DepartmentId };
                await _context.AddAsync(sd);
                await _context.SaveChangesAsync(ct);

                foreach (var roleId in block.DepartmentRoleLookupIds.Distinct())
                {
                    await RemoveExclusiveDepartmentRoleFromOthersAsync(block.DepartmentId, roleId, sd.Id, ct);
                    await _context.AddAsync(new StaffDepartmentRole
                    {
                        StaffDepartmentId = sd.Id,
                        DepartmentRoleLookupId = roleId
                    });
                }

                await _context.SaveChangesAsync(ct);
            }
        }

        private async Task ReplaceDepartmentAssignmentsAsync(int staffId, int collegeId,
            List<StaffDepartmentAssignmentDto>? departments, CancellationToken ct)
        {
            var existing = await _context.StaffDepartments.Where(sd => sd.StaffId == staffId).ToListAsync(ct);
            foreach (var e in existing)
                _context.Remove(e);

            await _context.SaveChangesAsync(ct);
            await ApplyDepartmentAssignmentsAsync(staffId, collegeId, departments, ct);
        }

        private async Task RemoveExclusiveDepartmentRoleFromOthersAsync(int departmentId, int departmentRoleLookupId,
            int keepingStaffDepartmentId, CancellationToken ct)
        {
            var meta = await _context.DepartmentRoleLookups.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == departmentRoleLookupId, ct);
            if (meta == null || !meta.IsExclusivePerDepartment)
                return;

            var toRemove = await _context.StaffDepartmentRoles
                .Where(sdr =>
                    sdr.DepartmentRoleLookupId == departmentRoleLookupId
                    && sdr.StaffDepartmentId != keepingStaffDepartmentId
                    && sdr.StaffDepartment.DepartmentId == departmentId)
                .ToListAsync(ct);

            foreach (var row in toRemove)
                _context.Remove(row);

            if (toRemove.Count > 0)
                await _context.SaveChangesAsync(ct);
        }

        private async Task ApplyCollegeRolesAsync(int staffId, int collegeId, List<int>? collegeRoleLookupIds,
            CancellationToken ct)
        {
            if (collegeRoleLookupIds == null || collegeRoleLookupIds.Count == 0)
                return;

            foreach (var roleId in collegeRoleLookupIds.Distinct())
            {
                await RemoveExclusiveCollegeRoleFromOthersAsync(collegeId, roleId, staffId, ct);
                await _context.AddAsync(new StaffCollegeRole
                {
                    StaffId = staffId,
                    CollegeRoleLookupId = roleId
                });
            }

            await _context.SaveChangesAsync(ct);
        }

        private async Task ReplaceCollegeRolesAsync(int staffId, int collegeId, List<int>? collegeRoleLookupIds,
            CancellationToken ct)
        {
            var existing = await _context.StaffCollegeRoles.Where(r => r.StaffId == staffId).ToListAsync(ct);
            foreach (var e in existing)
                _context.Remove(e);

            await _context.SaveChangesAsync(ct);
            await ApplyCollegeRolesAsync(staffId, collegeId, collegeRoleLookupIds, ct);
        }

        private async Task RemoveExclusiveCollegeRoleFromOthersAsync(int collegeId, int collegeRoleLookupId,
            int keepingStaffId, CancellationToken ct)
        {
            var meta = await _context.CollegeRoleLookups.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == collegeRoleLookupId, ct);
            if (meta == null || !meta.IsExclusivePerCollege)
                return;

            var others = await _context.StaffCollegeRoles
                .Where(scr =>
                    scr.CollegeRoleLookupId == collegeRoleLookupId
                    && scr.StaffId != keepingStaffId
                    && scr.Staff.CollegeId == collegeId)
                .ToListAsync(ct);

            foreach (var row in others)
                _context.Remove(row);

            if (others.Count > 0)
                await _context.SaveChangesAsync(ct);
        }

        private async Task ApplySubjectAssignmentsAsync(int staffId, List<int>? subjectIds, CancellationToken ct)
        {
            if (subjectIds == null || subjectIds.Count == 0)
                return;

            foreach (var sid in subjectIds.Distinct())
            {
                var exists = await _context.Subjects.AsNoTracking().AnyAsync(s => s.Id == sid, ct);
                if (!exists)
                    throw new InvalidOperationException($"Subject {sid} not found.");

                await _context.AddAsync(new StaffSubjectAssignment
                {
                    StaffId = staffId,
                    SubjectId = sid
                });
            }

            await _context.SaveChangesAsync(ct);
        }

        private async Task ReplaceSubjectAssignmentsAsync(int staffId, List<int>? subjectIds, CancellationToken ct)
        {
            var existing = await _context.StaffSubjectAssignments.Where(a => a.StaffId == staffId).ToListAsync(ct);
            foreach (var e in existing)
                _context.Remove(e);

            await _context.SaveChangesAsync(ct);
            await ApplySubjectAssignmentsAsync(staffId, subjectIds, ct);
        }

        private async Task ClearTeachingAssociationsAsync(int staffId, CancellationToken ct)
        {
            var sd = await _context.StaffDepartments.Where(x => x.StaffId == staffId).ToListAsync(ct);
            foreach (var x in sd)
                _context.Remove(x);

            var scr = await _context.StaffCollegeRoles.Where(x => x.StaffId == staffId).ToListAsync(ct);
            foreach (var x in scr)
                _context.Remove(x);

            var ssa = await _context.StaffSubjectAssignments.Where(x => x.StaffId == staffId).ToListAsync(ct);
            foreach (var x in ssa)
                _context.Remove(x);

            await _context.SaveChangesAsync(ct);
        }

        /// <summary>
        /// JSON date-only values deserialize as <see cref="DateTimeKind.Unspecified"/>; Npgsql requires UTC for timestamptz.
        /// </summary>
        private static DateTime? NormalizeUtcDate(DateTime? value)
        {
            if (!value.HasValue)
                return null;

            var d = value.Value;
            return d.Kind switch
            {
                DateTimeKind.Utc => d,
                DateTimeKind.Local => d.ToUniversalTime(),
                _ => DateTime.SpecifyKind(d, DateTimeKind.Utc)
            };
        }
    }
}
