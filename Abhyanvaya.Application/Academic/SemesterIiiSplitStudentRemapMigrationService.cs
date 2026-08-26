using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3B —
/// Fail-closed, transactional, idempotent Semester III split + Student remap.
/// Does not mutate Attendance/Subject/Section/SA/Timetable/TeachingGroup.
/// </summary>
public sealed class SemesterIiiSplitStudentRemapMigrationService : ISemesterIiiSplitStudentRemapMigrationService
{
    public const int ExpectedSemesterNumber = 3;
    public const int ExpectedFinanceStudents = 60;
    public const int ExpectedCaStudents = 236;
    public const int ExpectedTotalStudents = 296;
    public const string ExpectedSemesterName = "Semester III";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILegacySemesterMigrationDecisionPlanService _decisionPlan;
    private readonly ILogger<SemesterIiiSplitStudentRemapMigrationService> _logger;

    public SemesterIiiSplitStudentRemapMigrationService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ILegacySemesterMigrationDecisionPlanService decisionPlan,
        ILogger<SemesterIiiSplitStudentRemapMigrationService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _decisionPlan = decisionPlan;
        _logger = logger;
    }

    public async Task<SemesterIiiSplitMigrationResultDto> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        SemesterIiiSplitMigrationResultDto? result = null;

        try
        {
            await _db.ExecuteInTransactionAsync(async ct =>
            {
                result = await ExecuteCoreAsync(tenantId, ct);
                if (!string.Equals(result.Status, "Completed", StringComparison.Ordinal)
                    && !string.Equals(result.Status, "AlreadyCompleted", StringComparison.Ordinal))
                {
                    throw new DomainException(result.AbortReason ?? "Semester III split migration aborted.");
                }
            }, cancellationToken);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "P1-4 Prompt 3B Semester III split aborted and rolled back.");
            if (result is not null)
            {
                return new SemesterIiiSplitMigrationResultDto
                {
                    Status = "Aborted",
                    RolledBack = true,
                    AbortReason = ex.Message,
                    SourceSemesterId = result.SourceSemesterId,
                    FinanceGroupId = result.FinanceGroupId,
                    CaGroupId = result.CaGroupId,
                    FinanceTargetSemesterId = result.FinanceTargetSemesterId,
                    CaTargetSemesterId = result.CaTargetSemesterId,
                    FinanceSemesterCreated = result.FinanceSemesterCreated,
                    CaSemesterCreated = result.CaSemesterCreated,
                    FinanceSemesterReused = result.FinanceSemesterReused,
                    CaSemesterReused = result.CaSemesterReused,
                    FinanceStudentsRemapped = result.FinanceStudentsRemapped,
                    CaStudentsRemapped = result.CaStudentsRemapped,
                    TotalStudentsRemapped = result.TotalStudentsRemapped,
                    UnresolvedStudents = result.UnresolvedStudents,
                    DownstreamAttendanceReferences = result.DownstreamAttendanceReferences,
                    DownstreamSubjectReferences = result.DownstreamSubjectReferences,
                    DownstreamSectionReferences = result.DownstreamSectionReferences,
                    DownstreamSubjectAllocationReferences = result.DownstreamSubjectAllocationReferences,
                    DownstreamTimetableEntryReferences = result.DownstreamTimetableEntryReferences,
                    DownstreamTeachingGroupReferences = result.DownstreamTeachingGroupReferences,
                    Notes = result.Notes,
                };
            }

            return new SemesterIiiSplitMigrationResultDto
            {
                Status = "Aborted",
                RolledBack = true,
                AbortReason = ex.Message,
            };
        }

        return result ?? new SemesterIiiSplitMigrationResultDto
        {
            Status = "Aborted",
            RolledBack = true,
            AbortReason = "Migration produced no result.",
        };
    }

    private async Task<SemesterIiiSplitMigrationResultDto> ExecuteCoreAsync(int tenantId, CancellationToken ct)
    {
        var notes = new List<string>();

        // --- Idempotent short-circuit before baseline gate ---
        // After a successful split, Prompt 3A baseline intentionally drifts (legacy Sem III has 0 students;
        // Group-specific Semesters exist). Prefer AlreadyCompleted over Abort.
        var alreadyCompleted = await TryAlreadyCompletedAsync(tenantId, ct, notes);
        if (alreadyCompleted is not null)
            return alreadyCompleted;

        // --- Pre-flight: Prompt 3A decision ---
        var plan = await _decisionPlan.BuildDecisionPlanAsync(ct);
        if (!plan.MatchesPrompt2BBaseline)
        {
            return Abort("Prompt 3A baseline revalidation failed; aborting Prompt 3B.", notes);
        }

        var splitDecision = plan.Decisions.SingleOrDefault(d =>
            d.Decision == LegacySemesterMigrationDecision.Split
            && d.Number == ExpectedSemesterNumber
            && d.CurrentGroupId is null);

        if (splitDecision is null)
        {
            return Abort("No approved SPLIT decision for legacy Semester Number=3; aborting.", notes);
        }

        if (splitDecision.SemesterId != 3 && splitDecision.StudentCountsByTargetGroup.Values.Sum() != ExpectedTotalStudents)
        {
            // Prefer Id=3 when present; otherwise require exact total baseline.
            notes.Add($"SPLIT source SemesterId={splitDecision.SemesterId} (expected local Id=3).");
        }

        var sourceId = splitDecision.SemesterId;
        var source = await _db.Semesters
            .FirstOrDefaultAsync(s => s.Id == sourceId && s.TenantId == tenantId && !s.IsDeleted, ct)
            ?? throw new DomainException($"Source Semester {sourceId} not found.");

        if (source.GroupId is not null || source.Number != ExpectedSemesterNumber)
        {
            return Abort("Source Semester is not the approved legacy NULL-group Semester III.", notes);
        }

        // Protect other semesters
        await EnsureProtectedSemestersUnchangedAsync(tenantId, ct);

        var groupCounts = splitDecision.StudentCountsByTargetGroup;
        if (groupCounts.Count != 2)
            return Abort($"Expected exactly 2 target Groups; found {groupCounts.Count}.", notes);

        // Resolve Finance=60 and CA=236 by count (authoritative Student.GroupId distribution).
        var financeEntry = groupCounts.SingleOrDefault(kv => kv.Value == ExpectedFinanceStudents);
        var caEntry = groupCounts.SingleOrDefault(kv => kv.Value == ExpectedCaStudents);
        if (financeEntry.Key <= 0 || caEntry.Key <= 0 || financeEntry.Value + caEntry.Value != ExpectedTotalStudents)
        {
            return Abort(
                $"Student baseline mismatch. Expected Finance={ExpectedFinanceStudents}, CA={ExpectedCaStudents}, total={ExpectedTotalStudents}.",
                notes);
        }

        var financeGroupId = financeEntry.Key;
        var caGroupId = caEntry.Key;

        var financeGroup = await LoadGroupAsync(tenantId, financeGroupId, ct);
        var caGroup = await LoadGroupAsync(tenantId, caGroupId, ct);
        ValidateGroup(tenantId, source.CourseId, financeGroup, "Finance");
        ValidateGroup(tenantId, source.CourseId, caGroup, "CA");

        // Precondition: load affected students and validate counts/mapping before any mutation.
        var students = await _db.Students
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.SemesterId == sourceId)
            .ToListAsync(ct);

        if (students.Count != ExpectedTotalStudents)
        {
            return Abort($"Affected student count {students.Count} != expected {ExpectedTotalStudents}.", notes);
        }

        var financeStudents = students.Where(s => s.GroupId == financeGroupId).ToList();
        var caStudents = students.Where(s => s.GroupId == caGroupId).ToList();
        if (financeStudents.Count != ExpectedFinanceStudents || caStudents.Count != ExpectedCaStudents)
        {
            return Abort(
                $"Student Group distribution mismatch: Finance={financeStudents.Count}, CA={caStudents.Count}.",
                notes);
        }

        foreach (var student in students)
        {
            if (student.GroupId <= 0)
                throw new DomainException($"Student Id={student.Id} has invalid GroupId.");

            if (student.CourseId != source.CourseId)
                throw new DomainException($"Student Id={student.Id} CourseId does not match source Semester Course.");

            if (student.GroupId != financeGroupId && student.GroupId != caGroupId)
            {
                throw new DomainException(
                    $"Student Id={student.Id} GroupId={student.GroupId} is not an approved target Group.");
            }
        }

        // Downstream snapshot (informational only)
        var downstream = await SnapshotDownstreamAsync(tenantId, sourceId, ct);
        notes.Add($"Downstream refs to legacy Sem {sourceId}: Att={downstream.Att}, Subj={downstream.Subj}, Sec={downstream.Sec}, SA={downstream.Sa}, TT={downstream.Tt}, TG={downstream.Tg}.");

        // Create/reuse targets
        var (financeSem, financeCreated, financeReused) =
            await ResolveOrCreateTargetAsync(tenantId, source, financeGroup, ct);
        var (caSem, caCreated, caReused) =
            await ResolveOrCreateTargetAsync(tenantId, source, caGroup, ct);

        await _db.SaveChangesAsync(ct);

        // Remap students — authoritative key: Student.GroupId
        var financeRemapped = 0;
        var caRemapped = 0;
        foreach (var student in financeStudents)
        {
            if (student.CourseId != financeSem.CourseId || financeSem.GroupId != financeGroupId)
                throw new DomainException("Finance target Semester ownership invalid.");
            student.SemesterId = financeSem.Id;
            student.UpdatedDate = DateTime.UtcNow;
            financeRemapped++;
        }

        foreach (var student in caStudents)
        {
            if (student.CourseId != caSem.CourseId || caSem.GroupId != caGroupId)
                throw new DomainException("CA target Semester ownership invalid.");
            student.SemesterId = caSem.Id;
            student.UpdatedDate = DateTime.UtcNow;
            caRemapped++;
        }

        if (financeRemapped != ExpectedFinanceStudents || caRemapped != ExpectedCaStudents)
        {
            throw new DomainException(
                $"Remap counts mismatch: Finance={financeRemapped}, CA={caRemapped}.");
        }

        await _db.SaveChangesAsync(ct);

        // Post-verification (before commit)
        await VerifyPostMigrationAsync(tenantId, sourceId, financeSem, caSem, financeGroupId, caGroupId, ct);
        await EnsureProtectedSemestersUnchangedAsync(tenantId, ct);

        // Legacy source must remain NULL-group, same Number
        var legacy = await _db.Semesters.AsNoTracking()
            .FirstAsync(s => s.Id == sourceId, ct);
        if (legacy.GroupId is not null || legacy.Number != ExpectedSemesterNumber)
            throw new DomainException("Legacy Semester III was unexpectedly modified.");

        notes.Add("Post-migration verification passed.");

        return new SemesterIiiSplitMigrationResultDto
        {
            Status = "Completed",
            RolledBack = false,
            SourceSemesterId = sourceId,
            FinanceGroupId = financeGroupId,
            CaGroupId = caGroupId,
            FinanceTargetSemesterId = financeSem.Id,
            CaTargetSemesterId = caSem.Id,
            FinanceSemesterCreated = financeCreated,
            CaSemesterCreated = caCreated,
            FinanceSemesterReused = financeReused,
            CaSemesterReused = caReused,
            FinanceStudentsRemapped = financeRemapped,
            CaStudentsRemapped = caRemapped,
            TotalStudentsRemapped = financeRemapped + caRemapped,
            UnresolvedStudents = 0,
            DownstreamAttendanceReferences = downstream.Att,
            DownstreamSubjectReferences = downstream.Subj,
            DownstreamSectionReferences = downstream.Sec,
            DownstreamSubjectAllocationReferences = downstream.Sa,
            DownstreamTimetableEntryReferences = downstream.Tt,
            DownstreamTeachingGroupReferences = downstream.Tg,
            Notes = notes,
        };
    }

    private async Task<SemesterIiiSplitMigrationResultDto?> TryAlreadyCompletedAsync(
        int tenantId, CancellationToken ct, List<string> notes)
    {
        var legacy = await _db.Semesters.AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.TenantId == tenantId && !s.IsDeleted && s.Number == ExpectedSemesterNumber && s.GroupId == null,
                ct);
        if (legacy is null)
            return null;

        var remaining = await _db.Students.CountAsync(
            s => s.TenantId == tenantId && !s.IsDeleted && s.SemesterId == legacy.Id, ct);
        if (remaining != 0)
            return null;

        var targets = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.Number == ExpectedSemesterNumber && s.GroupId != null && s.CourseId == legacy.CourseId)
            .ToListAsync(ct);
        if (targets.Count != 2)
            return null;

        var counts = new List<(int SemId, int GroupId, int Count)>();
        foreach (var t in targets)
        {
            var c = await _db.Students.CountAsync(
                s => s.TenantId == tenantId && !s.IsDeleted && s.SemesterId == t.Id, ct);
            counts.Add((t.Id, t.GroupId!.Value, c));
        }

        var finance = counts.SingleOrDefault(x => x.Count == ExpectedFinanceStudents);
        var ca = counts.SingleOrDefault(x => x.Count == ExpectedCaStudents);
        if (finance.SemId == 0 || ca.SemId == 0)
            return null;

        notes.Add("Idempotent: migration already completed; no students remain on legacy Semester III.");
        return new SemesterIiiSplitMigrationResultDto
        {
            Status = "AlreadyCompleted",
            RolledBack = false,
            SourceSemesterId = legacy.Id,
            FinanceGroupId = finance.GroupId,
            CaGroupId = ca.GroupId,
            FinanceTargetSemesterId = finance.SemId,
            CaTargetSemesterId = ca.SemId,
            FinanceSemesterReused = true,
            CaSemesterReused = true,
            FinanceStudentsRemapped = 0,
            CaStudentsRemapped = 0,
            TotalStudentsRemapped = 0,
            UnresolvedStudents = 0,
            Notes = notes,
        };
    }

    private async Task<(Semester Entity, bool Created, bool Reused)> ResolveOrCreateTargetAsync(
        int tenantId,
        Semester source,
        Group group,
        CancellationToken ct)
    {
        var ownership = SemesterGroupOwnershipRules.EvaluateWrite(
            tenantId,
            group.Id,
            source.CourseId,
            new SemesterGroupOwnershipRules.GroupSnapshot(group.Id, group.TenantId, group.CourseId, group.IsDeleted));
        if (!ownership.Accepted)
            throw new DomainException(ownership.Error ?? "Invalid Group for target Semester.");

        var matches = await _db.Semesters
            .Where(s =>
                s.TenantId == tenantId
                && !s.IsDeleted
                && s.GroupId == group.Id
                && s.Number == ExpectedSemesterNumber)
            .ToListAsync(ct);

        if (matches.Count > 1)
            throw new DomainException($"Multiple Group-specific Semester Number={ExpectedSemesterNumber} for GroupId={group.Id}.");

        if (matches.Count == 1)
        {
            var existing = matches[0];
            if (existing.CourseId != ownership.AlignedCourseId)
                throw new DomainException($"Existing target Semester Id={existing.Id} has conflicting CourseId.");
            if (existing.GroupId != group.Id)
                throw new DomainException($"Existing target Semester Id={existing.Id} has conflicting GroupId.");
            return (existing, false, true);
        }

        var created = new Semester
        {
            TenantId = tenantId,
            Number = ExpectedSemesterNumber,
            Name = string.IsNullOrWhiteSpace(source.Name) ? ExpectedSemesterName : source.Name,
            CourseId = ownership.AlignedCourseId,
            GroupId = ownership.AlignedGroupId,
            DisplayOrder = source.DisplayOrder,
            CreatedDate = DateTime.UtcNow,
        };
        await _db.AddAsync(created);
        return (created, true, false);
    }

    private async Task VerifyPostMigrationAsync(
        int tenantId,
        int sourceId,
        Semester financeSem,
        Semester caSem,
        int financeGroupId,
        int caGroupId,
        CancellationToken ct)
    {
        var remainingOnLegacy = await _db.Students.CountAsync(
            s => s.TenantId == tenantId && !s.IsDeleted && s.SemesterId == sourceId, ct);
        if (remainingOnLegacy != 0)
            throw new DomainException($"{remainingOnLegacy} students still reference legacy Semester III.");

        var financeStudents = await _db.Students
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.SemesterId == financeSem.Id)
            .ToListAsync(ct);
        var caStudents = await _db.Students
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.SemesterId == caSem.Id)
            .ToListAsync(ct);

        if (financeStudents.Count != ExpectedFinanceStudents || caStudents.Count != ExpectedCaStudents)
            throw new DomainException("Post-migration student counts do not match approved baseline.");

        foreach (var s in financeStudents)
        {
            if (s.GroupId != financeGroupId || s.CourseId != financeSem.CourseId || financeSem.GroupId is null || financeSem.Number != ExpectedSemesterNumber)
                throw new DomainException($"Finance student Id={s.Id} failed ownership verification.");
        }

        foreach (var s in caStudents)
        {
            if (s.GroupId != caGroupId || s.CourseId != caSem.CourseId || caSem.GroupId is null || caSem.Number != ExpectedSemesterNumber)
                throw new DomainException($"CA student Id={s.Id} failed ownership verification.");
        }
    }

    private async Task EnsureProtectedSemestersUnchangedAsync(int tenantId, CancellationToken ct)
    {
        // Snapshot checks for Id 1,2,4,5,9 — verify they still exist with expected GroupId nullability.
        var protectedIds = new[] { 1, 2, 4, 5, 9 };
        var rows = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == tenantId && protectedIds.Contains(s.Id))
            .Select(s => new { s.Id, s.GroupId, s.Number })
            .ToListAsync(ct);

        var s9 = rows.FirstOrDefault(r => r.Id == 9);
        if (s9 is null || s9.GroupId is null)
            throw new DomainException("Protected Semester 9 must remain Group-specific.");

        foreach (var id in new[] { 1, 2, 4, 5 })
        {
            var row = rows.FirstOrDefault(r => r.Id == id);
            if (row is null)
                throw new DomainException($"Protected Semester {id} is missing.");
            if (row.GroupId is not null)
                throw new DomainException($"Protected Semester {id} must remain legacy NULL-group for this prompt.");
        }
    }

    private async Task<Group> LoadGroupAsync(int tenantId, int groupId, CancellationToken ct)
        => await _db.Groups.FirstOrDefaultAsync(g => g.Id == groupId && g.TenantId == tenantId && !g.IsDeleted, ct)
           ?? throw new DomainException($"Group {groupId} not found for tenant.");

    private static void ValidateGroup(int tenantId, int courseId, Group group, string label)
    {
        var decision = SemesterGroupOwnershipRules.EvaluateWrite(
            tenantId,
            group.Id,
            courseId,
            new SemesterGroupOwnershipRules.GroupSnapshot(group.Id, group.TenantId, group.CourseId, group.IsDeleted));
        if (!decision.Accepted)
            throw new DomainException($"{label} Group invalid: {decision.Error}");
        if (group.CourseId != courseId)
            throw new DomainException($"{label} Group belongs to a different Course.");
    }

    private async Task<(int Att, int Subj, int Sec, int Sa, int Tt, int Tg)> SnapshotDownstreamAsync(
        int tenantId, int sourceId, CancellationToken ct)
    {
        var att = await _db.AttendanceSessions.CountAsync(a => a.TenantId == tenantId && a.SemesterId == sourceId, ct);
        var subj = await _db.Subjects.CountAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.SemesterId == sourceId, ct);
        var sec = await _db.Sections.CountAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.SemesterId == sourceId, ct);
        var sa = await _db.SchedulingSubjectAllocations.CountAsync(a => a.TenantId == tenantId && !a.IsDeleted && a.SemesterId == sourceId, ct);
        var tt = await _db.SchedulingTimetableEntries.CountAsync(e => e.TenantId == tenantId && !e.IsDeleted && e.SemesterId == sourceId, ct);
        var tg = await _db.SchedulingTeachingGroups.CountAsync(t => t.TenantId == tenantId && !t.IsDeleted && t.SemesterId == sourceId, ct);
        return (att, subj, sec, sa, tt, tg);
    }

    private static SemesterIiiSplitMigrationResultDto Abort(string reason, List<string> notes)
        => new()
        {
            Status = "Aborted",
            RolledBack = false, // will be set true by outer catch after rollback
            AbortReason = reason,
            Notes = notes,
        };
}
