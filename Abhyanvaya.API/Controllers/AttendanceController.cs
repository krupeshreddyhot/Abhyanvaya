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
    [Authorize(Policy = AuthorizationPolicies.CanManageAttendance)]
    public class AttendanceController : ControllerBase
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly ILogger<AttendanceController> _logger;
        private readonly IConfiguration _configuration;

        public AttendanceController(
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

        private TimeZoneInfo ReportingTz =>
            ReportingCalendar.ResolveReportingTimeZone(_configuration["Dashboard:ReportingTimeZoneId"]);

        /// <summary>UTC half-open range [start, end) for the reporting calendar day implied by the client date.</summary>
        private (DateTime StartUtcInclusive, DateTime EndUtcExclusive) ReportingDayRange(DateTime clientDateTime)
        {
            var utc = ReportingCalendar.NormalizeToUtc(clientDateTime);
            return ReportingCalendar.GetUtcRangeForReportingDayContainingUtc(utc, ReportingTz);
        }

        [HttpPost("mark")]
        public async Task<IActionResult> MarkAttendance(MarkAttendanceRequest request)
        {
            if (request?.Students == null || !request.Students.Any())
                return BadRequest("Students list is required");

            var (dayStartUtc, dayEndUtc) = ReportingDayRange(request.Date);
            var localToday = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ReportingTz).Date;
            var localSelected = TimeZoneInfo.ConvertTimeFromUtc(dayStartUtc, ReportingTz).Date;
            if (localSelected > localToday)
                return BadRequest("Cannot mark future attendance");

            var subject = await _context.Subjects
                .FirstOrDefaultAsync(x => x.Id == request.SubjectId);

            if (subject == null)
                return BadRequest("Invalid subject");

            var student = await _context.Students
                .FirstOrDefaultAsync(x => x.StudentNumber == request.Students.FirstOrDefault().StudentNumber);

                if (!subject.IsElective)
                {
                    // must belong to course/group/semester
                    if (student.CourseId != subject.CourseId ||
                        student.GroupId != subject.GroupId ||
                        student.SemesterId != subject.SemesterId)
                    {
                        return BadRequest($"Invalid student: {student.StudentNumber}");
                    }

                    if (!StudentMatchesLanguageSubject(subject, student))
                    {
                        return BadRequest(
                            $"Student {student.StudentNumber} is not in this language cohort for the selected subject.");
                    }
                }
                else
            {
                // must exist in StudentSubjects
                var exists = await _context.StudentSubjects
                    .AnyAsync(x =>
                        x.StudentId == student.Id &&
                        x.SubjectId == subject.Id);

                if (!exists)
                    return BadRequest($"Student not mapped to elective: {student.StudentNumber}");
            }

            var alreadyExists = await _context.Attendances
                .AnyAsync(x =>
                    x.SubjectId == request.SubjectId &&
                    x.Date >= dayStartUtc &&
                    x.Date < dayEndUtc &&
                    x.TenantId == _currentUser.TenantId);

            if (alreadyExists)
                return BadRequest("Attendance already marked");

            var studentNumbers = request.Students
                .Select(x => x.StudentNumber)
                .ToList();

            var locked = await _context.Attendances
                .AnyAsync(x =>
                    x.SubjectId == request.SubjectId &&
                    x.Date >= dayStartUtc &&
                    x.Date < dayEndUtc &&
                    x.IsLocked &&
                    x.TenantId == _currentUser.TenantId);

            if (locked)
            {
                return BadRequest("Attendance is locked. Cannot modify.");
            }

            var query = _context.Students
                .Where(x =>
                    studentNumbers.Contains(x.StudentNumber) &&
                    x.TenantId == _currentUser.TenantId);

            if (_currentUser.Role.Equals("Faculty", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x =>
                    x.CourseId == _currentUser.CourseId &&
                    x.GroupId == _currentUser.GroupId);
            }

            query = ApplyLanguageSubjectFilter(query, subject);

            var students = await query.ToListAsync();

            var map = request.Students.ToDictionary(x => x.StudentNumber);

            // fetch existing once
            var existingRecords = await _context.Attendances
                .Where(x =>
                    x.SubjectId == request.SubjectId &&
                    x.Date >= dayStartUtc &&
                    x.Date < dayEndUtc &&
                    x.TenantId == _currentUser.TenantId)
                .Select(x => x.StudentId)
                .ToListAsync();

            var existingSet = existingRecords.ToHashSet();

            var attendanceList = new List<Attendance>();

            foreach (var stu in students)
            {
                if (!map.TryGetValue(stu.StudentNumber, out var dto))
                    continue;

                if (existingSet.Contains(stu.Id))
                    continue;

                attendanceList.Add(new Attendance
                {
                    StudentId = stu.Id,
                    SubjectId = request.SubjectId,
                    Date = dayStartUtc,
                    Status = dto.Status,
                    TenantId = _currentUser.TenantId
                });
            }

            _context.AddAttendances(attendanceList);
            await _context.SaveChangesAsync(CancellationToken.None);

            return Ok(new
            {
                Message = "Attendance saved successfully",
                Count = attendanceList.Count
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetAttendance(int subjectId, DateTime date)
        {
            var (dayStartUtc, dayEndUtc) = ReportingDayRange(date);

            var subject = await _context.Subjects
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == subjectId && x.TenantId == _currentUser.TenantId);

            if (subject == null)
                return BadRequest("Invalid subject.");

            var query = _context.Attendances
                .Where(x => x.TenantId == _currentUser.TenantId &&
                            x.SubjectId == subjectId &&
                            x.Date >= dayStartUtc &&
                            x.Date < dayEndUtc);

            query = ApplyLanguageSubjectFilterForAttendance(query, subject);

            // 🔐 Faculty restriction
            if (_currentUser.Role.Equals("Faculty", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x =>
                    x.Student.CourseId == _currentUser.CourseId &&
                    x.Student.GroupId == _currentUser.GroupId);
            }

            var result = await query
                .Select(x => new
                {
                    x.StudentId,
                    StudentName = x.Student.Name,
                    x.SubjectId,
                    x.Date,
                    x.Status
                })
                .ToListAsync();

            return Ok(result);
        }

        [HttpGet("students-for-marking")]
        public async Task<IActionResult> GetStudentsForMarking(
            int courseId,
            int groupId,
            int semesterId,
            int subjectId,
            DateTime date,
            string? search = null,
            int pageNumber = 1,
            int pageSize = 50)
        {
            if (courseId <= 0 || groupId <= 0 || semesterId <= 0 || subjectId <= 0)
                return BadRequest("Course, group, semester and subject are required.");
            if (pageNumber <= 0)
                pageNumber = 1;
            if (pageSize <= 0)
                pageSize = 50;
            if (pageSize > 200)
                pageSize = 200;

            var subject = await _context.Subjects
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == subjectId);

            if (subject == null)
                return BadRequest("Invalid subject.");

            if (subject.CourseId != courseId || subject.GroupId != groupId || subject.SemesterId != semesterId)
                return BadRequest("Selected subject does not belong to selected course/group/semester.");

            var query = _context.Students
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == _currentUser.TenantId &&
                    x.CourseId == courseId &&
                    x.GroupId == groupId &&
                    x.SemesterId == semesterId);

            if (_currentUser.Role.Equals("Faculty", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x =>
                    x.CourseId == _currentUser.CourseId &&
                    x.GroupId == _currentUser.GroupId);
            }

            query = ApplyLanguageSubjectFilter(query, subject);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(x =>
                    x.StudentNumber.ToLower().Contains(s) ||
                    x.Name.ToLower().Contains(s) ||
                    (x.MobileNumber != null && x.MobileNumber.ToLower().Contains(s)) ||
                    (x.AlternateMobileNumber != null && x.AlternateMobileNumber.ToLower().Contains(s)));
            }

            var (dayStartUtc, dayEndUtc) = ReportingDayRange(date);
            var tenantId = _currentUser.TenantId;

            // Present first (for the selected subject + date), then name A–Z
            var orderedQuery = query
                .OrderByDescending(s => _context.Attendances.AsNoTracking().Any(a =>
                    a.TenantId == tenantId &&
                    a.SubjectId == subjectId &&
                    a.StudentId == s.Id &&
                    a.Date >= dayStartUtc &&
                    a.Date < dayEndUtc &&
                    a.Status == AttendanceStatus.Present))
                .ThenBy(s => s.Name);

            var totalCount = await orderedQuery.CountAsync();

            var students = await orderedQuery
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.Id,
                    x.StudentNumber,
                    x.Batch,
                    x.Name,
                    x.MobileNumber,
                    x.AlternateMobileNumber,
                    x.Email
                })
                .ToListAsync();

            var existing = await _context.Attendances
                .Where(x =>
                    x.TenantId == _currentUser.TenantId &&
                    x.SubjectId == subjectId &&
                    x.Date >= dayStartUtc &&
                    x.Date < dayEndUtc)
                .Select(x => new
                {
                    x.StudentId,
                    x.Status,
                    x.IsLocked
                })
                .ToListAsync();

            var existingByStudent = existing.ToDictionary(x => x.StudentId, x => x.Status);
            var isLocked = existing.Any(x => x.IsLocked);

            var result = students
                .Select((x, index) => new
                {
                    SlNo = ((pageNumber - 1) * pageSize) + index + 1,
                    x.StudentNumber,
                    x.Batch,
                    x.Name,
                    x.MobileNumber,
                    x.AlternateMobileNumber,
                    Mobile = string.Join(" / ", new[] { x.MobileNumber, x.AlternateMobileNumber }
                        .Where(v => !string.IsNullOrWhiteSpace(v))),
                    x.Email,
                    Status = existingByStudent.TryGetValue(x.Id, out var st) ? st : AttendanceStatus.Absent
                })
                .ToList();

            return Ok(new
            {
                IsLocked = isLocked,
                AlreadyMarked = existing.Any(),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Students = result
            });
        }

        [HttpPost("lock")]
        public async Task<IActionResult> LockAttendance(int subjectId, DateTime date)
        {
            var (dayStartUtc, dayEndUtc) = ReportingDayRange(date);

            var subject = await _context.Subjects
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == subjectId &&
                    x.TenantId == _currentUser.TenantId);

            if (subject == null)
                return BadRequest("Invalid subject.");

            var recordsQuery = _context.Attendances
                .Where(x =>
                    x.SubjectId == subjectId &&
                    x.Date >= dayStartUtc &&
                    x.Date < dayEndUtc &&
                    x.TenantId == _currentUser.TenantId);

            recordsQuery = ApplyLanguageSubjectFilterForAttendance(recordsQuery, subject);

            var records = await recordsQuery.ToListAsync();

            if (!records.Any())
                return NotFound("No attendance found");

            foreach (var r in records)
            {
                r.IsLocked = true;
            }

            await _context.SaveChangesAsync();

            return Ok("Attendance locked");
        }

        [HttpPut("edit")]
        public async Task<IActionResult> EditAttendance(EditAttendanceRequest request)
        {
            var (dayStartUtc, dayEndUtc) = ReportingDayRange(request.Date);

            var subject = await _context.Subjects
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == request.SubjectId &&
                    x.TenantId == _currentUser.TenantId);

            if (subject == null)
                return BadRequest("Invalid subject.");

            var recordsQuery = _context.Attendances
                .Where(x =>
                    x.SubjectId == request.SubjectId &&
                    x.Date >= dayStartUtc &&
                    x.Date < dayEndUtc &&
                    x.TenantId == _currentUser.TenantId);

            recordsQuery = ApplyLanguageSubjectFilterForAttendance(recordsQuery, subject);

            var records = await recordsQuery.ToListAsync();

            if (!records.Any())
                return NotFound("Attendance not found");

            //  LOCK CHECK
            if (records.Any(x => x.IsLocked))
            {
                //  Allow Admin override
                if (!_currentUser.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("Attendance is locked");
                }
            }

            var map = request.Students.ToDictionary(x => x.StudentNumber);

            foreach (var record in records)
            {
                var student = await _context.Students
                    .FirstOrDefaultAsync(x => x.Id == record.StudentId);

                if (student == null) continue;

                if (!map.TryGetValue(student.StudentNumber, out var dto))
                    continue;

                record.Status = dto.Status;
                record.UpdatedDate = DateTime.UtcNow;
                record.UpdatedBy = _currentUser.UserId;
            }

            await _context.SaveChangesAsync();

            return Ok("Attendance updated");
        }

        private static IQueryable<Student> ApplyLanguageSubjectFilter(IQueryable<Student> query, Subject subject)
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

        private static bool StudentMatchesLanguageSubject(Subject subject, Student student)
        {
            if (subject.IsElective)
                return true;

            return subject.LanguageSubjectSlot switch
            {
                SubjectLanguageSlot.FirstLanguage when subject.TeachingLanguageId.HasValue =>
                    student.FirstLanguageId == subject.TeachingLanguageId.Value,
                SubjectLanguageSlot.SecondLanguage when subject.TeachingLanguageId.HasValue =>
                    student.LanguageId == subject.TeachingLanguageId.Value,
                _ => true
            };
        }

        private static IQueryable<Attendance> ApplyLanguageSubjectFilterForAttendance(
            IQueryable<Attendance> query,
            Subject subject)
        {
            if (subject.IsElective)
                return query;

            return subject.LanguageSubjectSlot switch
            {
                SubjectLanguageSlot.FirstLanguage when subject.TeachingLanguageId.HasValue =>
                    query.Where(a => a.Student.FirstLanguageId == subject.TeachingLanguageId.Value),
                SubjectLanguageSlot.SecondLanguage when subject.TeachingLanguageId.HasValue =>
                    query.Where(a => a.Student.LanguageId == subject.TeachingLanguageId.Value),
                _ => query
            };
        }
    }

}



