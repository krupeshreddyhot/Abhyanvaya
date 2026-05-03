using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.API.Common;
using Abhyanvaya.Application.DTOs.Course;
using Abhyanvaya.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.API.Controllers
{
    [ApiController]
    [Route("api/course")]
    [Authorize(Policy = AuthorizationPolicies.CanManageCourses)]
    public class CourseController : ControllerBase
    {
        private readonly IApplicationDbContext _context;
        private readonly ICacheService _cache;
        private readonly ICurrentUserService _currentUser;

        public CourseController(IApplicationDbContext context, ICacheService cache, ICurrentUserService currentUser)
        {
            _context = context;
            _cache = cache;
            _currentUser = currentUser;
        }
        private static string CoursesCacheKey(int tenantId) => $"tenant:{tenantId}:master:courses";

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var data = await _context.Courses
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new { x.Id, x.Code, x.Name })
                .ToListAsync();

            return Ok(data);
        }

        // ADD
        [HttpPost]
        public async Task<IActionResult> Create(CreateCourseRequest request)
        {
            var code = (request.Code ?? "").Trim().ToUpperInvariant();
            var name = (request.Name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
                return BadRequest("Course code and name are required.");

            var exists = await _context.Courses
                .AnyAsync(x =>
                    x.TenantId == _currentUser.TenantId &&
                    (x.Name.ToLower() == name.ToLower() || x.Code.ToLower() == code.ToLower()));

            if (exists)
                return BadRequest("Course code or name already exists.");

            var course = new Course
            {
                Code = code,
                Name = name,
                CreatedDate = DateTime.UtcNow
            };

            await _context.AddAsync(course);
            await _context.SaveChangesAsync();

            // invalidate cache
            await _cache.RemoveAsync(CoursesCacheKey(course.TenantId));

            return Ok(course);
        }

        // UPDATE
        [HttpPut]
        public async Task<IActionResult> Update(UpdateCourseRequest request)
        {
            var code = (request.Code ?? "").Trim().ToUpperInvariant();
            var name = (request.Name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
                return BadRequest("Course code and name are required.");

            var course = await _context.Courses.FirstOrDefaultAsync(x => x.Id == request.Id);

            if (course == null)
                return NotFound();

            var dup = await _context.Courses.AnyAsync(x =>
                x.Id != request.Id &&
                x.TenantId == _currentUser.TenantId &&
                (x.Name.ToLower() == name.ToLower() || x.Code.ToLower() == code.ToLower()));

            if (dup)
                return BadRequest("Another course already uses this code or name.");

            course.Code = code;
            course.Name = name;
            course.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _cache.RemoveAsync(CoursesCacheKey(course.TenantId));

            return Ok(course);
        }
    }
}



