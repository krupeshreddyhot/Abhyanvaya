using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Abhyanvaya.API.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Abhyanvaya.API.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly ILogger<AttendanceController> _logger;
        private readonly IConfiguration _configuration;

        public DashboardController(
            IApplicationDbContext context,
            ICurrentUserService currentUser,
            ILogger<AttendanceController> logger,
            IConfiguration configuration)
        {
            _context = context;
            _currentUser = currentUser;
            _logger = logger;
            _configuration = configuration;
        }

        [HttpGet("overview")]
        [Authorize(Policy = AuthorizationPolicies.DashboardOverviewAccess)]
        public async Task<IActionResult> GetOverview()
        {
            if (string.Equals(_currentUser.Role, nameof(UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new
                {
                    TotalStudents = 0,
                    TotalSubjects = 0,
                    TotalAttendance = 0,
                    TotalPresent = 0,
                    OverallPercentage = 0d,
                    TodayPresent = 0,
                    TodayAbsent = 0
                });
            }

            var studentsQuery = _context.Students
                .Where(x => x.TenantId == _currentUser.TenantId);

            var subjectsQuery = _context.Subjects
                .Where(x => x.TenantId == _currentUser.TenantId);

            var attendanceQuery = _context.Attendances
                .Where(x => x.TenantId == _currentUser.TenantId);

            if (_currentUser.Role.Equals("Faculty", StringComparison.OrdinalIgnoreCase))
            {
                if (_currentUser.StaffId > 0)
                {
                    var assignedIds = await FacultySubjectAccess.GetAssignedSubjectIdsAsync(
                            _context,
                            _currentUser.StaffId,
                            HttpContext.RequestAborted)
                        .ConfigureAwait(false);

                    if (assignedIds.Count == 0)
                    {
                        return Ok(new
                        {
                            TotalStudents = 0,
                            TotalSubjects = 0,
                            TotalAttendance = 0,
                            TotalPresent = 0,
                            OverallPercentage = 0d,
                            TodayPresent = 0,
                            TodayAbsent = 0
                        });
                    }

                    var staffId = _currentUser.StaffId;
                    subjectsQuery = subjectsQuery.Where(s => assignedIds.Contains(s.Id));
                    attendanceQuery = attendanceQuery.Where(a => assignedIds.Contains(a.SubjectId));

                    studentsQuery = studentsQuery.Where(student =>
                        _context.StaffSubjectAssignments.Any(ssa =>
                            ssa.StaffId == staffId &&
                            assignedIds.Contains(ssa.SubjectId) &&
                            _context.Subjects.Any(sub =>
                                sub.Id == ssa.SubjectId &&
                                (
                                    (!sub.IsElective &&
                                     student.CourseId == sub.CourseId &&
                                     student.GroupId == sub.GroupId &&
                                     student.SemesterId == sub.SemesterId)
                                    ||
                                    (sub.IsElective &&
                                     _context.StudentSubjects.Any(ss =>
                                         ss.StudentId == student.Id && ss.SubjectId == sub.Id))))));
                }
                else
                {
                    studentsQuery = studentsQuery.Where(x =>
                        x.CourseId == _currentUser.CourseId &&
                        x.GroupId == _currentUser.GroupId);

                    subjectsQuery = subjectsQuery.Where(x =>
                        x.CourseId == _currentUser.CourseId &&
                        x.GroupId == _currentUser.GroupId);

                    attendanceQuery = attendanceQuery.Where(x =>
                        x.Student.CourseId == _currentUser.CourseId &&
                        x.Student.GroupId == _currentUser.GroupId);
                }
            }

            var totalStudents = await studentsQuery.CountAsync();
            var totalSubjects = await subjectsQuery.CountAsync();
            var totalAttendance = await attendanceQuery.CountAsync();
            var totalPresent = await attendanceQuery.CountAsync(x => x.Status == AttendanceStatus.Present);

            // Attendance rows store UTC instants for the start of a reporting-zone calendar day.
            var tz = ReportingCalendar.ResolveReportingTimeZone(_configuration["Dashboard:ReportingTimeZoneId"]);
            var (dayStartUtc, dayEndUtc) = ReportingCalendar.GetReportingDayUtcRangeForUtcNow(DateTime.UtcNow, tz);
            var todayAttendance = await attendanceQuery
                .Where(x => x.Date >= dayStartUtc && x.Date < dayEndUtc)
                .ToListAsync();

            var todayPresent = todayAttendance.Count(x => x.Status == AttendanceStatus.Present);
            var todayAbsent = todayAttendance.Count(x => x.Status == AttendanceStatus.Absent);

            var overallPercentage = totalAttendance == 0
                ? 0
                : (totalPresent * 100.0) / totalAttendance;

            return Ok(new
            {
                TotalStudents = totalStudents,
                TotalSubjects = totalSubjects,
                TotalAttendance = totalAttendance,
                TotalPresent = totalPresent,
                OverallPercentage = Math.Round(overallPercentage, 2),
                TodayPresent = todayPresent,
                TodayAbsent = todayAbsent
            });
        }

        [HttpGet("student")]
        [Authorize(Policy = AuthorizationPolicies.TenantScopedUser)]
        public async Task<IActionResult> GetStudentDashboard()
        {
            var student = await _context.Students
                .FirstOrDefaultAsync(x => x.Id == _currentUser.UserId);

            if (student == null)
                return NotFound();

            var data = await _context.Attendances
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

            var overallTotal = data.Sum(x => x.Total);
            var overallPresent = data.Sum(x => x.Present);

            var overallPercentage = overallTotal == 0
                ? 0
                : (overallPresent * 100.0) / overallTotal;

            return Ok(new
            {
                OverallPercentage = overallPercentage,
                Subjects = data
            });
        }
        [HttpGet("low-attendance")]
        [Authorize(Policy = AuthorizationPolicies.TenantScopedUser)]
        public async Task<IActionResult> GetLowAttendance(double threshold = 75)
        {
            var result = await _context.Attendances
                .Where(x => x.StudentId == _currentUser.UserId)
                .GroupBy(x => x.Subject.TenantSubject.Name)
                .Select(g => new
                {
                    Subject = g.Key,
                    Percentage = (g.Count(x => x.Status == AttendanceStatus.Present) * 100.0) / g.Count()
                })
                .Where(x => x.Percentage < threshold)
                .ToListAsync();

            return Ok(result);
        }
        [HttpGet("monthly-trend")]
        [Authorize(Policy = AuthorizationPolicies.TenantScopedUser)]
        public async Task<IActionResult> GetMonthlyTrend(int month, int year)
        {
            var tz = ReportingCalendar.ResolveReportingTimeZone(_configuration["Dashboard:ReportingTimeZoneId"]);
            var monthStart = ReportingCalendar.GetUtcRangeForReportingCalendarDate(year, month, 1, tz).StartUtcInclusive;
            var lastDay = DateTime.DaysInMonth(year, month);
            var monthEndExclusive = ReportingCalendar
                .GetUtcRangeForReportingCalendarDate(year, month, lastDay, tz)
                .EndUtcExclusive;

            var rows = await _context.Attendances
                .Where(x =>
                    x.StudentId == _currentUser.UserId &&
                    x.Date >= monthStart &&
                    x.Date < monthEndExclusive)
                .Select(x => new { x.Date, x.Status })
                .ToListAsync();

            var data = rows
                .GroupBy(x =>
                {
                    var utc = DateTime.SpecifyKind(x.Date, DateTimeKind.Utc);
                    var local = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
                    return new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified);
                })
                .Select(g => new
                {
                    Date = TimeZoneInfo.ConvertTimeToUtc(g.Key, tz),
                    Present = g.Count(x => x.Status == AttendanceStatus.Present),
                    Total = g.Count(),
                })
                .OrderBy(x => x.Date)
                .ToList();

            return Ok(data);
        }
        [HttpGet("class")]
        [Authorize(Policy = AuthorizationPolicies.TenantScopedUser)]
        public async Task<IActionResult> GetClassDashboard(int subjectId, DateTime date)
        {
            var subjectRow = await _context.Subjects.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == subjectId && x.TenantId == _currentUser.TenantId);
            if (subjectRow == null)
                return NotFound();

            if (!await FacultySubjectAccess.FacultyMayAccessSubjectAsync(
                    _context,
                    _currentUser,
                    subjectRow.Id,
                    HttpContext.RequestAborted)
                .ConfigureAwait(false))
                return Forbid();

            var tz = ReportingCalendar.ResolveReportingTimeZone(_configuration["Dashboard:ReportingTimeZoneId"]);
            var utc = ReportingCalendar.NormalizeToUtc(date);
            var (dayStartUtc, dayEndUtc) = ReportingCalendar.GetUtcRangeForReportingDayContainingUtc(utc, tz);

            var data = await _context.Attendances
                .Where(x =>
                    x.SubjectId == subjectId &&
                    x.Date >= dayStartUtc &&
                    x.Date < dayEndUtc &&
                    x.TenantId == _currentUser.TenantId)
                .GroupBy(x => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Present = g.Count(x => x.Status == AttendanceStatus.Present),
                    Absent = g.Count(x => x.Status == AttendanceStatus.Absent)
                })
                .FirstOrDefaultAsync();

            if (data == null)
                return NotFound();

            var percentage = (data.Present * 100.0) / data.Total;

            return Ok(new
            {
                data.Total,
                data.Present,
                data.Absent,
                Percentage = percentage
            });
        }

        [HttpGet("subject-performance")]
        [Authorize(Policy = AuthorizationPolicies.TenantScopedUser)]
        public async Task<IActionResult> GetSubjectPerformance(int subjectId)
        {
            var subjectRow = await _context.Subjects.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == subjectId && x.TenantId == _currentUser.TenantId);
            if (subjectRow == null)
                return NotFound();

            if (!await FacultySubjectAccess.FacultyMayAccessSubjectAsync(
                    _context,
                    _currentUser,
                    subjectRow.Id,
                    HttpContext.RequestAborted)
                .ConfigureAwait(false))
                return Forbid();

            var data = await _context.Attendances
                .Where(x => x.SubjectId == subjectId && x.TenantId == _currentUser.TenantId)
                .GroupBy(x => x.Student.StudentNumber)
                .Select(g => new
                {
                    Student = g.Key,
                    Percentage = (g.Count(x => x.Status == AttendanceStatus.Present) * 100.0) / g.Count()
                })
                .ToListAsync();

            return Ok(data);
        }

    }
}


