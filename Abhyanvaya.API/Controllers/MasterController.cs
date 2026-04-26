using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Abhyanvaya.API.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Application.Common.Extensions;
using Abhyanvaya.Application.DTOs.Course;

namespace Abhyanvaya.API.Controllers
{
    [ApiController]
    [Route("api/master")]
    [Authorize(Policy = AuthorizationPolicies.TenantScopedUser)]
    public class MasterController : ControllerBase
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly ILogger<AttendanceController> _logger;
        private readonly ICacheService _cache;

        public MasterController(
            IApplicationDbContext context,
            ICurrentUserService currentUser,
            ILogger<AttendanceController> logger,
            ICacheService Cache)
        {
            _context = context;
            _currentUser = currentUser;
            _logger = logger;
            _cache = Cache;
        }
        [HttpGet("courses")]
        public async Task<IActionResult> GetCourses()
        {
            var cacheKey = $"tenant:{_currentUser.TenantId}:master:courses";

            var cached = await _cache.GetAsync<List<CourseDto>>(cacheKey);
            if (cached != null)
            {
                Console.WriteLine("✅ RETURNING FROM CACHE");
                return Ok(cached);
            }

            Console.WriteLine("❌ FETCHING FROM DB");

            var data = await _context.Courses
                .AsNoTracking()
                .Select(x => new CourseDto
                {
                    Id = x.Id,
                    Code = x.Code,
                    Name = x.Name
                })
                .ToListAsync();

            await _cache.SetAsync(cacheKey, data, TimeSpan.FromHours(12));

            return Ok(data);
        }

        [HttpGet("semesters")]
        public async Task<IActionResult> GetSemesters()
        {
            var cacheKey = $"tenant:{_currentUser.TenantId}:master:semesters";

            var cached = await _cache.GetAsync<List<object>>(cacheKey);
            if (cached != null)
                return Ok(cached);

            var data = await _context.Semesters
                .AsNoTracking()
                .Select(x => new { x.Id, x.Name })
                .ToListAsync();

            await _cache.SetAsync(cacheKey, data, TimeSpan.FromHours(12));

            return Ok(data);
        }

        [HttpGet("genders")]
        public async Task<IActionResult> GetGenders()
        {
            var data = await _context.Genders
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new { x.Id, x.Name })
                .ToListAsync();
            return Ok(data);
        }

        [HttpGet("mediums")]
        public async Task<IActionResult> GetMediums()
        {
            var data = await _context.Mediums
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new { x.Id, x.Name })
                .ToListAsync();
            return Ok(data);
        }

        [HttpGet("languages")]
        public async Task<IActionResult> GetLanguages()
        {
            var data = await _context.Languages
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new { x.Id, x.Name })
                .ToListAsync();
            return Ok(data);
        }

        [HttpGet("subjects")]
        public async Task<IActionResult> GetSubjects(int courseId, int groupId, int semesterId)
        {
            var subjects = await _context.Subjects
                .Where(x =>
                    x.CourseId == courseId &&
                    x.GroupId == groupId &&
                    x.SemesterId == semesterId)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Code,
                    x.IsElective
                })
                .ToListAsync();

            return Ok(subjects);
        }

        [HttpGet("groups")]
        public async Task<IActionResult> GetGroups(int? courseId = null)
        {
            var query = _context.Groups.AsNoTracking();

            if (courseId.HasValue && courseId.Value > 0)
            {
                query = query.Where(x => x.CourseId == courseId.Value);
            }

            var groups = await query
                .OrderBy(x => x.Name)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Code,
                    x.CourseId
                })
                .ToListAsync();

            return Ok(groups);
        }

        [HttpGet("faculty-subjects")]
        public async Task<IActionResult> GetFacultySubjects()
        {
            if (!_currentUser.Role.Equals("Faculty", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            var subjects = await _context.Subjects
                .Where(x =>
                    x.CourseId == _currentUser.CourseId &&
                    x.GroupId == _currentUser.GroupId)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.SemesterId
                })
                .ToListAsync();

            return Ok(subjects);
        }

        [HttpGet("my-subjects")]
        public async Task<IActionResult> GetMySubjects()
        {
            var student = await _context.Students
                .FirstOrDefaultAsync(x => x.Id == _currentUser.UserId);

            if (student == null)
                return NotFound();

            var coreSubjects = _context.Subjects
                .Where(x =>
                    x.CourseId == student.CourseId &&
                    x.GroupId == student.GroupId &&
                    x.SemesterId == student.SemesterId &&
                    !x.IsElective)
                .Where(x =>
                    x.LanguageSubjectSlot == SubjectLanguageSlot.None ||
                    (x.LanguageSubjectSlot == SubjectLanguageSlot.FirstLanguage &&
                     x.TeachingLanguageId == student.FirstLanguageId) ||
                    (x.LanguageSubjectSlot == SubjectLanguageSlot.SecondLanguage &&
                     x.TeachingLanguageId == student.LanguageId));

            var electiveSubjects = _context.StudentSubjects
                .Where(x => x.StudentId == student.Id)
                .Select(x => x.Subject);

            var result = await coreSubjects
                .Union(electiveSubjects)
                .Select(x => new
                {
                    x.Id,
                    x.Name
                })
                .ToListAsync();

            return Ok(result);
        }

        [HttpGet("faculty-students")]
        public async Task<IActionResult> GetFacultyStudents(
            int subjectId,
            int pageNumber = 1,
            int pageSize = 20,
            string? search = null)
        {
            var subject = await _context.Subjects
                .FirstOrDefaultAsync(x => x.Id == subjectId);

            if (subject == null)
                return NotFound();

            IQueryable<Student> query;

            if (!subject.IsElective)
            {
                query = _context.Students
                    .Where(x =>
                        x.CourseId == subject.CourseId &&
                        x.GroupId == subject.GroupId &&
                        x.SemesterId == subject.SemesterId);
            }
            else
            {
                query = _context.StudentSubjects
                    .Where(x => x.SubjectId == subjectId)
                    .Select(x => x.Student);
            }

            if (!subject.IsElective)
                query = ApplyLanguageSubjectStudentFilter(query, subject);

            // GLOBAL SEARCH
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();

                query = query.Where(x =>
                    x.StudentNumber.ToLower().Contains(search) ||
                    x.Name.ToLower().Contains(search) ||
                    x.MobileNumber.Contains(search));
            }

            var paged = await query.ToPagedAsync(pageNumber, pageSize);

            var result = paged.Data.Select(x => new
            {
                x.StudentNumber,
                x.Name,
                x.MobileNumber
            });

            return Ok(new
            {
                paged.TotalCount,
                Data = result
            });
        }

        private static IQueryable<Student> ApplyLanguageSubjectStudentFilter(IQueryable<Student> query, Subject subject)
        {
            if (subject.IsElective)
                return query;

            return subject.LanguageSubjectSlot switch
            {
                SubjectLanguageSlot.FirstLanguage when subject.TeachingLanguageId.HasValue =>
                    query.Where(s => s.FirstLanguageId == subject.TeachingLanguageId.Value),
                SubjectLanguageSlot.SecondLanguage when subject.TeachingLanguageId.HasValue =>
                    query.Where(s => s.LanguageId == subject.TeachingLanguageId.Value),
                _ => query
            };
        }
    }

}



