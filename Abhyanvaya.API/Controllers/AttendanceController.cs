using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs;
using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Events;
using Abhyanvaya.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
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
        private readonly ITenantContextService _tenantContextService;
        private readonly ILogger<AttendanceController> _logger;
        private readonly IAttendanceCalendar _attendanceCalendar;
        private readonly IDomainEventDispatcher _domainEventDispatcher;

        public AttendanceController(
            IApplicationDbContext context,
            ICurrentUserService currentUser,
            ITenantContextService tenantContextService,
            ILogger<AttendanceController> logger,
            IAttendanceCalendar attendanceCalendar,
            IDomainEventDispatcher domainEventDispatcher)
        {
            _context = context;
            _currentUser = currentUser;
            _tenantContextService = tenantContextService;
            _logger = logger;
            _attendanceCalendar = attendanceCalendar;
            _domainEventDispatcher = domainEventDispatcher;
        }

        /// <summary>UTC half-open range [start, end) view of an <see cref="AttendanceDay"/> for LINQ predicates.</summary>
        private static (DateTime StartUtcInclusive, DateTime EndUtcExclusive) ToRange(AttendanceDay day) =>
            (day.UtcStart, day.UtcEnd);

        [HttpPost("mark")]
        public async Task<IActionResult> MarkAttendance(MarkAttendanceRequest request)
        {
            if (this.RequireTenantContext(_tenantContextService, out var resolution) is { } contextError)
            {
                return contextError;
            }

            if (request?.Students == null || !request.Students.Any())
                return BadRequest("Students list is required");

            var attendanceDay = _attendanceCalendar.GetAttendanceDay(request.Date);
            var (dayStartUtc, dayEndUtc) = ToRange(attendanceDay);
            var today = _attendanceCalendar.Today();
            if (attendanceDay.LocalDate > today.LocalDate)
                return BadRequest("Cannot mark future attendance");

            var subject = await _context.Subjects
                .FirstOrDefaultAsync(x => x.Id == request.SubjectId);

            if (subject == null)
                return BadRequest("Invalid subject");

            if (!await FacultySubjectAccess.FacultyMayAccessSubjectAsync(
                    _context,
                    _currentUser,
                    subject.Id,
                    HttpContext.RequestAborted)
                .ConfigureAwait(false))
                return Forbid();

            // AI29.1D.15A Prompt 3 — optional section scope (subject C/G/S is authoritative; UI not trusted).
            var (scopeSectionIds, scopeError) = await AttendanceSaveScope.ValidateWriteSectionScopeAsync(
                    _context,
                    _currentUser.TenantId,
                    subject.CourseId,
                    subject.GroupId,
                    subject.SemesterId,
                    request.SectionId,
                    request.SectionIds,
                    _logger,
                    HttpContext.RequestAborted)
                .ConfigureAwait(false);
            if (scopeError != null)
                return BadRequest(scopeError);

            var firstNumber = request.Students.FirstOrDefault()?.StudentNumber;
            if (string.IsNullOrEmpty(firstNumber))
                return BadRequest("Students list is required");

            var student = await _context.Students
                .FirstOrDefaultAsync(x => x.StudentNumber == firstNumber);

            if (student == null)
                return BadRequest("Invalid student");

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
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.Ordinal)
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

            List<Student> students;
            if (scopeSectionIds.Count > 0)
            {
                // AI29.1D.15A Prompt 4 — every submitted student must have current StudentSection
                // in the validated section scope (A, or A OR B for combined). UI list is untrusted.
                var (authorizedStudents, studentScopeError) =
                    await AttendanceSaveScope.ValidateEverySubmittedStudentInSectionScopeAsync(
                            _context,
                            _currentUser.TenantId,
                            subject.CourseId,
                            subject.GroupId,
                            subject.SemesterId,
                            scopeSectionIds,
                            studentNumbers,
                            requireCourseGroupSemesterMatch: !subject.IsElective,
                            HttpContext.RequestAborted)
                        .ConfigureAwait(false);
                if (studentScopeError != null)
                    return BadRequest(studentScopeError);

                var languageMatched = authorizedStudents
                    .Where(s => StudentMatchesLanguageSubject(subject, s))
                    .ToList();
                var languageError = AttendanceSaveScope.EnsureAllSubmittedStudentsAuthorized(
                    studentNumbers,
                    languageMatched.Select(s => s.StudentNumber));
                if (languageError != null)
                    return BadRequest(languageError);

                if (subject.IsElective)
                {
                    var authorizedIds = languageMatched.Select(s => s.Id).ToList();
                    var mappedIds = await _context.StudentSubjects.AsNoTracking()
                        .Where(x => x.SubjectId == subject.Id && authorizedIds.Contains(x.StudentId))
                        .Select(x => x.StudentId)
                        .Distinct()
                        .ToListAsync();
                    if (mappedIds.Count != authorizedIds.Count)
                        return BadRequest(AttendanceSaveScope.UnauthorizedStudentsMessage);
                }

                students = languageMatched;
            }
            else
            {
                var query = _context.Students
                    .Where(x =>
                        studentNumbers.Contains(x.StudentNumber) &&
                        x.TenantId == _currentUser.TenantId);

                if (_currentUser.Role.Equals("Faculty", StringComparison.OrdinalIgnoreCase)
                    && _currentUser.StaffId <= 0)
                {
                    query = query.Where(x =>
                        x.CourseId == _currentUser.CourseId &&
                        x.GroupId == _currentUser.GroupId);
                }

                query = ApplyLanguageSubjectFilter(query, subject);
                students = await query.ToListAsync();
            }

            var map = request.Students
                .GroupBy(x => x.StudentNumber, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);

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

            List<Attendance> attendanceList;
            if (scopeSectionIds.Count > 0)
            {
                // Atomic section-scoped write: all submitted students or none. Never silently drop.
                var (planned, planError) = AttendanceSaveScope.BuildAtomicMarkRows(
                    studentNumbers,
                    students,
                    stu =>
                    {
                        if (!map.TryGetValue(stu.StudentNumber, out var dto))
                            return null;
                        if (existingSet.Contains(stu.Id))
                            return null;
                        return new Attendance
                        {
                            StudentId = stu.Id,
                            SubjectId = request.SubjectId,
                            Date = dayStartUtc,
                            Status = dto.Status,
                            TenantId = _currentUser.TenantId
                        };
                    });
                if (planError != null)
                    return BadRequest(planError);

                attendanceList = planned.ToList();
            }
            else
            {
                // Legacy no-Section path — preserve existing Course/Group/Semester cohort behavior.
                attendanceList = new List<Attendance>();
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
            }

            await _context.ExecuteInTransactionAsync(
                    async ct =>
                    {
                        _context.AddAttendances(attendanceList);
                        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
                    },
                    HttpContext.RequestAborted)
                .ConfigureAwait(false);

            if (attendanceList.Count > 0)
            {
                await _domainEventDispatcher.DispatchAsync(
                    [
                        new AttendanceMarkedEvent(
                            _currentUser.TenantId,
                            request.SubjectId,
                            attendanceDay,
                            attendanceList.Count,
                            AttendanceMethod.Manual,
                            _currentUser.UserId > 0 ? _currentUser.UserId : null)
                    ],
                    CancellationToken.None);
            }

            return Ok(new
            {
                Message = "Attendance saved successfully",
                Count = attendanceList.Count
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetAttendance(int subjectId, DateTime date)
        {
            var (dayStartUtc, dayEndUtc) = ToRange(_attendanceCalendar.GetAttendanceDay(date));

            var subject = await _context.Subjects
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == subjectId && x.TenantId == _currentUser.TenantId);

            if (subject == null)
                return BadRequest("Invalid subject.");

            if (!await FacultySubjectAccess.FacultyMayAccessSubjectAsync(
                    _context,
                    _currentUser,
                    subject.Id,
                    HttpContext.RequestAborted)
                .ConfigureAwait(false))
                return Forbid();

            var query = _context.Attendances
                .Where(x => x.TenantId == _currentUser.TenantId &&
                            x.SubjectId == subjectId &&
                            x.Date >= dayStartUtc &&
                            x.Date < dayEndUtc);

            query = ApplyLanguageSubjectFilterForAttendance(query, subject);

            // 🔐 Legacy Faculty (no staff link): cohort restriction
            if (_currentUser.Role.Equals("Faculty", StringComparison.OrdinalIgnoreCase)
                && _currentUser.StaffId <= 0)
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
            int pageSize = 50,
            // AI29 optional — when omitted, all students load (legacy behavior).
            int? sectionId = null,
            // AI29 optional — combined section ids; when empty, ignored.
            [FromQuery] int[]? sectionIds = null)
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

            if (!await FacultySubjectAccess.FacultyMayAccessSubjectAsync(
                    _context,
                    _currentUser,
                    subject.Id,
                    HttpContext.RequestAborted)
                .ConfigureAwait(false))
                return Forbid();

            var query = _context.Students
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == _currentUser.TenantId &&
                    x.CourseId == courseId &&
                    x.GroupId == groupId &&
                    x.SemesterId == semesterId);

            if (_currentUser.Role.Equals("Faculty", StringComparison.OrdinalIgnoreCase)
                && _currentUser.StaffId <= 0)
            {
                query = query.Where(x =>
                    x.CourseId == _currentUser.CourseId &&
                    x.GroupId == _currentUser.GroupId);
            }

            // AI29.1D Prompt 11A/11B/13 — optional section filter (omit = legacy full cohort; AY not required).
            // When section ids are supplied: require exactly one current Academic Year + Tenant/AY/C/G/S match.
            // Combined classes use TimetableSections/SectionGroup ids from the existing session contract.
            var requestedSectionIds = AttendanceSectionScope.NormalizeRequestedIds(sectionId, sectionIds);
            IReadOnlyList<int> participatingSectionIds = Array.Empty<int>();
            if (requestedSectionIds.Count > 0)
            {
                var (scopeSectionIds, scopeError) = await AttendanceSectionScope.ValidateSectionIdsAsync(
                        _context,
                        _currentUser.TenantId,
                        courseId,
                        groupId,
                        semesterId,
                        requestedSectionIds,
                        _logger,
                        HttpContext.RequestAborted)
                    .ConfigureAwait(false);
                if (scopeError != null)
                    return BadRequest(scopeError);

                participatingSectionIds = scopeSectionIds;
                query = AttendanceSectionScope.ApplyStudentSectionFilter(
                    query,
                    _context,
                    _currentUser.TenantId,
                    scopeSectionIds);
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

            var (dayStartUtc, dayEndUtc) = ToRange(_attendanceCalendar.GetAttendanceDay(date));
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

            // Prompt 13 — retain underlying Section identity for reporting (additive; does not change mark/edit).
            var pageStudentIds = students.Select(s => s.Id).ToList();
            var membershipRows = await (
                    from ss in _context.StudentSections.AsNoTracking()
                    join sec in _context.Sections.AsNoTracking() on ss.SectionId equals sec.Id
                    where ss.TenantId == tenantId
                          && ss.IsCurrent
                          && pageStudentIds.Contains(ss.StudentId)
                    orderby sec.DisplayOrder, sec.SectionCode
                    select new { ss.StudentId, SectionId = sec.Id, sec.SectionCode }
                ).ToListAsync();

            var membershipByStudent = membershipRows
                .GroupBy(m => m.StudentId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        if (participatingSectionIds.Count > 0)
                        {
                            var inScope = g.FirstOrDefault(m => participatingSectionIds.Contains(m.SectionId));
                            if (inScope != null)
                                return inScope;
                        }
                        return g.First();
                    });

            List<string> participatingSectionCodes;
            if (participatingSectionIds.Count == 0)
            {
                participatingSectionCodes = [];
            }
            else
            {
                participatingSectionCodes = await _context.Sections.AsNoTracking()
                    .Where(s =>
                        s.TenantId == tenantId
                        && participatingSectionIds.Contains(s.Id))
                    .OrderBy(s => s.DisplayOrder)
                    .ThenBy(s => s.SectionCode)
                    .Select(s => s.SectionCode)
                    .ToListAsync();
            }

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

            // Collapse to one status per student (Present wins) so legacy duplicate rows for the same
            // student/subject/day cannot throw a duplicate-key exception while building the dictionary.
            var existingByStudent = existing
                .GroupBy(x => x.StudentId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Any(x => x.Status == AttendanceStatus.Present)
                        ? AttendanceStatus.Present
                        : g.First().Status);
            var isLocked = existing.Any(x => x.IsLocked);

            var result = students
                .Select((x, index) =>
                {
                    membershipByStudent.TryGetValue(x.Id, out var membership);
                    return new
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
                        Status = existingByStudent.TryGetValue(x.Id, out var st) ? st : AttendanceStatus.Absent,
                        SectionId = membership?.SectionId,
                        SectionCode = membership?.SectionCode
                    };
                })
                .ToList();

            var isCombinedClass = participatingSectionIds.Count > 1;
            var operationalClassLabel = participatingSectionCodes.Count == 0
                ? null
                : string.Join(" + ", participatingSectionCodes);

            return Ok(new
            {
                IsLocked = isLocked,
                AlreadyMarked = existing.Any(),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                // Prompt 13 additive operational-class metadata (TimetableSections / multi-select).
                IsCombinedClass = isCombinedClass,
                ParticipatingSectionIds = participatingSectionIds,
                ParticipatingSectionCodes = participatingSectionCodes,
                OperationalClassLabel = operationalClassLabel,
                Students = result
            });
        }

        [HttpPost("lock")]
        public async Task<IActionResult> LockAttendance(int subjectId, DateTime date)
        {
            var attendanceDay = _attendanceCalendar.GetAttendanceDay(date);
            var (dayStartUtc, dayEndUtc) = ToRange(attendanceDay);

            var subject = await _context.Subjects
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == subjectId &&
                    x.TenantId == _currentUser.TenantId);

            if (subject == null)
                return BadRequest("Invalid subject.");

            if (!await FacultySubjectAccess.FacultyMayAccessSubjectAsync(
                    _context,
                    _currentUser,
                    subject.Id,
                    HttpContext.RequestAborted)
                .ConfigureAwait(false))
                return Forbid();

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

            await _domainEventDispatcher.DispatchAsync(
                [
                    new AttendanceLockedEvent(
                        _currentUser.TenantId,
                        subjectId,
                        attendanceDay,
                        records.Count,
                        _currentUser.UserId > 0 ? _currentUser.UserId : null)
                ],
                CancellationToken.None);

            return Ok("Attendance locked");
        }

        [HttpPut("edit")]
        public async Task<IActionResult> EditAttendance(EditAttendanceRequest request)
        {
            if (request?.Students == null || !request.Students.Any())
                return BadRequest("Students list is required");

            var (dayStartUtc, dayEndUtc) = ToRange(_attendanceCalendar.GetAttendanceDay(request.Date));

            var subject = await _context.Subjects
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == request.SubjectId &&
                    x.TenantId == _currentUser.TenantId);

            if (subject == null)
                return BadRequest("Invalid subject.");

            if (!await FacultySubjectAccess.FacultyMayAccessSubjectAsync(
                    _context,
                    _currentUser,
                    subject.Id,
                    HttpContext.RequestAborted)
                .ConfigureAwait(false))
                return Forbid();

            // AI29.1D.15A Prompt 3 — optional section scope (subject C/G/S is authoritative; UI not trusted).
            var (scopeSectionIds, scopeError) = await AttendanceSaveScope.ValidateWriteSectionScopeAsync(
                    _context,
                    _currentUser.TenantId,
                    subject.CourseId,
                    subject.GroupId,
                    subject.SemesterId,
                    request.SectionId,
                    request.SectionIds,
                    _logger,
                    HttpContext.RequestAborted)
                .ConfigureAwait(false);
            if (scopeError != null)
                return BadRequest(scopeError);

            if (scopeSectionIds.Count > 0)
            {
                // AI29.1D.15A Prompt 4 — reject entire edit before any mutation if any student is out of section scope.
                var studentNumbers = AttendanceSaveScope.NormalizeStudentNumbers(
                    request.Students.Select(x => x.StudentNumber));

                var (authorizedStudents, studentScopeError) =
                    await AttendanceSaveScope.ValidateEverySubmittedStudentInSectionScopeAsync(
                            _context,
                            _currentUser.TenantId,
                            subject.CourseId,
                            subject.GroupId,
                            subject.SemesterId,
                            scopeSectionIds,
                            studentNumbers,
                            requireCourseGroupSemesterMatch: !subject.IsElective,
                            HttpContext.RequestAborted)
                        .ConfigureAwait(false);
                if (studentScopeError != null)
                    return BadRequest(studentScopeError);

                var languageMatched = authorizedStudents
                    .Where(s => StudentMatchesLanguageSubject(subject, s))
                    .ToList();
                var languageError = AttendanceSaveScope.EnsureAllSubmittedStudentsAuthorized(
                    studentNumbers,
                    languageMatched.Select(s => s.StudentNumber));
                if (languageError != null)
                    return BadRequest(languageError);
            }

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

            var map = request.Students
                .GroupBy(x => x.StudentNumber, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);

            await _context.ExecuteInTransactionAsync(
                    async ct =>
                    {
                        foreach (var record in records)
                        {
                            var student = await _context.Students
                                .FirstOrDefaultAsync(x => x.Id == record.StudentId, ct);

                            if (student == null) continue;

                            if (!map.TryGetValue(student.StudentNumber, out var dto))
                                continue;

                            record.Status = dto.Status;
                            record.UpdatedDate = DateTime.UtcNow;
                            record.UpdatedBy = _currentUser.UserId;
                        }

                        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
                    },
                    HttpContext.RequestAborted)
                .ConfigureAwait(false);

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



