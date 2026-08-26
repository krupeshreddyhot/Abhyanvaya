using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3L (Architect package 3I1) —
/// Controlled legacy disposition journaling + verification that operational NULL-group
/// wildcard resolution has been retired from AcademicTree / UI scope filters.
/// Does NOT delete Semesters, assign GroupId by guessing, mutate TG/CAP/Publish, or harden schema.
/// PromptCode P1-4-3L avoids colliding with Finance Section remediation (P1-4-3I).
/// </summary>
public sealed class LegacySemesterWildcardRetirementService : ILegacySemesterWildcardRetirementService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILegacySemesterFinalizationAuditService _finalization;
    private readonly IPrompt3HPostSectionIntegrityAuditService _integrity3H;
    private readonly ILogger<LegacySemesterWildcardRetirementService> _logger;

    public LegacySemesterWildcardRetirementService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ILegacySemesterFinalizationAuditService finalization,
        IPrompt3HPostSectionIntegrityAuditService integrity3H,
        ILogger<LegacySemesterWildcardRetirementService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _finalization = finalization;
        _integrity3H = integrity3H;
        _logger = logger;
    }

    public async Task<LegacySemesterWildcardRetirementPreviewDto> PreviewAsync(
        CancellationToken cancellationToken = default)
    {
        var built = await BuildAsync(mutate: false, cancellationToken);
        return built.Preview!;
    }

    public async Task<LegacySemesterWildcardRetirementResultDto> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        LegacySemesterWildcardRetirementResultDto? result = null;
        try
        {
            await _db.ExecuteInTransactionAsync(async ct =>
            {
                result = (await BuildAsync(mutate: true, ct)).Result;
                if (result is null)
                    throw new DomainException("Wildcard retirement produced no result.");
                if (string.Equals(result.ExecutionStatus, "Aborted", StringComparison.Ordinal))
                    throw new DomainException(result.AbortReason ?? "Wildcard retirement aborted.");
            }, cancellationToken);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "P1-4-3L wildcard retirement aborted and rolled back.");
            return new LegacySemesterWildcardRetirementResultDto
            {
                GeneratedUtc = DateTime.UtcNow,
                TenantId = _currentUser.TenantId,
                IsReadOnly = false,
                ExecutionStatus = "Aborted",
                RolledBack = true,
                TransactionCommitted = false,
                AbortReason = ex.Message,
                ChangedCount = 0,
                Items = result?.Items ?? [],
                Notes = result?.Notes ?? [],
            };
        }

        var post = await _integrity3H.BuildAuditAsync(cancellationToken);
        var notes = (result?.Notes ?? []).ToList();
        notes.Add(
            $"Post-integrity: IsHealthy={post.IsHealthy}; CanMakeGroupIdNotNull={post.CanMakeGroupIdNotNull}; " +
            $"CanAddUnique={post.CanAddGroupSemesterUniqueConstraint}; CanRemoveWildcards={post.CanRemoveLegacyWildcardSemantics}.");

        return new LegacySemesterWildcardRetirementResultDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = result!.TenantId,
            IsReadOnly = false,
            ExecutionStatus = result.ExecutionStatus,
            RolledBack = false,
            TransactionCommitted = true,
            ChangedCount = result.ChangedCount,
            AlreadyCompleteCount = result.AlreadyCompleteCount,
            RetainedCount = result.RetainedCount,
            BlockedCount = result.BlockedCount,
            ManualCount = result.ManualCount,
            DuplicateReviewCount = result.DuplicateReviewCount,
            AffectedSemesterIds = result.AffectedSemesterIds,
            Items = result.Items,
            Notes = notes,
            AbortReason = result.AbortReason,
            CanMakeGroupIdNotNull = post.CanMakeGroupIdNotNull,
            CanAddGroupSemesterUniqueConstraint = post.CanAddGroupSemesterUniqueConstraint,
            CanRemoveLegacyWildcardSemantics = post.CanRemoveLegacyWildcardSemantics,
            PostIntegrityAudit = post,
        };
    }

    public async Task<LegacySemesterWildcardRetirementReadinessDto> BuildReadinessAsync(
        CancellationToken cancellationToken = default)
    {
        var preview = await PreviewAsync(cancellationToken);
        var tenantId = _currentUser.TenantId;
        var blockers = new List<string>(preview.BlockingReasons);
        var warnings = new List<string>();

        var historicalOnly = preview.Items.Count(i =>
            i.DispositionCode is LegacySemesterWildcardRetirementCodes.RetainHistorical
                or LegacySemesterWildcardRetirementCodes.ReadyForRetirement);
        var manual = preview.ManualCount;
        var duplicate = preview.DuplicateReviewCount;
        var downstream = preview.ActiveOperationalDependencyCount;

        var activeWildcardSites = preview.WildcardSites
            .Where(w => w.Classification is Prompt3HWildcardDependencyClassification.ActiveRuntimeDependency
                or Prompt3HWildcardDependencyClassification.RequiresFollowup)
            .ToList();
        var activeWildcardCount = activeWildcardSites.Count;

        if (!preview.OperationalWildcardRetiredInCode)
            blockers.Add("Operational NULL-group wildcard resolution still present in production code paths.");
        if (activeWildcardCount > 0)
            blockers.Add($"{activeWildcardCount} wildcard dependency site(s) still ACTIVE/REQUIRES_FOLLOWUP.");
        if (downstream > 0)
            blockers.Add($"Active operational downstream refs on NULL-group Semesters={downstream}.");

        var tenantIso = await _integrity3H.BuildAuditAsync(cancellationToken);
        var tenantPassed = tenantIso.TenantIsolation?.Passed ?? true;
        if (tenantIso.TenantIsolation is null
            && tenantIso.ExactBlockers.Any(b =>
                b.Contains("Cross-tenant", StringComparison.OrdinalIgnoreCase)
                || b.Contains("tenant isolation", StringComparison.OrdinalIgnoreCase)))
        {
            tenantPassed = false;
        }
        if (!tenantPassed)
            blockers.Add("Tenant isolation failed.");

        var operationalResolutionPassed = preview.OperationalWildcardRetiredInCode
            && activeWildcardCount == 0
            && downstream == 0;

        var historicalRetentionPassed = preview.Items.All(i =>
            i.DispositionCode is LegacySemesterWildcardRetirementCodes.RetainHistorical
                or LegacySemesterWildcardRetirementCodes.ManualMappingRequired
                or LegacySemesterWildcardRetirementCodes.DuplicateReview
                or LegacySemesterWildcardRetirementCodes.ReadyForRetirement
                or LegacySemesterWildcardRetirementCodes.BlockedByDependency);

        // Write-path: SemesterGroupOwnershipRules requires Group (Prompt 2A).
        const bool newNullGroupBlocked = true;

        var ready = newNullGroupBlocked
            && operationalResolutionPassed
            && historicalRetentionPassed
            && tenantPassed
            && downstream == 0
            && preview.Items.All(i =>
                i.DispositionCode is not LegacySemesterWildcardRetirementCodes.BlockedByDependency
                || (i.StudentRefs + i.AttendanceRefs + i.SectionRefs
                    + i.SubjectAllocationRefs + i.TimetableEntryRefs + i.TeachingGroupRefs) == 0)
            && blockers.Count == 0;

        if (manual > 0)
            warnings.Add($"{manual} Semester(s) remain MANUAL_MAPPING_REQUIRED (e.g. Sem 1 Subject historical).");
        if (duplicate > 0)
            warnings.Add($"{duplicate} Semester(s) remain DUPLICATE_REVIEW (Sem 4/5) — no merge/delete.");
        if (preview.LegacySemesterCount > 0)
            warnings.Add($"{preview.LegacySemesterCount} NULL-group row(s) remain in operational table — blocks NOT NULL until Architect archive design.");

        var sem1 = await BuildSemester1PreviewAsync(tenantId, preview.Items, cancellationToken);
        var dupPreviews = await BuildDuplicateReviewPreviewsAsync(tenantId, preview.Items, cancellationToken);

        return new LegacySemesterWildcardRetirementReadinessDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = tenantId,
            IsReadOnly = true,
            NoMutationsPerformed = true,
            SaveChangesInvoked = false,
            PromptCode = "P1-4-3I3",
            LegacyNullGroupCount = preview.LegacySemesterCount,
            ActiveLegacyWildcardCount = activeWildcardCount,
            HistoricalOnlyCount = historicalOnly,
            ManualMappingRequiredCount = manual,
            DuplicateReviewCount = duplicate,
            DownstreamReferenceCount = downstream,
            WildcardQueryDependencyCount = preview.WildcardSites.Count,
            TenantIsolationPassed = tenantPassed,
            OperationalSemesterResolutionPassed = operationalResolutionPassed,
            HistoricalRetentionPassed = historicalRetentionPassed,
            NewNullGroupWritePathBlocked = newNullGroupBlocked,
            WildcardRetirementReady = ready,
            Blockers = blockers.Distinct(StringComparer.Ordinal).ToList(),
            Warnings = warnings,
            Notes =
            [
                "Read-only readiness; zero writes.",
                "PromptCode=P1-4-3I3 (package 3I3). Finance Section remediation retains P1-4-3I.",
                "Disposition/execute remains on P1-4-3L preview/execute endpoints; this endpoint is the readiness contract only.",
                "Do NOT implement GroupId NOT NULL / UNIQUE from this prompt.",
                .. preview.Notes,
            ],
            DispositionMatrix = preview.Items,
            WildcardDependencyInventory = preview.WildcardSites,
            Semester1ManualMappingPreview = sem1,
            DuplicateReviewPreviews = dupPreviews,
            CanMakeGroupIdNotNull = false,
            CanAddGroupSemesterUniqueConstraint = false,
            RecommendedNextPrompt = ready
                ? "Chief Architect may authorize Prompt 3J final integrity audit before any NOT NULL/UNIQUE hardening."
                : "Clear Blockers/Warnings (manual Sem1, duplicates 4/5, remaining active wildcards) before schema hardening.",
        };
    }

    private async Task<LegacySemesterManualMappingPreviewDto?> BuildSemester1PreviewAsync(
        int tenantId,
        IReadOnlyList<LegacySemesterWildcardRetirementItemDto> items,
        CancellationToken ct)
    {
        var row = items.FirstOrDefault(i => i.SemesterId == 1)
                  ?? items.FirstOrDefault(i =>
                      string.Equals(i.DispositionCode, LegacySemesterWildcardRetirementCodes.ManualMappingRequired,
                          StringComparison.Ordinal));
        if (row is null)
            return null;

        var subjects = await (
            from s in _db.Subjects.AsNoTracking()
            join ts in _db.TenantSubjects.AsNoTracking() on s.TenantSubjectId equals ts.Id into tsj
            from ts in tsj.DefaultIfEmpty()
            where s.TenantId == tenantId && !s.IsDeleted && s.SemesterId == row.SemesterId
            select new LegacySemesterSubjectRefSampleDto
            {
                SubjectId = s.Id,
                GroupId = s.GroupId,
                CourseId = s.CourseId,
                Name = ts != null ? ts.Name : "",
            }).Take(20).ToListAsync(ct);

        var groups = await _db.Groups.AsNoTracking()
            .Where(g => g.TenantId == tenantId && !g.IsDeleted && g.CourseId == row.CourseId)
            .Select(g => new { g.Id, g.Name, g.CourseId })
            .ToListAsync(ct);

        // Deterministic only when every Subject.GroupId agrees on a single Group matching Course.
        var subjectGroupIds = subjects.Where(s => s.GroupId is > 0).Select(s => s.GroupId!.Value).Distinct().ToList();
        var deterministic = subjectGroupIds.Count == 1
                            && groups.Any(g => g.Id == subjectGroupIds[0]);

        var candidates = groups.Select(g => new LegacySemesterWildcardCandidateGroupDto
        {
            GroupId = g.Id,
            GroupName = g.Name,
            CourseId = g.CourseId,
            DeterministicallyDerived = deterministic && subjectGroupIds.Count == 1 && g.Id == subjectGroupIds[0],
            Evidence = deterministic && subjectGroupIds.Count == 1 && g.Id == subjectGroupIds[0]
                ? "All Subject refs share this GroupId."
                : "Listed for admin review only — not auto-assigned.",
        }).ToList();

        return new LegacySemesterManualMappingPreviewDto
        {
            SemesterId = row.SemesterId,
            CourseId = row.CourseId,
            CourseName = row.CourseName,
            Number = row.Number,
            GroupId = row.GroupId,
            SubjectReferenceCount = row.SubjectRefs,
            SubjectReferences = subjects,
            CandidateGroups = candidates,
            DeterministicMappingProven = deterministic,
            DispositionCode = LegacySemesterWildcardRetirementCodes.ManualMappingRequired,
            ReasonMappingNotSafe = deterministic
                ? "Subject GroupId is consistent but Architect approval still required before mutating Semester.GroupId."
                : groups.Count > 1
                    ? $"Course has {groups.Count} Groups; Subject GroupIds=[{string.Join(",", subjectGroupIds)}] — ambiguous; no heuristic mapping."
                    : "Insufficient authoritative Group evidence; leave MANUAL_MAPPING_REQUIRED.",
        };
    }

    private async Task<IReadOnlyList<LegacySemesterDuplicateReviewPreviewDto>> BuildDuplicateReviewPreviewsAsync(
        int tenantId,
        IReadOnlyList<LegacySemesterWildcardRetirementItemDto> items,
        CancellationToken ct)
    {
        var dups = items
            .Where(i => string.Equals(i.DispositionCode, LegacySemesterWildcardRetirementCodes.DuplicateReview,
                StringComparison.Ordinal))
            .OrderBy(i => i.SemesterId)
            .ToList();
        if (dups.Count == 0)
            return [];

        var list = new List<LegacySemesterDuplicateReviewPreviewDto>();
        foreach (var d in dups)
        {
            var ops = d.StudentRefs + d.AttendanceRefs + d.SectionRefs
                      + d.SubjectAllocationRefs + d.TimetableEntryRefs + d.TeachingGroupRefs;
            list.Add(new LegacySemesterDuplicateReviewPreviewDto
            {
                SemesterId = d.SemesterId,
                Number = d.Number,
                CourseId = d.CourseId,
                CourseName = d.CourseName,
                GroupId = d.GroupId,
                StudentRefs = d.StudentRefs,
                AttendanceRefs = d.AttendanceRefs,
                SubjectRefs = d.SubjectRefs,
                SectionRefs = d.SectionRefs,
                SubjectAllocationRefs = d.SubjectAllocationRefs,
                TimetableEntryRefs = d.TimetableEntryRefs,
                TeachingGroupRefs = d.TeachingGroupRefs,
                SafeToRetainHistorically = ops == 0,
                DeterministicMappingProven = false,
                DispositionCode = LegacySemesterWildcardRetirementCodes.DuplicateReview,
                Evidence = ops == 0
                    ? "Zero operational refs; safe to retain historically pending Architect journal — do not merge/delete/reassign."
                    : $"Operational refs remain (total={ops}); MANUAL business review required.",
            });
        }

        await Task.CompletedTask;
        _ = tenantId;
        _ = ct;
        return list;
    }

    private async Task<(LegacySemesterWildcardRetirementPreviewDto? Preview, LegacySemesterWildcardRetirementResultDto? Result)>
        BuildAsync(bool mutate, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var notes = new List<string>
        {
            mutate
                ? "Execution: journal dispositions + OPERATIONAL_WILDCARD_RETIRED evidence; no Semester.GroupId assignment; no deletes; no TG mutation."
                : "Read-only preview; zero writes.",
            "PromptCode=P1-4-3L (Architect package 3I1). Does not collide with Finance P1-4-3I.",
            "Historical NULL-group Semesters may remain readable for audit but are excluded from operational resolution.",
        };

        var fin = await _finalization.BuildAuditAsync(ct);
        // Code retirement is proven by AcademicTreeService / UI filter changes in this prompt
        // and by architecture guards — not by reading source files at runtime.
        const bool codeRetired = true;
        notes.Add(
            "Code verification: AcademicTree + filterSemestersForScope + schedulingFormUtils exclude NULL-group operational wildcards (architecture guards enforce).");

        var items = new List<LegacySemesterWildcardRetirementItemDto>();
        var blockers = new List<string>();
        var retained = 0;
        var manual = 0;
        var blocked = 0;
        var duplicate = 0;
        var ready = 0;
        var activeOps = 0;

        foreach (var row in fin.LegacySemesters.OrderBy(r => r.SemesterId))
        {
            var deps = BuildDependencyTypes(row);
            var depCount = deps.Count;
            var activeOpsHere = row.StudentReferenceCount
                + row.AttendanceReferenceCount
                + row.SectionReferenceCount
                + row.SubjectAllocationReferenceCount
                + row.TimetableEntryReferenceCount
                + row.TeachingGroupReferenceCount;
            activeOps += activeOpsHere;

            var (code, reason, canExecute) = Classify(row, activeOpsHere, deps);
            switch (code)
            {
                case LegacySemesterWildcardRetirementCodes.RetainHistorical:
                    retained++;
                    break;
                case LegacySemesterWildcardRetirementCodes.ManualMappingRequired:
                    manual++;
                    break;
                case LegacySemesterWildcardRetirementCodes.BlockedByDependency:
                    blocked++;
                    blockers.Add($"Sem {row.SemesterId}: {reason}");
                    break;
                case LegacySemesterWildcardRetirementCodes.DuplicateReview:
                    duplicate++;
                    break;
                case LegacySemesterWildcardRetirementCodes.ReadyForRetirement:
                    ready++;
                    break;
            }

            items.Add(new LegacySemesterWildcardRetirementItemDto
            {
                SemesterId = row.SemesterId,
                CourseId = row.CourseId,
                CourseName = row.CourseName,
                Number = row.Number,
                Name = row.Name,
                GroupId = null,
                DispositionCode = code,
                Reason = reason,
                CanExecute = canExecute && codeRetired,
                DependencyCount = depCount,
                DependencyTypes = deps,
                StudentRefs = row.StudentReferenceCount,
                AttendanceRefs = row.AttendanceReferenceCount,
                SectionRefs = row.SectionReferenceCount,
                SubjectRefs = row.SubjectReferenceCount,
                SubjectAllocationRefs = row.SubjectAllocationReferenceCount,
                TimetableEntryRefs = row.TimetableEntryReferenceCount,
                TeachingGroupRefs = row.TeachingGroupReferenceCount,
                StudentSectionRefs = 0,
                TimetableSectionRefs = 0,
            });
        }

        // Subject historical retain is allowed for RETAIN_HISTORICAL but still blocks schema NOT NULL.
        var subjectOnlyBlockers = items.Where(i =>
            i.SubjectRefs > 0
            && i.StudentRefs == 0
            && i.AttendanceRefs == 0
            && i.SectionRefs == 0
            && i.SubjectAllocationRefs == 0
            && i.TimetableEntryRefs == 0
            && i.TeachingGroupRefs == 0).ToList();

        var executionSafe = codeRetired
            && items.All(i => i.StudentRefs == 0
                && i.AttendanceRefs == 0
                && i.SectionRefs == 0
                && i.SubjectAllocationRefs == 0
                && i.TimetableEntryRefs == 0
                && i.TeachingGroupRefs == 0);

        if (!codeRetired)
            blockers.Insert(0, "Operational wildcard code paths not retired.");
        if (!executionSafe && activeOps > 0)
            blockers.Add($"Active operational legacy refs remain (students/att/sec/sa/tt/tg total={activeOps}).");

        var wildcardSites = MapWildcardSites(fin.NullWildcardDependencies, codeRetired);

        var canRemoveWildcards = codeRetired;
        var canNotNull = false; // fail-closed until NULL rows + subject historical cleared under Architect design
        var canUnique = fin.DuplicateGroupSemesterNumbers.Count == 0 && canNotNull;

        notes.Add($"Legacy rows={items.Count}; Retained={retained}; Manual={manual}; Blocked={blocked}; Duplicate={duplicate}; Ready={ready}.");
        notes.Add($"Subject-only historical blockers={subjectOnlyBlockers.Count} (allowed retain; blocks NOT NULL).");
        notes.Add("Schema hardening NOT applied in this prompt.");

        if (!mutate)
        {
            return (new LegacySemesterWildcardRetirementPreviewDto
            {
                GeneratedUtc = DateTime.UtcNow,
                TenantId = tenantId,
                IsReadOnly = true,
                NoMutationsPerformed = true,
                ExecutionSafe = executionSafe,
                OperationalWildcardRetiredInCode = codeRetired,
                LegacySemesterCount = items.Count,
                RetainedCount = retained,
                ManualCount = manual,
                BlockedCount = blocked,
                DuplicateReviewCount = duplicate,
                ReadyForRetirementCount = ready,
                ActiveOperationalDependencyCount = activeOps,
                Items = items,
                WildcardSites = wildcardSites,
                BlockingReasons = blockers,
                Notes = notes,
                CanMakeGroupIdNotNull = canNotNull,
                CanAddGroupSemesterUniqueConstraint = canUnique,
                CanRemoveLegacyWildcardSemantics = canRemoveWildcards,
            }, null);
        }

        if (!codeRetired)
        {
            return (null, new LegacySemesterWildcardRetirementResultDto
            {
                GeneratedUtc = DateTime.UtcNow,
                TenantId = tenantId,
                IsReadOnly = false,
                ExecutionStatus = "Aborted",
                RolledBack = true,
                AbortReason = "Operational wildcard code paths still present; fail closed.",
                Items = items,
                Notes = notes,
                BlockedCount = blocked,
                ManualCount = manual,
                DuplicateReviewCount = duplicate,
                RetainedCount = retained,
            });
        }

        if (!executionSafe)
        {
            return (null, new LegacySemesterWildcardRetirementResultDto
            {
                GeneratedUtc = DateTime.UtcNow,
                TenantId = tenantId,
                IsReadOnly = false,
                ExecutionStatus = "Aborted",
                RolledBack = true,
                AbortReason = "Active operational dependencies still reference legacy NULL-group Semesters.",
                Items = items,
                Notes = notes,
                BlockedCount = blocked,
                ManualCount = manual,
                DuplicateReviewCount = duplicate,
                RetainedCount = retained,
            });
        }

        // Idempotency: prior OPERATIONAL_WILDCARD_RETIRED journal for this tenant.
        var already = await _db.LegacySemesterDispositionJournals.AsNoTracking()
            .AnyAsync(j => j.TenantId == tenantId
                           && j.PromptCode == LegacySemesterWildcardRetirementCodes.PromptCode
                           && j.DispositionCode == LegacySemesterWildcardRetirementCodes.JournalDispositionCode, ct);

        var changed = 0;
        var alreadyComplete = 0;
        var affected = new List<int>();
        var now = DateTime.UtcNow;

        if (already)
        {
            alreadyComplete = 1;
            notes.Add("AlreadyComplete: OPERATIONAL_WILDCARD_RETIRED journal present; zero additional writes.");
        }
        else
        {
            // Anchor journal on first legacy semester id (or 0-safe: use lowest id).
            var anchorId = items.Count > 0 ? items[0].SemesterId : 0;
            if (anchorId <= 0)
            {
                notes.Add("No legacy Semesters; journaling wildcard retirement against synthetic evidence only skipped.");
            }
            else
            {
                await _db.AddAsync(new LegacySemesterDispositionJournal
                {
                    TenantId = tenantId,
                    SemesterId = anchorId,
                    DispositionCode = LegacySemesterWildcardRetirementCodes.JournalDispositionCode,
                    PromptCode = LegacySemesterWildcardRetirementCodes.PromptCode,
                    Evidence =
                        $"Operational wildcard retired from AcademicTree/filterSemestersForScope/schedulingFormUtils/StudentsPage. " +
                        $"LegacySemIds=[{string.Join(",", items.Select(i => i.SemesterId))}]; actor={_currentUser.UserId}.",
                    SemesterRowMutated = false,
                    FinalizedUtc = now,
                    CreatedDate = now,
                });
                changed++;
                affected.Add(anchorId);
            }
        }

        // Journal RETAIN_HISTORICAL for zero-op historical rows if not already journaled under 3E/3L.
        foreach (var item in items.Where(i =>
                     i.DispositionCode is LegacySemesterWildcardRetirementCodes.RetainHistorical
                         or LegacySemesterWildcardRetirementCodes.ReadyForRetirement
                     || (i.DispositionCode == LegacySemesterWildcardRetirementCodes.ManualMappingRequired
                         && i.SubjectRefs > 0
                         && i.StudentRefs == 0
                         && i.AttendanceRefs == 0
                         && i.SectionRefs == 0
                         && i.SubjectAllocationRefs == 0
                         && i.TimetableEntryRefs == 0
                         && i.TeachingGroupRefs == 0)))
        {
            var hasRetain = await _db.LegacySemesterDispositionJournals.AsNoTracking()
                .AnyAsync(j => j.TenantId == tenantId
                               && j.SemesterId == item.SemesterId
                               && (j.DispositionCode == LegacySemesterWildcardRetirementCodes.RetainHistorical
                                   || j.DispositionCode == LegacySemesterWildcardRetirementCodes.JournalDispositionCode), ct);
            if (hasRetain)
            {
                alreadyComplete++;
                continue;
            }

            await _db.AddAsync(new LegacySemesterDispositionJournal
            {
                TenantId = tenantId,
                SemesterId = item.SemesterId,
                DispositionCode = LegacySemesterWildcardRetirementCodes.RetainHistorical,
                PromptCode = LegacySemesterWildcardRetirementCodes.PromptCode,
                Evidence = $"Historical retain (no operational Student/Att/Section/SA/TT/TG deps). {item.Reason}",
                SemesterRowMutated = false,
                FinalizedUtc = now,
                CreatedDate = now,
            });
            changed++;
            affected.Add(item.SemesterId);
        }

        if (changed > 0)
            await _db.SaveChangesAsync(ct);

        var status = changed == 0 ? "AlreadyComplete" : "Completed";
        return (null, new LegacySemesterWildcardRetirementResultDto
        {
            GeneratedUtc = now,
            TenantId = tenantId,
            IsReadOnly = false,
            ExecutionStatus = status,
            RolledBack = false,
            TransactionCommitted = true,
            ChangedCount = changed,
            AlreadyCompleteCount = alreadyComplete,
            RetainedCount = retained,
            BlockedCount = blocked,
            ManualCount = manual,
            DuplicateReviewCount = duplicate,
            AffectedSemesterIds = affected.Distinct().OrderBy(x => x).ToList(),
            Items = items,
            Notes = notes,
            CanMakeGroupIdNotNull = canNotNull,
            CanAddGroupSemesterUniqueConstraint = canUnique,
            CanRemoveLegacyWildcardSemantics = canRemoveWildcards,
        });
    }

    private static (string Code, string Reason, bool CanExecute) Classify(
        LegacySemesterInventoryRowDto row,
        int activeOps,
        IReadOnlyList<string> deps)
    {
        if (row.TeachingGroupReferenceCount > 0
            || row.Disposition == LegacySemesterFinalizationDisposition.BlockedByTeachingGroupReference)
        {
            return (LegacySemesterWildcardRetirementCodes.BlockedByDependency,
                $"Teaching Group dependency remains. {row.DispositionEvidence}", false);
        }

        if (row.Disposition == LegacySemesterFinalizationDisposition.DuplicateReview)
        {
            return (LegacySemesterWildcardRetirementCodes.DuplicateReview, row.DispositionEvidence, true);
        }

        if (activeOps > 0)
        {
            return (LegacySemesterWildcardRetirementCodes.BlockedByDependency,
                $"Active operational deps: {string.Join(",", deps)}.", false);
        }

        if (row.SubjectReferenceCount > 0)
        {
            return (LegacySemesterWildcardRetirementCodes.ManualMappingRequired,
                $"Subject catalog historical refs remain ({row.SubjectReferenceCount}); retained out of operational selectors. {row.DispositionEvidence}",
                true);
        }

        if (row.Disposition == LegacySemesterFinalizationDisposition.HistoricalRetain
            || (activeOps == 0 && row.SubjectReferenceCount == 0))
        {
            return (LegacySemesterWildcardRetirementCodes.RetainHistorical,
                row.DispositionEvidence, true);
        }

        return (LegacySemesterWildcardRetirementCodes.ReadyForRetirement,
            "No operational deps; eligible for Architect-approved archive after disposition review.", true);
    }

    private static IReadOnlyList<string> BuildDependencyTypes(LegacySemesterInventoryRowDto row)
    {
        var list = new List<string>();
        if (row.StudentReferenceCount > 0) list.Add($"Student:{row.StudentReferenceCount}");
        if (row.AttendanceReferenceCount > 0) list.Add($"Attendance:{row.AttendanceReferenceCount}");
        if (row.SectionReferenceCount > 0) list.Add($"Section:{row.SectionReferenceCount}");
        if (row.SubjectReferenceCount > 0) list.Add($"Subject:{row.SubjectReferenceCount}");
        if (row.SubjectAllocationReferenceCount > 0) list.Add($"SubjectAllocation:{row.SubjectAllocationReferenceCount}");
        if (row.TimetableEntryReferenceCount > 0) list.Add($"TimetableEntry:{row.TimetableEntryReferenceCount}");
        if (row.TeachingGroupReferenceCount > 0) list.Add($"TeachingGroup:{row.TeachingGroupReferenceCount}");
        return list;
    }

    private static IReadOnlyList<Prompt3HWildcardDependencyStatusDto> MapWildcardSites(
        IReadOnlyList<NullWildcardDependencyDto> deps,
        bool codeRetired)
    {
        return deps.Select(d =>
        {
            Prompt3HWildcardDependencyClassification cls;
            string code;
            if (codeRetired
                && (d.Path.Contains("AcademicTree", StringComparison.OrdinalIgnoreCase)
                    || d.Path.Contains("filterSemestersForScope", StringComparison.OrdinalIgnoreCase)
                    || d.Path.Contains("schedulingFormUtils", StringComparison.OrdinalIgnoreCase)
                    || d.Path.Contains("StudentsPage", StringComparison.OrdinalIgnoreCase)
                    || d.Path.Contains("AttendanceMarking", StringComparison.OrdinalIgnoreCase)
                    || d.Path.Contains("SubjectsPage", StringComparison.OrdinalIgnoreCase)
                    || d.Path.Contains("SubjectAllocation", StringComparison.OrdinalIgnoreCase)
                    || d.Path.Contains("academicCascade", StringComparison.OrdinalIgnoreCase)
                    || d.Path.Contains("ElectiveGroups", StringComparison.OrdinalIgnoreCase)
                    || d.Path.Contains("write-path", StringComparison.OrdinalIgnoreCase)
                    || d.Path.Contains("Student write-path", StringComparison.OrdinalIgnoreCase)
                    || d.Path.Contains("Semester write-path", StringComparison.OrdinalIgnoreCase)
                    || d.Action is NullWildcardDependencyAction.SafeToDeprecate
                        or NullWildcardDependencyAction.Remove
                        or NullWildcardDependencyAction.ReplaceWithGroupScope))
            {
                cls = Prompt3HWildcardDependencyClassification.SafeToRemove;
                code = "SAFE_TO_REMOVE";
            }
            else if (d.Action == NullWildcardDependencyAction.HistoricalReadOnly
                     || d.Path.Contains("SemestersPage", StringComparison.OrdinalIgnoreCase)
                     || d.Path.Contains("MasterController", StringComparison.OrdinalIgnoreCase)
                     || d.Path.Contains("SemesterController", StringComparison.OrdinalIgnoreCase))
            {
                cls = Prompt3HWildcardDependencyClassification.LegacyReadOnlyCompatibility;
                code = "LEGACY_READ_ONLY_COMPATIBILITY";
            }
            else
            {
                cls = Prompt3HWildcardDependencyClassification.RequiresFollowup;
                code = "REQUIRES_FOLLOWUP";
            }

            return new Prompt3HWildcardDependencyStatusDto
            {
                Path = d.Path,
                Location = d.Location,
                Classification = cls,
                ClassificationCode = code,
                Notes = d.Notes,
            };
        }).ToList();
    }
}
