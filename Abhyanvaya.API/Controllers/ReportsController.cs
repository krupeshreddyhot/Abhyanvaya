using Abhyanvaya.Application.Common.Interfaces;
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
        private readonly ITenantContextService _tenantContextService;
        private readonly ILogger<AttendanceController> _logger;

        public ReportsController(
            IApplicationDbContext context,
            ICurrentUserService currentUser,
            ITenantContextService tenantContextService,
            ILogger<AttendanceController> logger)
        {
            _context = context;
            _currentUser = currentUser;
            _tenantContextService = tenantContextService;
            _logger = logger;
        }

        private async Task<Student?> FindStudentForReportAsync(string studentNumber, int tenantId, CancellationToken ct) =>
            await _context.Students
                .Where(x => x.StudentNumber == studentNumber && x.TenantId == tenantId)
                .FirstOrDefaultAsync(ct);

        /// <summary>Legacy Faculty (no staff link) may only run reports for students in their JWT course/group.</summary>
        private IActionResult? LegacyFacultyStudentGate(Student student)
        {
            if (!_currentUser.Role.Equals("Faculty", StringComparison.OrdinalIgnoreCase))
                return null;
            if (_currentUser.StaffId > 0)
                return null;

            if (student.CourseId != _currentUser.CourseId || student.GroupId != _currentUser.GroupId)
                return Forbid();

            return null;
        }

        /// <summary>Staff-linked Faculty: attendance rows limited to <see cref="StaffSubjectAssignment"/> subjects.</summary>
        private async Task<IQueryable<Attendance>> ApplyStaffFacultySubjectFilterAsync(
            IQueryable<Attendance> query,
            CancellationToken ct)
        {
            if (!_currentUser.Role.Equals("Faculty", StringComparison.OrdinalIgnoreCase) || _currentUser.StaffId <= 0)
                return query;

            var ids = await FacultySubjectAccess.GetAssignedSubjectIdsAsync(_context, _currentUser.StaffId, ct)
                .ConfigureAwait(false);
            if (ids.Count == 0)
                return query.Where(_ => false);

            return query.Where(a => ids.Contains(a.SubjectId));
        }

        [HttpGet("student")]
        public async Task<IActionResult> GetStudentReport(string studentNumber)
        {
            if (this.RequireTenantContext(_tenantContextService, out var resolution) is { } contextError)
            {
                return contextError;
            }

            var ct = HttpContext.RequestAborted;

            var student = await FindStudentForReportAsync(studentNumber, resolution.EffectiveTenantId, ct);
            if (student == null)
                return NotFound();

            var legacyGate = LegacyFacultyStudentGate(student);
            if (legacyGate != null)
                return legacyGate;

            var attendanceQuery = _context.Attendances.Where(x => x.StudentId == student.Id);
            attendanceQuery = attendanceQuery.Where(x => x.TenantId == resolution.EffectiveTenantId);

            attendanceQuery = await ApplyStaffFacultySubjectFilterAsync(attendanceQuery, ct).ConfigureAwait(false);

            var report = await attendanceQuery
                .GroupBy(x => x.Subject.TenantSubject.Name)
                .Select(g => new
                {
                    Subject = g.Key,
                    Total = g.Count(),
                    Present = g.Count(x => x.Status == AttendanceStatus.Present),
                    Percentage = (g.Count(x => x.Status == AttendanceStatus.Present) * 100.0) / g.Count()
                })
                .ToListAsync(ct);

            return Ok(report);
        }

        [HttpGet("subject")]
        public async Task<IActionResult> GetSubjectReport(int subjectId)
        {
            if (this.RequireTenantContext(_tenantContextService, out var resolution) is { } contextError)
            {
                return contextError;
            }

            var subjectRow = await _context.Subjects.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == subjectId && x.TenantId == resolution.EffectiveTenantId);
            if (subjectRow == null)
                return NotFound();

            if (!await FacultySubjectAccess.FacultyMayAccessSubjectAsync(
                    _context,
                    _currentUser,
                    subjectRow.Id,
                    HttpContext.RequestAborted)
                .ConfigureAwait(false))
                return Forbid();

            var report = await _context.Attendances
                .Where(x => x.SubjectId == subjectId && x.TenantId == _currentUser.TenantId)
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
            if (this.RequireTenantContext(_tenantContextService, out var resolution) is { } contextError)
            {
                return contextError;
            }

            var ct = HttpContext.RequestAborted;

            var student = await FindStudentForReportAsync(studentNumber, resolution.EffectiveTenantId, ct);
            if (student == null)
                return NotFound();

            var legacyGate = LegacyFacultyStudentGate(student);
            if (legacyGate != null)
                return legacyGate;

            var attendanceQuery = _context.Attendances.Where(x =>
                x.StudentId == student.Id &&
                x.Date.Month == month &&
                x.Date.Year == year);

            attendanceQuery = attendanceQuery.Where(x => x.TenantId == resolution.EffectiveTenantId);

            attendanceQuery = await ApplyStaffFacultySubjectFilterAsync(attendanceQuery, ct).ConfigureAwait(false);

            var report = await attendanceQuery
                .GroupBy(x => x.Subject.TenantSubject.Name)
                .Select(g => new
                {
                    Subject = g.Key,
                    Total = g.Count(),
                    Present = g.Count(x => x.Status == AttendanceStatus.Present),
                    Percentage = (g.Count(x => x.Status == AttendanceStatus.Present) * 100.0) / g.Count()
                })
                .ToListAsync(ct);

            return Ok(report);
        }

    }
}



