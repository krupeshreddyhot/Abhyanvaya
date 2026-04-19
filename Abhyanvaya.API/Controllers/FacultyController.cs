using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs;
using Abhyanvaya.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrFaculty)]
    public class FacultyController : ControllerBase
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;

        public FacultyController(IApplicationDbContext context, ICurrentUserService currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        [HttpGet("students")]
        public async Task<IActionResult> GetStudents(int subjectId, string? search = null)
        {
            var subject = await _context.Subjects
                .FirstOrDefaultAsync(x => x.Id == subjectId);

            if (subject == null)
                return NotFound("Subject not found");

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

            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();

                query = query.Where(x =>
                    x.StudentNumber.ToLower().Contains(search) ||
                    x.Name.ToLower().Contains(search) ||
                    x.MobileNumber.Contains(search)
                );
            }

            var students = await query
                .Select(x => new
                {
                    x.Id,
                    x.StudentNumber,
                    x.Name,
                    x.MobileNumber
                })
                .ToListAsync();

            return Ok(students);
        }
    }
}
