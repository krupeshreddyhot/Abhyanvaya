using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Course;
using Abhyanvaya.Application.DTOs.Group;
using Abhyanvaya.Application.DTOs.Semester;
using Abhyanvaya.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Abhyanvaya.API.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.API.Controllers
{
    [ApiController]
    [Route("api/semester")]
    [Authorize(Policy = AuthorizationPolicies.CanManageSemesters)]
    public class SemesterController : ControllerBase
    {
        private readonly IApplicationDbContext _context;
        private readonly ICacheService _cache;
        private readonly ICurrentUserService _currentUser;

        public SemesterController(IApplicationDbContext context, ICacheService cache, ICurrentUserService currentUser)
        {
            _context = context;
            _cache = cache;
            _currentUser = currentUser;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var data = await _context.Semesters
                .AsNoTracking()
                .OrderBy(x => x.CourseId)
                .ThenBy(x => x.Number)
                .Select(x => new
                {
                    x.Id,
                    x.Number,
                    x.Name,
                    x.CourseId,
                    CourseName = x.Course != null ? x.Course.Name : "",
                    x.GroupId,
                    GroupName = x.Group != null ? x.Group.Name : (string?)null
                })
                .ToListAsync();

            return Ok(data);
        }

        // ADD
        [HttpPost]
        public async Task<IActionResult> Create(CreateSemesterRequest request)
        {
            var exists = await _context.Semesters
                .AnyAsync(x =>
                    x.TenantId == _currentUser.TenantId &&
                    x.CourseId == request.CourseId &&
                    x.GroupId == request.GroupId &&
                    x.Number == request.Number);

            if (exists)
                return BadRequest("A semester with this number already exists for this course and group.");

            if (!await _context.Courses.AnyAsync(x => x.Id == request.CourseId))
                return BadRequest("Invalid Course");
            if (request.GroupId.HasValue)
            {
                if (!await _context.Groups.AnyAsync(x =>
                        x.Id == request.GroupId.Value && x.CourseId == request.CourseId))
                    return BadRequest("Invalid group for this course.");
            }

            var semester = new Semester
            {
                Name = request.Name,
                Number = request.Number,
                CourseId = request.CourseId,    
                GroupId = request.GroupId,
                TenantId = _currentUser.TenantId,
                CreatedDate = DateTime.UtcNow
            };

            await _context.AddAsync(semester);
            await _context.SaveChangesAsync();

            // invalidate cache
            await _cache.RemoveAsync("master:semester");

            return Ok(semester);
        }

        // UPDATE
        [HttpPut]
        public async Task<IActionResult> Update(UpdateSemesterRequest request)
        {
            var semester = await _context.Semesters.FirstOrDefaultAsync(x => x.Id == request.Id);

            if (semester == null)
                return NotFound();
            if (!await _context.Courses.AnyAsync(x => x.Id == request.CourseId))
                return BadRequest("Invalid course");
            if (request.GroupId.HasValue)
            {
                if (!await _context.Groups.AnyAsync(x =>
                        x.Id == request.GroupId.Value && x.CourseId == request.CourseId))
                    return BadRequest("Invalid group for this course.");
            }

            semester.Name = request.Name;
            semester.Number = request.Number;
            semester.CourseId = request.CourseId;
            semester.GroupId = request.GroupId;
            semester.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _cache.RemoveAsync("master:semester");

            return Ok(semester);
        }
    }
}


