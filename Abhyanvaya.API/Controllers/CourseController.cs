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
                .Select(x => new { x.Id, x.Code, x.Name, x.ProgramId })
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

            try
            {
                var programId = await ResolveProgramIdAsync(request.ProgramId);
                var course = new Course
                {
                    Code = code,
                    Name = name,
                    ProgramId = programId,
                    CreatedDate = DateTime.UtcNow
                };

                await _context.AddAsync(course);
                await _context.SaveChangesAsync();

                await _cache.RemoveAsync(CoursesCacheKey(course.TenantId));
                return Ok(course);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
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
            // Additive: only update ProgramId when explicitly provided (existing Course CRUD unchanged).
            if (request.ProgramId.HasValue)
            {
                try { course.ProgramId = await ResolveProgramIdAsync(request.ProgramId); }
                catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            }
            course.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _cache.RemoveAsync(CoursesCacheKey(course.TenantId));

            return Ok(course);
        }

        /// <summary>AI29.1A — when Programs disabled, always null; when enabled, validate Active program.</summary>
        private async Task<int?> ResolveProgramIdAsync(int? programId)
        {
            if (!await ProgramsEnabledAsync())
                return null;
            if (programId is null or <= 0)
                return null;

            var program = await _context.Programs.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == programId && p.TenantId == _currentUser.TenantId);
            if (program is null)
                throw new InvalidOperationException("Invalid Program.");
            if (!program.IsActive || program.Status == "Archived")
                throw new InvalidOperationException("Archived Programs cannot receive new Courses.");
            return program.Id;
        }

        private async Task<bool> ProgramsEnabledAsync()
        {
            return await _context.TenantAcademicConfigurations.AsNoTracking()
                .Where(c => c.TenantId == _currentUser.TenantId)
                .Select(c => c.EnablePrograms)
                .FirstOrDefaultAsync();
        }
    }
}



