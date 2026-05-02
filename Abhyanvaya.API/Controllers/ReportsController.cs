using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Abhyanvaya.API.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = AuthorizationPolicies.CanViewReports)]
    public class ReportsController : ControllerBase
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly ILogger<AttendanceController> _logger;

        public ReportsController(
            IApplicationDbContext context,
            ICurrentUserService currentUser,
            ILogger<AttendanceController> logger)
        {
            _context = context;
            _currentUser = currentUser;
            _logger = logger;
        }

        [HttpGet("student")]
        public async Task<IActionResult> GetStudentReport(string studentNumber)
        {
            var student = await _context.Students
                .FirstOrDefaultAsync(x => x.StudentNumber == studentNumber);

            if (student == null)
                return NotFound();

            var report = await _context.Attendances
                .Where(x => x.StudentId == student.Id)
                .GroupBy(x => x.Subject.TenantSubject.Name)
                .Select(g => new
                {
                    Subject = g.Key,
                    Total = g.Count(),
                    Present = g.Count(x => x.Status == AttendanceStatus.Present),
                    Percentage = (g.Count(x => x.Status == AttendanceStatus.Present) * 100.0) / g.Count()
                })
                .ToListAsync();

            return Ok(report);
        }

        [HttpGet("subject")]
        public async Task<IActionResult> GetSubjectReport(int subjectId)
        {
            var report = await _context.Attendances
                .Where(x => x.SubjectId == subjectId)
                .GroupBy(x => x.Student.StudentNumber)
                .Select(g => new
                {
                    Student = g.Key,
                    Total = g.Count(),
                    Present = g.Count(x => x.Status == AttendanceStatus.Present),
                    Percentage = (g.Count(x => x.Status == AttendanceStatus.Present) * 100.0) / g.Count()
                })
                .ToListAsync();

            return Ok(report);
        }

        [HttpGet("monthly")]
        public async Task<IActionResult> GetMonthlyReport(string studentNumber, int month, int year)
        {
            var student = await _context.Students
                .FirstOrDefaultAsync(x => x.StudentNumber == studentNumber);

            if (student == null)
                return NotFound();

            var report = await _context.Attendances
                .Where(x =>
                    x.StudentId == student.Id &&
                    x.Date.Month == month &&
                    x.Date.Year == year)
                .GroupBy(x => x.Subject.TenantSubject.Name)
                .Select(g => new
                {
                    Subject = g.Key,
                    Total = g.Count(),
                    Present = g.Count(x => x.Status == AttendanceStatus.Present),
                    Percentage = (g.Count(x => x.Status == AttendanceStatus.Present) * 100.0) / g.Count()
                })
                .ToListAsync();

            return Ok(report);
        }

    }
}



