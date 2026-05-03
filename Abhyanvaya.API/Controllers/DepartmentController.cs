using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Department;
using Abhyanvaya.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = AuthorizationPolicies.TenantScopedUser)]
    public class DepartmentController : ControllerBase
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;

        public DepartmentController(IApplicationDbContext context, ICurrentUserService currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.TenantScopedAdmin)]
        public async Task<IActionResult> List([FromQuery] int? collegeId = null)
        {
            var q = _context.Departments.AsNoTracking();
            if (collegeId is > 0)
                q = q.Where(d => d.CollegeId == collegeId);

            var list = await q
                .OrderBy(d => d.CollegeId)
                .ThenBy(d => d.SortOrder)
                .ThenBy(d => d.Name)
                .Select(d => new DepartmentDto
                {
                    Id = d.Id,
                    CollegeId = d.CollegeId,
                    Name = d.Name,
                    Code = d.Code,
                    SortOrder = d.SortOrder
                })
                .ToListAsync();

            return Ok(list);
        }

        [HttpGet("{id:int}")]
        [Authorize(Policy = AuthorizationPolicies.TenantScopedAdmin)]
        public async Task<IActionResult> Get(int id)
        {
            var dto = await _context.Departments.AsNoTracking()
                .Where(d => d.Id == id)
                .Select(d => new DepartmentDto
                {
                    Id = d.Id,
                    CollegeId = d.CollegeId,
                    Name = d.Name,
                    Code = d.Code,
                    SortOrder = d.SortOrder
                })
                .FirstOrDefaultAsync();

            return dto == null ? NotFound() : Ok(dto);
        }

        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.TenantScopedAdmin)]
        public async Task<IActionResult> Create([FromBody] CreateDepartmentRequest request)
        {
            var college = await _context.Colleges.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.CollegeId);
            if (college == null)
                return BadRequest("College not found.");

            var dept = new Department
            {
                CollegeId = request.CollegeId,
                Name = request.Name.Trim(),
                Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim(),
                SortOrder = request.SortOrder
            };

            await _context.AddAsync(dept);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = dept.Id }, new DepartmentDto
            {
                Id = dept.Id,
                CollegeId = dept.CollegeId,
                Name = dept.Name,
                Code = dept.Code,
                SortOrder = dept.SortOrder
            });
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = AuthorizationPolicies.TenantScopedAdmin)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateDepartmentRequest request)
        {
            var dept = await _context.Departments.FirstOrDefaultAsync(d => d.Id == id);
            if (dept == null)
                return NotFound();

            dept.Name = request.Name.Trim();
            dept.Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim();
            dept.SortOrder = request.SortOrder;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = AuthorizationPolicies.TenantScopedAdmin)]
        public async Task<IActionResult> Delete(int id)
        {
            var dept = await _context.Departments.FirstOrDefaultAsync(d => d.Id == id);
            if (dept == null)
                return NotFound();

            dept.IsDeleted = true;
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
