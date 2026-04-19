using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Lookup;
using Abhyanvaya.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.API.Controllers;

[ApiController]
[Route("api/elective-group")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class ElectiveGroupController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ElectiveGroupController(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var data = await _context.ElectiveGroups
            .AsNoTracking()
            .OrderBy(x => x.Course!.Name)
            .ThenBy(x => x.Semester!.Number)
            .ThenBy(x => x.Group!.Name)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.CourseId,
                CourseName = x.Course != null ? x.Course.Name : "",
                x.SemesterId,
                SemesterName = x.Semester != null ? x.Semester.Name : "",
                x.GroupId,
                GroupName = x.Group != null ? x.Group.Name : ""
            })
            .ToListAsync();

        return Ok(data);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateElectiveGroupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Name is required.");

        var tid = _currentUser.TenantId;
        var validation = await ValidateElectiveHierarchyAsync(request.CourseId, request.SemesterId, request.GroupId, tid);
        if (validation != null)
            return validation;

        var name = request.Name.Trim();
        var dup = await _context.ElectiveGroups.AnyAsync(x =>
            x.TenantId == tid &&
            x.CourseId == request.CourseId &&
            x.SemesterId == request.SemesterId &&
            x.GroupId == request.GroupId &&
            x.Name.ToLower() == name.ToLower());

        if (dup)
            return BadRequest("An elective group with this name already exists for this course, semester and group.");

        var entity = new ElectiveGroup
        {
            Name = name,
            CourseId = request.CourseId,
            SemesterId = request.SemesterId,
            GroupId = request.GroupId,
            CreatedDate = DateTime.UtcNow
        };

        await _context.AddAsync(entity);
        await _context.SaveChangesAsync();

        return Ok(entity);
    }

    [HttpPut]
    public async Task<IActionResult> Update(UpdateElectiveGroupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Name is required.");

        var entity = await _context.ElectiveGroups.FirstOrDefaultAsync(x => x.Id == request.Id);
        if (entity == null)
            return NotFound();

        var tid = _currentUser.TenantId;
        var validation = await ValidateElectiveHierarchyAsync(request.CourseId, request.SemesterId, request.GroupId, tid);
        if (validation != null)
            return validation;

        var name = request.Name.Trim();
        var dup = await _context.ElectiveGroups.AnyAsync(x =>
            x.Id != request.Id &&
            x.TenantId == tid &&
            x.CourseId == request.CourseId &&
            x.SemesterId == request.SemesterId &&
            x.GroupId == request.GroupId &&
            x.Name.ToLower() == name.ToLower());

        if (dup)
            return BadRequest("Another elective group already uses this name for this course, semester and group.");

        entity.Name = name;
        entity.CourseId = request.CourseId;
        entity.SemesterId = request.SemesterId;
        entity.GroupId = request.GroupId;
        entity.UpdatedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(entity);
    }

    private async Task<IActionResult?> ValidateElectiveHierarchyAsync(int courseId, int semesterId, int groupId, int tenantId)
    {
        var courseOk = await _context.Courses.AnyAsync(c => c.Id == courseId && c.TenantId == tenantId);
        if (!courseOk)
            return BadRequest("Invalid course.");

        var groupOk = await _context.Groups.AnyAsync(g =>
            g.Id == groupId && g.CourseId == courseId && g.TenantId == tenantId);
        if (!groupOk)
            return BadRequest("Invalid group for this course.");

        var semester = await _context.Semesters.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == semesterId && s.TenantId == tenantId);

        if (semester == null)
            return BadRequest("Invalid semester.");

        if (semester.CourseId != courseId)
            return BadRequest("Semester does not belong to the selected course.");

        if (semester.GroupId.HasValue && semester.GroupId.Value != groupId)
            return BadRequest("Semester is tied to a different group. Pick the matching group or another semester.");

        return null;
    }
}
