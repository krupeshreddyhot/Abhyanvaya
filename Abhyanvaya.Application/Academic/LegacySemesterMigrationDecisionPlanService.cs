using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3A —
/// Builds an explicit, read-only migration decision plan. No data mutation.
/// </summary>
public sealed class LegacySemesterMigrationDecisionPlanService : ILegacySemesterMigrationDecisionPlanService
{
    /// <summary>Prompt 2B local baseline used for revalidation notes (not a hard gate in other environments).</summary>
    internal static readonly (int SemesterId, int? GroupId, int Number, string Name, int ExpectedStudents)[] Prompt2BBaseline =
    [
        (1, null, 1, "Semester I", 0),
        (2, null, 2, "Semester II", 0),
        (3, null, 3, "Semester III", 296),
        (4, null, 4, "Semester VI", 0),
        (5, null, 4, "Semester V", 0),
        (9, 2, 4, "Semester IV", 4),
    ];

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public LegacySemesterMigrationDecisionPlanService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<LegacySemesterMigrationDecisionPlanDto> BuildDecisionPlanAsync(
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
                CourseExists = s.Course != null,
                CourseDeleted = s.Course != null && s.Course.IsDeleted,
                s.Number,
                s.Name,
                s.GroupId,
                GroupName = s.Group != null ? s.Group.Name : null,
            })
            .OrderBy(s => s.Id)
            .ToListAsync(cancellationToken);

        var groups = await _db.Groups.AsNoTracking()
            .Where(g => g.TenantId == tenantId && !g.IsDeleted)
            .Select(g => new { g.Id, g.CourseId, g.Name })
            .ToListAsync(cancellationToken);

        var groupsByCourse = groups
            .GroupBy(g => g.CourseId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<LegacySemesterMigrationDecisionPlanner.GroupInfo>)g
                    .Select(x => new LegacySemesterMigrationDecisionPlanner.GroupInfo(x.Id, x.Name))
                    .OrderBy(x => x.Name)
                    .ToList());

        var duplicateLegacyKeys = semesters
            .Where(s => s.GroupId == null)
            .GroupBy(s => new { s.CourseId, s.Number })
            .Where(g => g.Count() > 1)
            .Select(g => (g.Key.CourseId, g.Key.Number))
            .ToHashSet();

        var semesterIds = semesters.Select(s => s.Id).ToList();

        var studentRows = await _db.Students.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && semesterIds.Contains(s.SemesterId))
            .GroupBy(s => new { s.SemesterId, s.GroupId })
            .Select(g => new { g.Key.SemesterId, g.Key.GroupId, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var attendanceRows = await _db.AttendanceSessions.AsNoTracking()
            .Where(a => a.TenantId == tenantId && semesterIds.Contains(a.SemesterId))
            .GroupBy(a => new { a.SemesterId, a.GroupId })
            .Select(g => new { g.Key.SemesterId, g.Key.GroupId, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var subjectRows = await _db.Subjects.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && semesterIds.Contains(s.SemesterId))
            .GroupBy(s => new { s.SemesterId, s.GroupId })
            .Select(g => new { g.Key.SemesterId, g.Key.GroupId, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var sectionRows = await _db.Sections.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && semesterIds.Contains(s.SemesterId))
            .GroupBy(s => new { s.SemesterId, s.GroupId })
            .Select(g => new { g.Key.SemesterId, g.Key.GroupId, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var allocationRows = await _db.SchedulingSubjectAllocations.AsNoTracking()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && semesterIds.Contains(a.SemesterId))
            .GroupBy(a => new { a.SemesterId, a.GroupId })
            .Select(g => new { g.Key.SemesterId, g.Key.GroupId, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var entryRows = await _db.SchedulingTimetableEntries.AsNoTracking()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted && semesterIds.Contains(e.SemesterId))
            .GroupBy(e => new { e.SemesterId, e.GroupId })
            .Select(g => new { g.Key.SemesterId, g.Key.GroupId, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var tgRows = await _db.SchedulingTeachingGroups.AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && semesterIds.Contains(t.SemesterId))
            .GroupBy(t => new { t.SemesterId, t.GroupId })
            .Select(g => new { g.Key.SemesterId, g.Key.GroupId, Count = g.Count() })
            .ToListAsync(cancellationToken);

        static Dictionary<int, Dictionary<int, int>> Index(
            IEnumerable<(int SemesterId, int GroupId, int Count)> rows)
            => rows.GroupBy(r => r.SemesterId)
                .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.GroupId, x => x.Count));

        var students = Index(studentRows.Select(r => (r.SemesterId, r.GroupId, r.Count)));
        var attendance = Index(attendanceRows.Select(r => (r.SemesterId, r.GroupId, r.Count)));
        var subjects = Index(subjectRows.Select(r => (r.SemesterId, r.GroupId, r.Count)));
        var sections = Index(sectionRows.Select(r => (r.SemesterId, r.GroupId, r.Count)));
        var allocations = Index(allocationRows.Select(r => (r.SemesterId, r.GroupId, r.Count)));
        var entries = Index(entryRows.Select(r => (r.SemesterId, r.GroupId, r.Count)));
        var teachingGroups = Index(tgRows.Select(r => (r.SemesterId, r.GroupId, r.Count)));

        static IReadOnlyDictionary<int, int> ForSem(Dictionary<int, Dictionary<int, int>> map, int semId)
            => map.TryGetValue(semId, out var d) ? d : new Dictionary<int, int>();

        var decisions = new List<LegacySemesterMigrationDecisionRowDto>();
        foreach (var s in semesters)
        {
            groupsByCourse.TryGetValue(s.CourseId, out var courseGroups);
            courseGroups ??= Array.Empty<LegacySemesterMigrationDecisionPlanner.GroupInfo>();

            var input = new LegacySemesterMigrationDecisionPlanner.Input(
                s.Id,
                s.CourseId,
                s.CourseName,
                s.CourseExists,
                s.CourseDeleted,
                s.Number,
                s.Name,
                s.GroupId,
                s.GroupName,
                courseGroups,
                new LegacySemesterMigrationDecisionPlanner.DownstreamCounts(
                    ForSem(students, s.Id),
                    ForSem(attendance, s.Id),
                    ForSem(subjects, s.Id),
                    ForSem(sections, s.Id),
                    ForSem(allocations, s.Id),
                    ForSem(entries, s.Id),
                    ForSem(teachingGroups, s.Id)),
                s.GroupId == null && duplicateLegacyKeys.Contains((s.CourseId, s.Number)));

            decisions.Add(LegacySemesterMigrationDecisionPlanner.Plan(input));
        }

        var revalidationNotes = BuildRevalidationNotes(decisions, students);
        var matches = revalidationNotes.All(n => n.StartsWith("OK:", StringComparison.Ordinal));

        var proposedCreates = new List<string>();
        var proposedUpdates = new List<string>();
        var approvals = new List<string>();
        var blockers = new List<string>();
        var groupNameById = groups.ToDictionary(g => g.Id, g => g.Name);

        foreach (var d in decisions.Where(x => x.Decision == LegacySemesterMigrationDecision.Split))
        {
            foreach (var gId in d.TargetGroupIds)
            {
                var gName = groupNameById.GetValueOrDefault(gId, $"Group {gId}");
                proposedCreates.Add(
                    $"CREATE Semester CourseId={d.CourseId} GroupId={gId} ({gName}) Number={d.Number} Name='{d.Name}' from legacy SemesterId={d.SemesterId}");
            }

            proposedUpdates.Add(
                $"REMAP Students with SemesterId={d.SemesterId} by Student.GroupId → new Group-specific SemesterIds (Prompt 3B+)");
            approvals.Add($"Approve SPLIT for SemesterId={d.SemesterId} ({d.Name}) into Groups [{string.Join(", ", d.TargetGroupIds)}]");
            blockers.Add($"Execution blocked until Architect approves SPLIT for SemesterId={d.SemesterId}");
        }

        foreach (var d in decisions.Where(x => x.Decision == LegacySemesterMigrationDecision.DuplicateReview))
        {
            approvals.Add($"Resolve duplicate Number={d.Number} for SemesterId={d.SemesterId} ({d.Name}) before any remap");
            blockers.Add($"DUPLICATE_REVIEW blocker: SemesterId={d.SemesterId}");
        }

        foreach (var d in decisions.Where(x => x.Decision == LegacySemesterMigrationDecision.RetainLegacyPendingDecision))
        {
            approvals.Add($"Decide RETAIN vs SPLIT for SemesterId={d.SemesterId} ({d.Name}) — no students spanning Groups yet");
            blockers.Add($"Pending decision: SemesterId={d.SemesterId}");
        }

        return new LegacySemesterMigrationDecisionPlanDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = tenantId,
            IsReadOnly = true,
            MatchesPrompt2BBaseline = matches,
            RevalidationNotes = revalidationNotes,
            Decisions = decisions,
            DuplicateReviewRows = decisions.Where(d => d.Decision == LegacySemesterMigrationDecision.DuplicateReview).ToList(),
            RecordsMustNotModify = decisions.Where(d => d.MustNotModify).Select(d => d.SemesterId).ToList(),
            ProposedCreates = proposedCreates,
            ProposedUpdates = proposedUpdates,
            ManualApprovalsRequired = approvals,
            MigrationBlockers = blockers,
        };
    }

    private static List<string> BuildRevalidationNotes(
        List<LegacySemesterMigrationDecisionRowDto> decisions,
        Dictionary<int, Dictionary<int, int>> students)
    {
        var notes = new List<string>();
        foreach (var (semesterId, groupId, number, name, expectedStudents) in Prompt2BBaseline)
        {
            var row = decisions.FirstOrDefault(d => d.SemesterId == semesterId);
            if (row is null)
            {
                notes.Add($"DIFF: Prompt 2B SemesterId={semesterId} missing from current audit.");
                continue;
            }

            if (row.CurrentGroupId != groupId)
                notes.Add($"DIFF: SemesterId={semesterId} GroupId expected {groupId?.ToString() ?? "NULL"} got {row.CurrentGroupId?.ToString() ?? "NULL"}.");
            else if (row.Number != number)
                notes.Add($"DIFF: SemesterId={semesterId} Number expected {number} got {row.Number}.");
            else if (!string.Equals(row.Name, name, StringComparison.Ordinal))
                notes.Add($"DIFF: SemesterId={semesterId} Name expected '{name}' got '{row.Name}'.");
            else
            {
                var actualStudents = students.TryGetValue(semesterId, out var byG) ? byG.Values.Sum() : 0;
                if (actualStudents != expectedStudents)
                    notes.Add($"DIFF: SemesterId={semesterId} students expected {expectedStudents} got {actualStudents}.");
                else
                    notes.Add($"OK: SemesterId={semesterId} matches Prompt 2B baseline ({name}, GroupId={(groupId?.ToString() ?? "NULL")}, students={expectedStudents}).");
            }
        }

        var sem3 = decisions.FirstOrDefault(d => d.SemesterId == 3);
        if (sem3 is not null)
        {
            var finance = sem3.StudentCountsByTargetGroup.GetValueOrDefault(1);
            var ca = sem3.StudentCountsByTargetGroup.GetValueOrDefault(2);
            if (finance == 60 && ca == 236)
                notes.Add("OK: Semester III student split Finance=60 / CA=236.");
            else
                notes.Add($"DIFF: Semester III student split expected Finance=60 CA=236 got Finance={finance} CA={ca}.");
        }

        return notes;
    }
}
