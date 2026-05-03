using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Group;
using Abhyanvaya.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Abhyanvaya.API.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.API.Controllers
{
    [ApiController]
    [Route("api/group")]
    [Authorize(Policy = AuthorizationPolicies.CanManageGroups)]
    public class GroupController : ControllerBase
    {
        private readonly IApplicationDbContext _context;
        private readonly ICacheService _cache;
        private readonly ICurrentUserService _currentUser;

        public GroupController(IApplicationDbContext context, ICacheService cache, ICurrentUserService currentUser)
        {
            _context = context;
            _cache = cache;
            _currentUser = currentUser;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var data = await _context.Groups
                .AsNoTracking()
                .OrderBy(x => x.Course!.Name)
                .ThenBy(x => x.Name)
                .Select(x => new
                {
                    x.Id,
                    x.Code,
                    x.Name,
                    x.CourseId,
                    CourseName = x.Course != null ? x.Course.Name : ""
                })
                .ToListAsync();

            return Ok(data);
        }

        // ADD
        [HttpPost]
        public async Task<IActionResult> Create(CreateGroupRequest request)
        {
            var code = (request.Code ?? "").Trim().ToUpperInvariant();
            var name = (request.Name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
                return BadRequest("Group code, name and course are required.");

            var exists = await _context.Groups
                .AnyAsync(x =>
                    x.TenantId == _currentUser.TenantId &&
                    x.CourseId == request.CourseId &&
                    (x.Name.ToLower() == name.ToLower() || x.Code.ToLower() == code.ToLower()));

            if (exists)
                return BadRequest("A group with this code or name already exists for this course.");

            if (!await _context.Courses.AnyAsync(x => x.Id == request.CourseId))
                return BadRequest("Invalid course");

            var group = new Group
            {
                Code = code,
                Name = name,
                CourseId = request.CourseId,
                CreatedDate = DateTime.UtcNow
            };

            await _context.AddAsync(group);
            await _context.SaveChangesAsync();

            // invalidate cache
            await _cache.RemoveAsync("master:group");

            return Ok(group);
        }

        // UPDATE
        [HttpPut]
        public async Task<IActionResult> Update(UpdateGroupRequest request)
        {
            var code = (request.Code ?? "").Trim().ToUpperInvariant();
            var name = (request.Name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
                return BadRequest("Group code, name and course are required.");

            var group = await _context.Groups.FirstOrDefaultAsync(x => x.Id == request.Id);

            if (group == null)
                return NotFound();
            if (!await _context.Courses.AnyAsync(x => x.Id == request.CourseId))
                return BadRequest("Invalid course");

            var dup = await _context.Groups.AnyAsync(x =>
                x.Id != request.Id &&
                x.TenantId == _currentUser.TenantId &&
                x.CourseId == request.CourseId &&
                (x.Name.ToLower() == name.ToLower() || x.Code.ToLower() == code.ToLower()));

            if (dup)
                return BadRequest("A group with this code or name already exists for this course.");

            group.Code = code;
            group.Name = name;
            group.CourseId = request.CourseId;
            group.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _cache.RemoveAsync("master:group");

            return Ok(group);
        }
    }
}


