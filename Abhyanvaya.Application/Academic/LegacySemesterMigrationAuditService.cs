using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 2B —
/// Read-only audit of legacy NULL-group Semesters and downstream FK impact.
/// </summary>
public sealed class LegacySemesterMigrationAuditService : ILegacySemesterMigrationAuditService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public LegacySemesterMigrationAuditService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<LegacySemesterMigrationAuditReportDto> BuildAuditAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;

        var semesters = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Select(s => new
            {
                s.Id,
                s.CourseId,
                CourseName = s.Course != null ? s.Course.Name : "",
                CourseDeleted = s.Course != null && s.Course.IsDeleted,
                CourseExists = s.Course != null,
                s.Number,
                s.Name,
                s.GroupId,
                GroupName = s.Group != null ? s.Group.Name : null,
            })
            .OrderBy(s => s.CourseId)
            .ThenBy(s => s.Number)
            .ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);

        var groups = await _db.Groups.AsNoTracking()
            .Where(g => g.TenantId == tenantId && !g.IsDeleted)
            .Select(g => new { g.Id, g.CourseId, g.Code, g.Name })
            .ToListAsync(cancellationToken);

        var groupsByCourse = groups
            .GroupBy(g => g.CourseId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<LegacySemesterMigrationClassifier.ActiveGroupInfo>)g
                    .Select(x => new LegacySemesterMigrationClassifier.ActiveGroupInfo(x.Id, x.Code, x.Name))
                    .ToList());

        // Duplicate legacy (NULL GroupId) Number keys per Course
        var duplicateLegacyKeys = semesters
            .Where(s => s.GroupId == null)
            .GroupBy(s => new { s.CourseId, s.Number })
            .Where(g => g.Count() > 1)
            .Select(g => (g.Key.CourseId, g.Key.Number))
            .ToHashSet();

        var semesterIds = semesters.Select(s => s.Id).ToList();

        var studentRefs = await _db.Students.AsNoTracking()
            .Where(st => st.TenantId == tenantId && !st.IsDeleted && semesterIds.Contains(st.SemesterId))
            .GroupBy(st => new { st.SemesterId, st.GroupId })
            .Select(g => new { g.Key.SemesterId, g.Key.GroupId, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var attendanceRefs = await _db.AttendanceSessions.AsNoTracking()
            .Where(a => a.TenantId == tenantId && semesterIds.Contains(a.SemesterId))
            .GroupBy(a => a.SemesterId)
            .Select(g => new { SemesterId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SemesterId, x => x.Count, cancellationToken);

        var saRefs = await _db.SchedulingSubjectAllocations.AsNoTracking()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && semesterIds.Contains(a.SemesterId))
            .GroupBy(a => a.SemesterId)
            .Select(g => new { SemesterId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SemesterId, x => x.Count, cancellationToken);

        var ttRefs = await _db.SchedulingTimetableEntries.AsNoTracking()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted && semesterIds.Contains(e.SemesterId))
            .GroupBy(e => e.SemesterId)
            .Select(g => new { SemesterId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SemesterId, x => x.Count, cancellationToken);

        var subjectRefs = await _db.Subjects.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && semesterIds.Contains(s.SemesterId))
            .GroupBy(s => s.SemesterId)
            .Select(g => new { SemesterId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SemesterId, x => x.Count, cancellationToken);

        var sectionRefs = await _db.Sections.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && semesterIds.Contains(s.SemesterId))
            .GroupBy(s => s.SemesterId)
            .Select(g => new { SemesterId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SemesterId, x => x.Count, cancellationToken);

        var tgRefs = await _db.SchedulingTeachingGroups.AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && semesterIds.Contains(t.SemesterId))
            .GroupBy(t => t.SemesterId)
            .Select(g => new { SemesterId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SemesterId, x => x.Count, cancellationToken);

        var studentBySemGroup = studentRefs
            .GroupBy(x => x.SemesterId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<int, int>)g.ToDictionary(x => x.GroupId, x => x.Count));

        var studentTotalBySem = studentRefs
            .GroupBy(x => x.SemesterId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Count));

        var rows = new List<LegacySemesterMigrationRowDto>();
        foreach (var s in semesters)
        {
            groupsByCourse.TryGetValue(s.CourseId, out var courseGroups);
            courseGroups ??= Array.Empty<LegacySemesterMigrationClassifier.ActiveGroupInfo>();

            studentBySemGroup.TryGetValue(s.Id, out var byGroup);
            byGroup ??= new Dictionary<int, int>();

            var input = new LegacySemesterMigrationClassifier.Input(
                SemesterId: s.Id,
                CourseId: s.CourseId,
                CourseName: s.CourseName,
                CourseExists: s.CourseExists,
                CourseDeleted: s.CourseDeleted,
                Number: s.Number,
                Name: s.Name,
                CurrentGroupId: s.GroupId,
                CurrentGroupName: s.GroupName,
                ActiveGroupsOnCourse: courseGroups,
                StudentReferenceCount: studentTotalBySem.GetValueOrDefault(s.Id),
                StudentCountByGroupId: byGroup,
                AttendanceReferenceCount: attendanceRefs.GetValueOrDefault(s.Id),
                SubjectAllocationReferenceCount: saRefs.GetValueOrDefault(s.Id),
                TimetableEntryReferenceCount: ttRefs.GetValueOrDefault(s.Id),
                SubjectReferenceCount: subjectRefs.GetValueOrDefault(s.Id),
                SectionReferenceCount: sectionRefs.GetValueOrDefault(s.Id),
                TeachingGroupReferenceCount: tgRefs.GetValueOrDefault(s.Id),
                HasDuplicateLegacyNumberOnCourse: s.GroupId == null
                    && duplicateLegacyKeys.Contains((s.CourseId, s.Number)));

            rows.Add(LegacySemesterMigrationClassifier.Classify(input));
        }

        var summary = new LegacySemesterMigrationAuditSummaryDto
        {
            TotalSemesters = rows.Count,
            LegacyNullGroupCount = rows.Count(r => r.CurrentGroupId is null),
            GroupSpecificCount = rows.Count(r => r.CurrentGroupId is not null),
            MapSingleGroupCount = rows.Count(r => r.MigrationAction == LegacySemesterMigrationAction.MapSingleGroup),
            SplitRequiredCount = rows.Count(r => r.MigrationAction == LegacySemesterMigrationAction.SplitRequired),
            ManualMappingRequiredCount = rows.Count(r => r.MigrationAction == LegacySemesterMigrationAction.ManualMappingRequired),
            OrphanReviewRequiredCount = rows.Count(r => r.MigrationAction == LegacySemesterMigrationAction.OrphanReviewRequired),
            InvalidDataReviewCount = rows.Count(r => r.MigrationAction == LegacySemesterMigrationAction.InvalidDataReview),
            AlreadyGroupSpecificCount = rows.Count(r => r.MigrationAction == LegacySemesterMigrationAction.AlreadyGroupSpecific),
            DuplicateLegacyNumberCourseKeys = duplicateLegacyKeys.Count,
            HasMigrationBlockers = rows.Any(r =>
                r.MigrationAction is LegacySemesterMigrationAction.ManualMappingRequired
                    or LegacySemesterMigrationAction.SplitRequired
                    or LegacySemesterMigrationAction.OrphanReviewRequired
                    or LegacySemesterMigrationAction.InvalidDataReview),
        };

        return new LegacySemesterMigrationAuditReportDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = tenantId,
            IsReadOnly = true,
            Summary = summary,
            Rows = rows,
        };
    }
}
