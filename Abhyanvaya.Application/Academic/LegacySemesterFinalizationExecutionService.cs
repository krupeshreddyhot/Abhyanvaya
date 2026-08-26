using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3E —
/// Fail-closed, transactional, idempotent legacy Semester disposition finalization.
/// Does not mutate Teaching Groups, attendance, students, allocations, or timetable entries.
/// Does not assign GroupId unless explicit Architect approval flag is set (default: never).
/// RETAIN_HISTORICAL journals only — Semester row unchanged.
/// </summary>
public sealed class LegacySemesterFinalizationExecutionService : ILegacySemesterFinalizationExecutionService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILegacySemesterFinalizationAuditService _finalizationAudit;
    private readonly ILegacySemesterMigrationDecisionPlanService _decisionPlan;
    private readonly ILogger<LegacySemesterFinalizationExecutionService> _logger;

    public LegacySemesterFinalizationExecutionService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ILegacySemesterFinalizationAuditService finalizationAudit,
        ILegacySemesterMigrationDecisionPlanService decisionPlan,
        ILogger<LegacySemesterFinalizationExecutionService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _finalizationAudit = finalizationAudit;
        _decisionPlan = decisionPlan;
        _logger = logger;
    }

    public Task<LegacySemesterFinalizationExecutionResultDto> PreviewAsync(CancellationToken cancellationToken = default)
        => BuildAsync(mutate: false, ct: cancellationToken);

    public async Task<LegacySemesterFinalizationExecutionResultDto> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        LegacySemesterFinalizationExecutionResultDto? result = null;
        try
        {
            await _db.ExecuteInTransactionAsync(async ct =>
            {
                result = await BuildAsync(mutate: true, ct: ct);
                if (!string.Equals(result.ExecutionStatus, "Completed", StringComparison.Ordinal)
                    && !string.Equals(result.ExecutionStatus, "AlreadyComplete", StringComparison.Ordinal))
                {
                    throw new DomainException(result.AbortReason ?? "Legacy finalization aborted.");
                }
            }, cancellationToken);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "P1-4 Prompt 3E legacy finalization aborted and rolled back.");
            return new LegacySemesterFinalizationExecutionResultDto
            {
                GeneratedUtc = DateTime.UtcNow,
                TenantId = _currentUser.TenantId,
                IsReadOnly = false,
                ExecutionStatus = "Aborted",
                RolledBack = true,
                AbortReason = ex.Message,
                ChangedCount = result?.ChangedCount ?? 0,
                AlreadyCompleteCount = result?.AlreadyCompleteCount ?? 0,
                BlockedCount = result?.BlockedCount ?? 0,
                ManualReviewCount = result?.ManualReviewCount ?? 0,
                RetainedCount = result?.RetainedCount ?? 0,
                DeferredTeachingGroupCount = result?.DeferredTeachingGroupCount ?? 0,
                AffectedSemesterIds = result?.AffectedSemesterIds ?? [],
                Items = result?.Items ?? [],
                BlockingReasons = result?.BlockingReasons ?? [],
                Notes = result?.Notes ?? [],
            };
        }

        if (result is null)
        {
            return new LegacySemesterFinalizationExecutionResultDto
            {
                GeneratedUtc = DateTime.UtcNow,
                TenantId = _currentUser.TenantId,
                IsReadOnly = false,
                ExecutionStatus = "Aborted",
                RolledBack = true,
                AbortReason = "Finalization produced no result.",
            };
        }

        var post = await _finalizationAudit.BuildAuditAsync(cancellationToken);
        var notes = result.Notes.ToList();
        notes.Add(
            $"Post-finalization audit: NullGroup={post.Summary.LegacyNullGroupCount}; TG residuals={post.Summary.TeachingGroupResidualCount}; NotNullReady={post.Summary.NotNullReady}; UniqueReady={post.Summary.UniqueConstraintReady}.");
        notes.Add("Teaching Groups were NOT mutated (Prompt 3E boundary).");

        return new LegacySemesterFinalizationExecutionResultDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = result.TenantId,
            IsReadOnly = false,
            ExecutionStatus = result.ExecutionStatus,
            RolledBack = false,
            ChangedCount = result.ChangedCount,
            AlreadyCompleteCount = result.AlreadyCompleteCount,
            BlockedCount = result.BlockedCount,
            ManualReviewCount = result.ManualReviewCount,
            RetainedCount = result.RetainedCount,
            DeferredTeachingGroupCount = result.DeferredTeachingGroupCount,
            FinalizationTimestamp = result.FinalizationTimestamp,
            AffectedSemesterIds = result.AffectedSemesterIds,
            Items = result.Items,
            BlockingReasons = result.BlockingReasons,
            Notes = notes,
            AbortReason = result.AbortReason,
            PostFinalizationAudit = post,
            SchemaHardeningReady = post.HardeningPreconditions.NotNullMayProceed
                && post.HardeningPreconditions.UniqueMayProceed,
        };
    }

    private async Task<LegacySemesterFinalizationExecutionResultDto> BuildAsync(
        bool mutate,
        CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var notes = new List<string>
        {
            mutate
                ? "Execution mode: journal RETAIN_HISTORICAL only; no TG / Attendance / Student / SA / TT mutation."
                : "Read-only preview; zero writes.",
            "SAFE_SINGLE_GROUP auto-mapping is disabled unless explicit Architect approval map is supplied.",
            "Teaching Group residuals are identify-only (OUT OF SCOPE).",
        };

        // Baseline: Prompt 3A still available (may not match 2B after 3B — expected).
        var plan = await _decisionPlan.BuildDecisionPlanAsync(ct);
        notes.Add($"Prompt 3A decisions loaded ({plan.Decisions.Count}); MatchesPrompt2BBaseline={plan.MatchesPrompt2BBaseline}.");

        var decisionBySem = plan.Decisions.ToDictionary(d => d.SemesterId);

        var legacyQuery = _db.Semesters.Where(s => s.TenantId == tenantId && !s.IsDeleted && s.GroupId == null);
        var legacy = mutate
            ? await legacyQuery.OrderBy(s => s.Id).ToListAsync(ct)
            : await legacyQuery.AsNoTracking().OrderBy(s => s.Id).ToListAsync(ct);

        // Cross-tenant guard: never process rows outside ambient tenant.
        if (legacy.Any(s => s.TenantId != tenantId))
        {
            return Abort(tenantId, mutate, "Cross-tenant Semester detected; fail closed.", notes);
        }

        var legacyIds = legacy.Select(s => s.Id).ToList();

        var groups = await _db.Groups.AsNoTracking()
            .Where(g => g.TenantId == tenantId && !g.IsDeleted)
            .Select(g => new { g.Id, g.CourseId })
            .ToListAsync(ct);
        var groupCountByCourse = groups.GroupBy(g => g.CourseId)
            .ToDictionary(g => g.Key, g => g.Count());

        var duplicateLegacyKeys = legacy
            .GroupBy(s => new { s.CourseId, s.Number })
            .Where(g => g.Count() > 1)
            .Select(g => (g.Key.CourseId, g.Key.Number))
            .ToHashSet();

        var studentCounts = await CountBySemesterAsync(
            _db.Students.AsNoTracking().Where(s => s.TenantId == tenantId && !s.IsDeleted && legacyIds.Contains(s.SemesterId)),
            s => s.SemesterId,
            ct);
        var attCounts = await CountBySemesterAsync(
            _db.AttendanceSessions.AsNoTracking().Where(a => a.TenantId == tenantId && legacyIds.Contains(a.SemesterId)),
            a => a.SemesterId,
            ct);
        var saCounts = await CountBySemesterAsync(
            _db.SchedulingSubjectAllocations.AsNoTracking()
                .Where(a => a.TenantId == tenantId && !a.IsDeleted && legacyIds.Contains(a.SemesterId)),
            a => a.SemesterId,
            ct);
        var ttCounts = await CountBySemesterAsync(
            _db.SchedulingTimetableEntries.AsNoTracking()
                .Where(e => e.TenantId == tenantId && !e.IsDeleted && legacyIds.Contains(e.SemesterId)),
            e => e.SemesterId,
            ct);
        var tgRows = await _db.SchedulingTeachingGroups.AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && legacyIds.Contains(t.SemesterId))
            .Select(t => new { t.Id, t.SemesterId, t.GroupId })
            .ToListAsync(ct);
        var tgCounts = tgRows.GroupBy(t => t.SemesterId).ToDictionary(g => g.Key, g => g.Count());
        var tgIdsBySem = tgRows.GroupBy(t => t.SemesterId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(x => x.Id).ToList());

        var subjectCounts = await CountBySemesterAsync(
            _db.Subjects.AsNoTracking().Where(s => s.TenantId == tenantId && !s.IsDeleted && legacyIds.Contains(s.SemesterId)),
            s => s.SemesterId,
            ct);
        var sectionCounts = await CountBySemesterAsync(
            _db.Sections.AsNoTracking().Where(s => s.TenantId == tenantId && !s.IsDeleted && legacyIds.Contains(s.SemesterId)),
            s => s.SemesterId,
            ct);

        var journals = await _db.LegacySemesterDispositionJournals.AsNoTracking()
            .Where(j => j.TenantId == tenantId && !j.IsDeleted && legacyIds.Contains(j.SemesterId))
            .ToListAsync(ct);
        var journalBySem = journals
            .GroupBy(j => j.SemesterId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.FinalizedUtc).First());

        // Candidate TG targets (identify-only)
        var groupSpecificByCourseNumber = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.GroupId != null)
            .Select(s => new { s.Id, s.CourseId, s.GroupId, s.Number })
            .ToListAsync(ct);

        var items = new List<LegacySemesterFinalizationExecutionItemDto>();
        var blockingReasons = new List<string>();
        var affected = new List<int>();
        var retained = 0;
        var changed = 0;
        var already = 0;
        var blocked = 0;
        var manual = 0;
        var deferredTg = 0;
        DateTime? finalizedUtc = null;
        var journalWrites = 0;

        // Snapshot TG SemesterIds before any work (immutability proof)
        var tgSemesterSnapshot = tgRows.ToDictionary(t => t.Id, t => t.SemesterId);

        foreach (var sem in legacy)
        {
            // Re-read before mutation
            var current = mutate
                ? await _db.Semesters.FirstAsync(s => s.Id == sem.Id && s.TenantId == tenantId, ct)
                : sem;

            if (current.TenantId != tenantId)
                return Abort(tenantId, mutate, $"Semester Id={sem.Id} tenant mismatch.", notes);

            if (current.GroupId is not null)
            {
                // Material baseline drift: was NULL in inventory query, now not — abort on mutate.
                if (mutate)
                    return Abort(tenantId, mutate, $"Semester Id={sem.Id} GroupId changed during finalization; fail closed.", notes);
            }

            decisionBySem.TryGetValue(sem.Id, out var d3a);
            var groupCount = groupCountByCourse.GetValueOrDefault(sem.CourseId, 0);
            var hasDup = duplicateLegacyKeys.Contains((sem.CourseId, sem.Number));

            var (classifierDisposition, classifierEvidence) = LegacySemesterFinalizationClassifier.Classify(
                new LegacySemesterFinalizationClassifier.Input(
                    sem.Id,
                    sem.Number,
                    groupCount,
                    hasDup,
                    studentCounts.GetValueOrDefault(sem.Id),
                    tgCounts.GetValueOrDefault(sem.Id),
                    attCounts.GetValueOrDefault(sem.Id),
                    saCounts.GetValueOrDefault(sem.Id),
                    ttCounts.GetValueOrDefault(sem.Id),
                    subjectCounts.GetValueOrDefault(sem.Id),
                    sectionCounts.GetValueOrDefault(sem.Id),
                    d3a?.DecisionCode));

            // Baseline guard: HISTORICAL_RETAIN must remain empty.
            if (classifierDisposition == LegacySemesterFinalizationDisposition.HistoricalRetain)
            {
                var refs = studentCounts.GetValueOrDefault(sem.Id)
                    + attCounts.GetValueOrDefault(sem.Id)
                    + saCounts.GetValueOrDefault(sem.Id)
                    + ttCounts.GetValueOrDefault(sem.Id)
                    + subjectCounts.GetValueOrDefault(sem.Id)
                    + sectionCounts.GetValueOrDefault(sem.Id)
                    + tgCounts.GetValueOrDefault(sem.Id);
                if (refs != 0)
                {
                    return Abort(
                        tenantId,
                        mutate,
                        $"Semester Id={sem.Id} classified HISTORICAL_RETAIN but refs={refs}; baseline drift — abort.",
                        notes);
                }
            }

            journalBySem.TryGetValue(sem.Id, out var existingJournal);
            var hasJournal = existingJournal is not null
                && (string.Equals(existingJournal.DispositionCode, LegacySemesterExecutionDispositionCodes.RetainHistorical, StringComparison.Ordinal)
                    || string.Equals(existingJournal.DispositionCode, LegacySemesterExecutionDispositionCodes.FinalizedLegacy, StringComparison.Ordinal));

            int? candidateTgTarget = null;
            if (tgIdsBySem.TryGetValue(sem.Id, out var tgIds) && tgIds.Count > 0)
            {
                var firstTg = tgRows.First(t => t.SemesterId == sem.Id);
                var matches = groupSpecificByCourseNumber
                    .Where(s => s.CourseId == sem.CourseId && s.GroupId == firstTg.GroupId && s.Number == sem.Number)
                    .ToList();
                if (matches.Count == 1)
                    candidateTgTarget = matches[0].Id;
            }

            var planResult = LegacySemesterFinalizationExecutionPlanner.Plan(
                new LegacySemesterFinalizationExecutionPlanner.PlanInput(
                    sem.Id,
                    sem.Number,
                    sem.Name,
                    sem.CourseId,
                    current.GroupId,
                    classifierDisposition,
                    classifierEvidence,
                    hasJournal,
                    AllowSafeSingleGroupFinalization: false,
                    ApprovedSingleGroupId: null,
                    tgIdsBySem.GetValueOrDefault(sem.Id) ?? [],
                    candidateTgTarget));

            var semesterMutated = false;
            var journalWritten = false;

            if (mutate && planResult.MutationAllowed)
            {
                if (planResult.AssignGroupId && planResult.AssignedGroupId is int gid)
                {
                    // Explicit approval path only — verify Group ownership.
                    var group = await _db.Groups.AsNoTracking()
                        .FirstOrDefaultAsync(g => g.Id == gid && g.TenantId == tenantId && !g.IsDeleted, ct);
                    if (group is null || group.CourseId != current.CourseId)
                    {
                        return Abort(
                            tenantId,
                            mutate,
                            $"Approved GroupId={gid} invalid for Semester Id={sem.Id}; fail closed.",
                            notes);
                    }

                    current.GroupId = gid;
                    current.UpdatedDate = DateTime.UtcNow;
                    semesterMutated = true;
                    changed++;
                    affected.Add(sem.Id);
                }

                if (planResult.WriteRetainJournal)
                {
                    var now = DateTime.UtcNow;
                    finalizedUtc = now;
                    await _db.AddAsync(new LegacySemesterDispositionJournal
                    {
                        TenantId = tenantId,
                        SemesterId = sem.Id,
                        DispositionCode = planResult.DispositionCode,
                        Evidence = classifierEvidence,
                        PromptCode = LegacySemesterFinalizationExecutionPlanner.PromptCode,
                        AssignedGroupId = planResult.AssignedGroupId,
                        SemesterRowMutated = semesterMutated,
                        FinalizedUtc = now,
                        CreatedDate = now,
                        CreatedBy = _currentUser.UserId,
                    });
                    journalWritten = true;
                    journalWrites++;

                    if (string.Equals(planResult.DispositionCode, LegacySemesterExecutionDispositionCodes.RetainHistorical, StringComparison.Ordinal))
                    {
                        retained++;
                        if (!affected.Contains(sem.Id))
                            affected.Add(sem.Id);
                    }
                    else if (string.Equals(planResult.DispositionCode, LegacySemesterExecutionDispositionCodes.FinalizedLegacy, StringComparison.Ordinal)
                             && !semesterMutated)
                    {
                        // Should not happen for FINALIZED without mutation; count as retained journal.
                        retained++;
                    }
                }
            }
            else if (planResult.Action == "AlreadyComplete")
            {
                already++;
            }
            else if (planResult.Action == "DeferTg")
            {
                deferredTg++;
                blockingReasons.Add($"Sem {sem.Id}: {planResult.BlockingReason}");
            }
            else if (planResult.Action == "Block")
            {
                if (string.Equals(planResult.DispositionCode, LegacySemesterExecutionDispositionCodes.ManualMappingRequired, StringComparison.Ordinal))
                    manual++;
                else
                    blocked++;
                blockingReasons.Add($"Sem {sem.Id}: {planResult.BlockingReason}");
            }

            items.Add(new LegacySemesterFinalizationExecutionItemDto
            {
                SemesterId = sem.Id,
                Number = sem.Number,
                Name = sem.Name,
                CourseId = sem.CourseId,
                ClassifierDispositionCode = LegacySemesterFinalizationClassifier.ToCode(classifierDisposition),
                DispositionCode = planResult.DispositionCode,
                Action = planResult.Action,
                BlockingReason = planResult.BlockingReason,
                MutationAllowed = planResult.MutationAllowed,
                SemesterRowMutated = semesterMutated,
                JournalWritten = journalWritten,
                CandidateTargetSemesterIdForTg = candidateTgTarget,
                TeachingGroupIds = tgIdsBySem.GetValueOrDefault(sem.Id) ?? [],
            });
        }

        if (mutate && journalWrites > 0)
            await _db.SaveChangesAsync(ct);

        // Immutability: TeachingGroup.SemesterId must be unchanged
        if (mutate && tgSemesterSnapshot.Count > 0)
        {
            var tgAfter = await _db.SchedulingTeachingGroups.AsNoTracking()
                .Where(t => tgSemesterSnapshot.Keys.Contains(t.Id))
                .Select(t => new { t.Id, t.SemesterId })
                .ToListAsync(ct);
            foreach (var t in tgAfter)
            {
                if (tgSemesterSnapshot[t.Id] != t.SemesterId)
                    return Abort(tenantId, mutate, $"TeachingGroup Id={t.Id} SemesterId mutated; forbidden.", notes);
            }
        }

        // Prove Semester GroupId unchanged for RETAIN rows
        if (mutate)
        {
            foreach (var item in items.Where(i =>
                i.JournalWritten
                && string.Equals(i.DispositionCode, LegacySemesterExecutionDispositionCodes.RetainHistorical, StringComparison.Ordinal)))
            {
                var row = await _db.Semesters.AsNoTracking().FirstAsync(s => s.Id == item.SemesterId, ct);
                if (row.GroupId is not null)
                    return Abort(tenantId, mutate, $"RETAIN_HISTORICAL Sem {item.SemesterId} unexpectedly received GroupId.", notes);
            }
        }

        notes.Add($"Items={items.Count}; Retained={retained}; Changed={changed}; Already={already}; Manual={manual}; Blocked={blocked}; DeferredTG={deferredTg}.");

        var status = "Completed";
        if (mutate && retained == 0 && changed == 0 && journalWrites == 0 && already > 0
            && items.All(i => i.Action is "AlreadyComplete" or "Block" or "DeferTg" or "Skip"))
        {
            // All eligible retains already journaled — idempotent complete.
            status = already > 0 && retained == 0 && changed == 0 ? "AlreadyComplete" : "Completed";
        }

        if (!mutate)
            status = "NotExecuted";

        return new LegacySemesterFinalizationExecutionResultDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = tenantId,
            IsReadOnly = !mutate,
            ExecutionStatus = status,
            RolledBack = false,
            ChangedCount = changed,
            AlreadyCompleteCount = already,
            BlockedCount = blocked,
            ManualReviewCount = manual,
            RetainedCount = retained,
            DeferredTeachingGroupCount = deferredTg,
            FinalizationTimestamp = finalizedUtc,
            AffectedSemesterIds = affected,
            Items = items,
            BlockingReasons = blockingReasons.Distinct().ToList(),
            Notes = notes,
        };
    }

    private static async Task<Dictionary<int, int>> CountBySemesterAsync<T>(
        IQueryable<T> query,
        Func<T, int> keySelector,
        CancellationToken ct)
    {
        var list = await query.ToListAsync(ct);
        return list.GroupBy(keySelector).ToDictionary(g => g.Key, g => g.Count());
    }

    private LegacySemesterFinalizationExecutionResultDto Abort(
        int tenantId,
        bool mutate,
        string reason,
        List<string> notes)
    {
        notes.Add($"ABORT: {reason}");
        return new LegacySemesterFinalizationExecutionResultDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = tenantId,
            IsReadOnly = !mutate,
            ExecutionStatus = "Aborted",
            RolledBack = mutate,
            AbortReason = reason,
            Notes = notes,
        };
    }
}
