using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3B-A —
/// Read-only post-migration integrity audit. Zero writes. Fail-closed classification only.
/// </summary>
public sealed class SemesterPostMigrationIntegrityAuditService : ISemesterPostMigrationIntegrityAuditService
{
    public const int Prompt3BExpectedSemesterNumber = 3;
    public const int Prompt3BExpectedFinanceStudents = 60;
    public const int Prompt3BExpectedCaStudents = 236;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILegacySemesterMigrationDecisionPlanService _decisionPlan;

    public SemesterPostMigrationIntegrityAuditService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ILegacySemesterMigrationDecisionPlanService decisionPlan)
    {
        _db = db;
        _currentUser = currentUser;
        _decisionPlan = decisionPlan;
    }

    public async Task<SemesterPostMigrationIntegrityAuditDto> BuildAuditAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var violations = new List<SemesterPostMigrationIntegrityViolationDto>();
        var notes = new List<string> { "Audit is read-only; no mutations performed." };

        var semesters = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Select(s => new SemesterRow(
                s.Id,
                s.TenantId,
                s.CourseId,
                s.Number,
                s.Name,
                s.GroupId,
                s.Course != null ? s.Course.TenantId : (int?)null,
                s.Course != null && !s.Course.IsDeleted,
                s.Group != null ? s.Group.TenantId : (int?)null,
                s.Group != null ? s.Group.CourseId : (int?)null,
                s.Group != null && !s.Group.IsDeleted))
            .ToListAsync(cancellationToken);

        var semesterById = semesters.ToDictionary(s => s.Id);

        var groups = await _db.Groups.AsNoTracking()
            .Where(g => g.TenantId == tenantId && !g.IsDeleted)
            .Select(g => new { g.Id, g.CourseId, g.TenantId })
            .ToListAsync(cancellationToken);
        var groupById = groups.ToDictionary(g => g.Id);

        var courses = await _db.Courses.AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted)
            .Select(c => new { c.Id, c.DepartmentId, c.TenantId })
            .ToListAsync(cancellationToken);
        var courseById = courses.ToDictionary(c => c.Id);

        // --- Semester checks ---
        foreach (var s in semesters)
        {
            if (s.GroupId is null)
            {
                // Classified later via decision plan; still emit legacy warning code.
                continue;
            }

            if (!s.GroupExists || s.GroupTenantId is null)
            {
                violations.Add(V("SEMESTER_GROUP_MISSING", IntegritySeverity.Critical, s.Id, "Semester",
                    $"Semester {s.Id} GroupId={s.GroupId} does not resolve to an active Group.", s.Id, s.GroupId, s.CourseId));
                continue;
            }

            if (s.GroupTenantId != s.TenantId)
            {
                violations.Add(V("CROSS_TENANT_SEMESTER_GROUP", IntegritySeverity.Critical, s.Id, "Semester",
                    $"Semester {s.Id} TenantId does not match Group TenantId.", s.Id, s.GroupId, s.CourseId));
            }

            if (s.CourseTenantId is int ct && ct != s.TenantId)
            {
                violations.Add(V("CROSS_TENANT_SEMESTER_COURSE", IntegritySeverity.Critical, s.Id, "Semester",
                    $"Semester {s.Id} TenantId does not match Course TenantId.", s.Id, s.GroupId, s.CourseId));
            }

            if (s.GroupCourseId is int gc && gc != s.CourseId)
            {
                violations.Add(V("SEMESTER_COURSE_GROUP_MISMATCH", IntegritySeverity.Critical, s.Id, "Semester",
                    $"Semester {s.Id} CourseId={s.CourseId} != Group.CourseId={gc}.", s.Id, s.GroupId, s.CourseId));
            }
        }

        // Duplicate Tenant+Group+Number (group-specific only)
        var duplicateGroups = semesters
            .Where(s => s.GroupId is not null)
            .GroupBy(s => new { s.TenantId, GroupId = s.GroupId!.Value, s.Number })
            .Where(g => g.Count() > 1);
        foreach (var g in duplicateGroups)
        {
            foreach (var s in g)
            {
                violations.Add(V("DUPLICATE_GROUP_SEMESTER_NUMBER", IntegritySeverity.Error, s.Id, "Semester",
                    $"Duplicate Semester Number={s.Number} for GroupId={s.GroupId}.", s.Id, s.GroupId, s.CourseId));
            }
        }

        // --- Students ---
        var students = await _db.Students.AsNoTracking()
            .Where(st => st.TenantId == tenantId && !st.IsDeleted)
            .Select(st => new { st.Id, st.TenantId, st.CourseId, st.GroupId, st.SemesterId })
            .ToListAsync(cancellationToken);

        foreach (var st in students)
        {
            if (!groupById.TryGetValue(st.GroupId, out var g))
            {
                violations.Add(V("STUDENT_GROUP_MISSING", IntegritySeverity.Critical, st.Id, "Student",
                    $"Student {st.Id} GroupId={st.GroupId} missing.", null, st.GroupId, st.CourseId));
            }
            else if (g.CourseId != st.CourseId)
            {
                violations.Add(V("STUDENT_GROUP_COURSE_MISMATCH", IntegritySeverity.Error, st.Id, "Student",
                    $"Student {st.Id} Group.CourseId={g.CourseId} != Student.CourseId={st.CourseId}.", null, st.GroupId, st.CourseId));
            }

            if (st.SemesterId <= 0)
                continue;

            if (!semesterById.TryGetValue(st.SemesterId, out var sem))
            {
                violations.Add(V("STUDENT_SEMESTER_MISSING", IntegritySeverity.Critical, st.Id, "Student",
                    $"Student {st.Id} SemesterId={st.SemesterId} missing.", st.SemesterId, st.GroupId, st.CourseId));
                continue;
            }

            if (sem.TenantId != st.TenantId)
            {
                violations.Add(V("CROSS_TENANT_STUDENT_SEMESTER", IntegritySeverity.Critical, st.Id, "Student",
                    $"Student {st.Id} Semester tenant mismatch.", st.SemesterId, st.GroupId, st.CourseId));
            }

            if (sem.GroupId is null || sem.GroupId.Value != st.GroupId)
            {
                violations.Add(V("STUDENT_SEMESTER_GROUP_MISMATCH", IntegritySeverity.Critical, st.Id, "Student",
                    $"Student {st.Id} Semester.GroupId={(sem.GroupId?.ToString() ?? "NULL")} != Student.GroupId={st.GroupId}.",
                    st.SemesterId, st.GroupId, st.CourseId));
            }

            if (sem.CourseId != st.CourseId)
            {
                violations.Add(V("STUDENT_SEMESTER_COURSE_MISMATCH", IntegritySeverity.Error, st.Id, "Student",
                    $"Student {st.Id} Semester.CourseId={sem.CourseId} != Student.CourseId={st.CourseId}.",
                    st.SemesterId, st.GroupId, st.CourseId));
            }
        }

        // --- Attendance ---
        var attendance = await _db.AttendanceSessions.AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .Select(a => new { a.Id, a.SemesterId, a.GroupId, a.CourseId })
            .ToListAsync(cancellationToken);

        foreach (var a in attendance)
        {
            if (!semesterById.TryGetValue(a.SemesterId, out var sem))
            {
                violations.Add(V("ATTENDANCE_SEMESTER_REFERENCE_MISMATCH", IntegritySeverity.Error, a.Id, "AttendanceSession",
                    $"AttendanceSession {a.Id} references missing SemesterId={a.SemesterId}.", a.SemesterId, a.GroupId, a.CourseId));
                continue;
            }

            if (sem.GroupId is null)
            {
                violations.Add(V("ATTENDANCE_SEMESTER_REFERENCE_MISMATCH", IntegritySeverity.Warning, a.Id, "AttendanceSession",
                    $"AttendanceSession {a.Id} still references legacy NULL-group Semester {a.SemesterId} (deferred remapping).",
                    a.SemesterId, a.GroupId, a.CourseId));
            }
            else if (sem.GroupId.Value != a.GroupId)
            {
                violations.Add(V("ATTENDANCE_SEMESTER_REFERENCE_MISMATCH", IntegritySeverity.Error, a.Id, "AttendanceSession",
                    $"AttendanceSession {a.Id} GroupId={a.GroupId} != Semester.GroupId={sem.GroupId}.",
                    a.SemesterId, a.GroupId, a.CourseId));
            }
        }

        // --- SubjectAllocation ---
        var allocations = await _db.SchedulingSubjectAllocations.AsNoTracking()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted)
            .Select(a => new { a.Id, a.CourseId, a.DepartmentId, a.GroupId, a.SemesterId })
            .ToListAsync(cancellationToken);

        foreach (var a in allocations)
        {
            if (!courseById.TryGetValue(a.CourseId, out var course))
            {
                violations.Add(V("SA_COURSE_MISSING", IntegritySeverity.Error, a.Id, "SubjectAllocation",
                    $"SubjectAllocation {a.Id} CourseId={a.CourseId} missing.", a.SemesterId, a.GroupId, a.CourseId));
            }
            else if (a.DepartmentId != course.DepartmentId)
            {
                violations.Add(V("SA_DEPARTMENT_MISMATCH", IntegritySeverity.Error, a.Id, "SubjectAllocation",
                    $"SubjectAllocation {a.Id} DepartmentId={a.DepartmentId} != Course.DepartmentId={course.DepartmentId}.",
                    a.SemesterId, a.GroupId, a.CourseId));
            }

            if (semesterById.TryGetValue(a.SemesterId, out var sem) && sem.GroupId is not null && sem.GroupId.Value != a.GroupId)
            {
                violations.Add(V("SA_SEMESTER_GROUP_MISMATCH", IntegritySeverity.Error, a.Id, "SubjectAllocation",
                    $"SubjectAllocation {a.Id} GroupId={a.GroupId} != Semester.GroupId={sem.GroupId}.",
                    a.SemesterId, a.GroupId, a.CourseId));
            }
            else if (semesterById.TryGetValue(a.SemesterId, out var legacySem) && legacySem.GroupId is null)
            {
                violations.Add(V("SA_SEMESTER_GROUP_MISMATCH", IntegritySeverity.Warning, a.Id, "SubjectAllocation",
                    $"SubjectAllocation {a.Id} still references legacy NULL-group Semester {a.SemesterId}.",
                    a.SemesterId, a.GroupId, a.CourseId));
            }
        }

        // --- TimetableEntry ---
        var entries = await _db.SchedulingTimetableEntries.AsNoTracking()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted)
            .Select(e => new { e.Id, e.CourseId, e.DepartmentId, e.GroupId, e.SemesterId, e.TeachingGroupId })
            .ToListAsync(cancellationToken);

        foreach (var e in entries)
        {
            if (!courseById.TryGetValue(e.CourseId, out var course))
            {
                violations.Add(V("TT_COURSE_MISSING", IntegritySeverity.Error, e.Id, "TimetableEntry",
                    $"TimetableEntry {e.Id} CourseId={e.CourseId} missing.", e.SemesterId, e.GroupId, e.CourseId));
            }
            else if (e.DepartmentId != course.DepartmentId)
            {
                violations.Add(V("TT_DEPARTMENT_MISMATCH", IntegritySeverity.Error, e.Id, "TimetableEntry",
                    $"TimetableEntry {e.Id} DepartmentId={e.DepartmentId} != Course.DepartmentId={course.DepartmentId}.",
                    e.SemesterId, e.GroupId, e.CourseId));
            }

            if (semesterById.TryGetValue(e.SemesterId, out var sem) && sem.GroupId is not null && sem.GroupId.Value != e.GroupId)
            {
                violations.Add(V("TT_SEMESTER_GROUP_MISMATCH", IntegritySeverity.Error, e.Id, "TimetableEntry",
                    $"TimetableEntry {e.Id} GroupId={e.GroupId} != Semester.GroupId={sem.GroupId}.",
                    e.SemesterId, e.GroupId, e.CourseId));
            }
            else if (semesterById.TryGetValue(e.SemesterId, out var legacySem) && legacySem.GroupId is null)
            {
                violations.Add(V("TT_SEMESTER_GROUP_MISMATCH", IntegritySeverity.Warning, e.Id, "TimetableEntry",
                    $"TimetableEntry {e.Id} still references legacy NULL-group Semester {e.SemesterId}.",
                    e.SemesterId, e.GroupId, e.CourseId));
            }
        }

        // --- Teaching Groups (audit only) ---
        var teachingGroups = await _db.SchedulingTeachingGroups.AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted)
            .Select(t => new { t.Id, t.CourseId, t.GroupId, t.SemesterId })
            .ToListAsync(cancellationToken);

        foreach (var t in teachingGroups)
        {
            if (!semesterById.TryGetValue(t.SemesterId, out var sem))
            {
                violations.Add(V("TEACHING_GROUP_REFERENCE_IMPACT", IntegritySeverity.Critical, t.Id, "TeachingGroup",
                    $"TeachingGroup {t.Id} references missing SemesterId={t.SemesterId}.", t.SemesterId, t.GroupId, t.CourseId));
                continue;
            }

            if (sem.GroupId is null)
            {
                violations.Add(V("TEACHING_GROUP_REFERENCE_IMPACT", IntegritySeverity.Warning, t.Id, "TeachingGroup",
                    $"TeachingGroup {t.Id} still references legacy NULL-group Semester {t.SemesterId} (identify-only; TG architecture frozen).",
                    t.SemesterId, t.GroupId, t.CourseId));
            }
            else if (sem.GroupId.Value != t.GroupId)
            {
                violations.Add(V("TEACHING_GROUP_REFERENCE_IMPACT", IntegritySeverity.Critical, t.Id, "TeachingGroup",
                    $"TeachingGroup {t.Id} GroupId={t.GroupId} != Semester.GroupId={sem.GroupId}.",
                    t.SemesterId, t.GroupId, t.CourseId));
            }
        }

        // --- Legacy classification via Prompt 3A planner ---
        var plan = await _decisionPlan.BuildDecisionPlanAsync(cancellationToken);
        var legacySemesters = new List<LegacySemesterStatusDto>();
        foreach (var d in plan.Decisions.Where(x => x.CurrentGroupId is null))
        {
            var downstream = d.DownstreamClassifications.Sum(x => x.ReferenceCount);
            var studentsOn = d.StudentCountsByTargetGroup.Values.Sum();
            legacySemesters.Add(new LegacySemesterStatusDto
            {
                SemesterId = d.SemesterId,
                CourseId = d.CourseId,
                Number = d.Number,
                Name = d.Name,
                Classification = d.DecisionCode,
                StudentCount = studentsOn,
                DownstreamReferenceTotal = downstream,
            });

            violations.Add(V("LEGACY_COURSE_WIDE_SEMESTER", IntegritySeverity.Warning, d.SemesterId, "Semester",
                $"Legacy NULL-group Semester {d.SemesterId} classified as {d.DecisionCode}.",
                d.SemesterId, null, d.CourseId));
        }

        // --- Semester III split verification ---
        var legacyIii = semesters.Where(s => s.Number == Prompt3BExpectedSemesterNumber && s.GroupId is null).ToList();
        var groupIii = semesters.Where(s => s.Number == Prompt3BExpectedSemesterNumber && s.GroupId is not null).ToList();

        int? financeSemId = null;
        int? caSemId = null;
        var financeCount = 0;
        var caCount = 0;

        foreach (var s in groupIii)
        {
            var c = students.Count(st => st.SemesterId == s.Id);
            if (c == Prompt3BExpectedFinanceStudents)
            {
                financeSemId = s.Id;
                financeCount = c;
            }
            else if (c == Prompt3BExpectedCaStudents)
            {
                caSemId = s.Id;
                caCount = c;
            }
        }

        // Prefer ownership match when counts alone are ambiguous
        if (financeSemId is null || caSemId is null)
        {
            foreach (var s in groupIii)
            {
                var alignedFinance = students.Count(st => st.SemesterId == s.Id && st.GroupId == s.GroupId);
                if (alignedFinance == Prompt3BExpectedFinanceStudents && financeSemId is null)
                {
                    financeSemId = s.Id;
                    financeCount = alignedFinance;
                }
                else if (alignedFinance == Prompt3BExpectedCaStudents && caSemId is null)
                {
                    caSemId = s.Id;
                    caCount = alignedFinance;
                }
            }
        }

        var studentsOnLegacyIii = legacyIii.Sum(s => students.Count(st => st.SemesterId == s.Id));
        var split = new SemesterIiiSplitVerificationDto
        {
            FinanceSemesterIiiExists = financeSemId is not null,
            CaSemesterIiiExists = caSemId is not null,
            FinanceSemesterId = financeSemId,
            CaSemesterId = caSemId,
            LegacySemesterIiiId = legacyIii.FirstOrDefault()?.Id,
            StudentsOnLegacySemesterIii = studentsOnLegacyIii,
            FinanceStudentsOnTarget = financeCount,
            CaStudentsOnTarget = caCount,
            MigratedStudentsFullyRemapped = studentsOnLegacyIii == 0
                                           && financeCount == Prompt3BExpectedFinanceStudents
                                           && caCount == Prompt3BExpectedCaStudents,
        };

        if (!split.MigratedStudentsFullyRemapped)
        {
            violations.Add(V("SEMESTER_III_SPLIT_INCOMPLETE", IntegritySeverity.Error, split.LegacySemesterIiiId, "Semester",
                $"Semester III split verification incomplete: legacyStudents={studentsOnLegacyIii}, finance={financeCount}, ca={caCount}.",
                split.LegacySemesterIiiId, null, null));
        }

        var checks = BuildChecks(violations, split);
        var summary = new SemesterPostMigrationIntegritySummaryDto
        {
            Critical = violations.Count(v => v.Severity == IntegritySeverity.Critical),
            Errors = violations.Count(v => v.Severity == IntegritySeverity.Error),
            Warnings = violations.Count(v => v.Severity == IntegritySeverity.Warning),
        };

        // Healthy = no Critical and no Error (warnings allowed for deferred downstream)
        var isHealthy = summary.Critical == 0 && summary.Errors == 0;

        notes.Add($"Checks={checks.Count}; Violations={violations.Count}; Healthy={isHealthy}.");

        return new SemesterPostMigrationIntegrityAuditDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = tenantId,
            IsReadOnly = true,
            IsHealthy = isHealthy,
            Summary = summary,
            Checks = checks,
            LegacySemesters = legacySemesters,
            Violations = violations
                .OrderBy(v => v.Severity)
                .ThenBy(v => v.Code)
                .ThenBy(v => v.EntityId)
                .ToList(),
            SemesterIiiSplit = split,
            Notes = notes,
        };
    }

    private static IReadOnlyList<SemesterPostMigrationIntegrityCheckDto> BuildChecks(
        List<SemesterPostMigrationIntegrityViolationDto> violations,
        SemesterIiiSplitVerificationDto split)
    {
        string ResultFor(params string[] codes)
        {
            var subset = violations.Where(v => codes.Contains(v.Code)).ToList();
            if (subset.Any(v => v.Severity == IntegritySeverity.Critical || v.Severity == IntegritySeverity.Error))
                return "FAIL";
            if (subset.Count > 0)
                return "WARN";
            return "PASS";
        }

        int Count(params string[] codes) => violations.Count(v => codes.Contains(v.Code));

        return
        [
            new() { Code = "SEMESTER_GROUP", Name = "Semester → Group", Result = ResultFor("SEMESTER_GROUP_MISSING", "CROSS_TENANT_SEMESTER_GROUP"), ViolationCount = Count("SEMESTER_GROUP_MISSING", "CROSS_TENANT_SEMESTER_GROUP") },
            new() { Code = "SEMESTER_COURSE", Name = "Semester → Course", Result = ResultFor("SEMESTER_COURSE_GROUP_MISMATCH", "CROSS_TENANT_SEMESTER_COURSE"), ViolationCount = Count("SEMESTER_COURSE_GROUP_MISMATCH", "CROSS_TENANT_SEMESTER_COURSE") },
            new() { Code = "GROUP_COURSE", Name = "Group → Course (via Student)", Result = ResultFor("STUDENT_GROUP_COURSE_MISMATCH", "STUDENT_GROUP_MISSING"), ViolationCount = Count("STUDENT_GROUP_COURSE_MISMATCH", "STUDENT_GROUP_MISSING") },
            new() { Code = "STUDENT_GROUP", Name = "Student → Group", Result = ResultFor("STUDENT_GROUP_MISSING", "STUDENT_GROUP_COURSE_MISMATCH"), ViolationCount = Count("STUDENT_GROUP_MISSING", "STUDENT_GROUP_COURSE_MISMATCH") },
            new() { Code = "STUDENT_SEMESTER", Name = "Student → Semester", Result = ResultFor("STUDENT_SEMESTER_GROUP_MISMATCH", "STUDENT_SEMESTER_MISSING", "CROSS_TENANT_STUDENT_SEMESTER"), ViolationCount = Count("STUDENT_SEMESTER_GROUP_MISMATCH", "STUDENT_SEMESTER_MISSING", "CROSS_TENANT_STUDENT_SEMESTER") },
            new() { Code = "STUDENT_COURSE", Name = "Student → Course (via Semester)", Result = ResultFor("STUDENT_SEMESTER_COURSE_MISMATCH"), ViolationCount = Count("STUDENT_SEMESTER_COURSE_MISMATCH") },
            new() { Code = "ATTENDANCE_SEMESTER", Name = "Attendance → Semester", Result = ResultFor("ATTENDANCE_SEMESTER_REFERENCE_MISMATCH"), ViolationCount = Count("ATTENDANCE_SEMESTER_REFERENCE_MISMATCH") },
            new() { Code = "SA_DEPARTMENT", Name = "SA → Course/Department", Result = ResultFor("SA_DEPARTMENT_MISMATCH", "SA_COURSE_MISSING", "SA_SEMESTER_GROUP_MISMATCH"), ViolationCount = Count("SA_DEPARTMENT_MISMATCH", "SA_COURSE_MISSING", "SA_SEMESTER_GROUP_MISMATCH") },
            new() { Code = "TT_DEPARTMENT", Name = "Timetable → Course/Department", Result = ResultFor("TT_DEPARTMENT_MISMATCH", "TT_COURSE_MISSING", "TT_SEMESTER_GROUP_MISMATCH"), ViolationCount = Count("TT_DEPARTMENT_MISMATCH", "TT_COURSE_MISSING", "TT_SEMESTER_GROUP_MISMATCH") },
            new() { Code = "TEACHING_GROUP", Name = "Teaching Group boundary", Result = ResultFor("TEACHING_GROUP_REFERENCE_IMPACT"), ViolationCount = Count("TEACHING_GROUP_REFERENCE_IMPACT") },
            new() { Code = "TENANT", Name = "Tenant isolation", Result = ResultFor("CROSS_TENANT_SEMESTER_GROUP", "CROSS_TENANT_SEMESTER_COURSE", "CROSS_TENANT_STUDENT_SEMESTER"), ViolationCount = Count("CROSS_TENANT_SEMESTER_GROUP", "CROSS_TENANT_SEMESTER_COURSE", "CROSS_TENANT_STUDENT_SEMESTER") },
            new() { Code = "DUPLICATE_NUMBER", Name = "Duplicate Semester numbers", Result = ResultFor("DUPLICATE_GROUP_SEMESTER_NUMBER"), ViolationCount = Count("DUPLICATE_GROUP_SEMESTER_NUMBER") },
            new() { Code = "LEGACY", Name = "Legacy Semester classification", Result = ResultFor("LEGACY_COURSE_WIDE_SEMESTER"), ViolationCount = Count("LEGACY_COURSE_WIDE_SEMESTER") },
            new() { Code = "SEMESTER_III_SPLIT", Name = "Semester III split verification", Result = split.MigratedStudentsFullyRemapped ? "PASS" : "FAIL", ViolationCount = Count("SEMESTER_III_SPLIT_INCOMPLETE") },
        ];
    }

    private static SemesterPostMigrationIntegrityViolationDto V(
        string code,
        IntegritySeverity severity,
        object? entityId,
        string entityType,
        string message,
        int? semesterId,
        int? groupId,
        int? courseId)
        => new()
        {
            Code = code,
            Severity = severity,
            SeverityCode = severity.ToString().ToUpperInvariant(),
            Message = message,
            EntityId = entityId?.ToString(),
            EntityType = entityType,
            SemesterId = semesterId,
            GroupId = groupId,
            CourseId = courseId,
        };

    private sealed record SemesterRow(
        int Id,
        int TenantId,
        int CourseId,
        int Number,
        string Name,
        int? GroupId,
        int? CourseTenantId,
        bool CourseExists,
        int? GroupTenantId,
        int? GroupCourseId,
        bool GroupExists);
}
