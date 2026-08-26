using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3J-A (PromptCode P1-4-3JA) —
/// Explicit historical disposition for legacy Semesters. Reuses finalization audit as SSOT for
/// downstream refs. Never assigns GroupId. Never deletes/merges Semesters. No schema hardening.
/// </summary>
public sealed class LegacySemesterHistoricalDispositionService : ILegacySemesterHistoricalDispositionService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILegacySemesterFinalizationAuditService _finalization;
    private readonly ILogger<LegacySemesterHistoricalDispositionService> _logger;

    public LegacySemesterHistoricalDispositionService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ILegacySemesterFinalizationAuditService finalization,
        ILogger<LegacySemesterHistoricalDispositionService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _finalization = finalization;
        _logger = logger;
    }

    public async Task<LegacySemesterHistoricalDispositionPreviewDto> PreviewAsync(
        CancellationToken cancellationToken = default)
    {
        var built = await BuildPreviewCoreAsync(cancellationToken);
        return built;
    }

    public async Task<LegacySemesterHistoricalDispositionResultDto> ExecuteAsync(
        LegacySemesterHistoricalDispositionExecuteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var correlationId = Guid.NewGuid().ToString("N");
        LegacySemesterHistoricalDispositionResultDto? result = null;

        try
        {
            await _db.ExecuteInTransactionAsync(async ct =>
            {
                result = await ExecuteCoreAsync(request, correlationId, ct);
                if (result is null)
                    throw new DomainException("Historical disposition produced no result.");
                if (!result.IsSuccessful
                    && !string.Equals(result.ExecutionStatus, "AlreadyComplete", StringComparison.Ordinal))
                    throw new DomainException(result.AbortReason ?? "Historical disposition aborted.");
            }, cancellationToken);
        }
        catch (ConcurrencyConflictException ex)
        {
            _logger.LogWarning(ex, "P1-4-3JA historical disposition concurrency conflict; rolled back.");
            return Aborted(
                _currentUser.TenantId,
                correlationId,
                ex.Message,
                "ConcurrencyConflictException",
                result?.Findings ?? []);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "P1-4-3JA historical disposition EF concurrency conflict; rolled back.");
            return Aborted(
                _currentUser.TenantId,
                correlationId,
                "Concurrency conflict while applying historical disposition.",
                "DbUpdateConcurrencyException",
                result?.Findings ?? []);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "P1-4-3JA historical disposition aborted and rolled back.");
            return Aborted(
                _currentUser.TenantId,
                correlationId,
                ex.Message,
                null,
                result?.Findings ?? []);
        }

        var integrity = await BuildPostIntegrityAsync(cancellationToken);
        if (!integrity.Passed)
        {
            _logger.LogError(
                "P1-4-3JA post-disposition integrity failed after commit marker; findings={Findings}",
                string.Join("; ", integrity.Findings));
            // Transaction already committed only when ExecuteInTransaction succeeded.
            // If integrity fails after commit, surface as unsuccessful for operators (fail closed on gate).
            return new LegacySemesterHistoricalDispositionResultDto
            {
                GeneratedUtc = DateTime.UtcNow,
                TenantId = result!.TenantId,
                IsSuccessful = false,
                ExecutionStatus = "IntegrityFailed",
                CorrelationId = correlationId,
                RolledBack = false,
                TransactionCommitted = true,
                ChangedCount = result.ChangedCount,
                AlreadyCompleteCount = result.AlreadyCompleteCount,
                ManualReviewCount = result.ManualReviewCount,
                DuplicateReviewCount = result.DuplicateReviewCount,
                BlockedCount = result.BlockedCount,
                JournalOnlyCount = result.JournalOnlyCount,
                AbortReason = "Post-disposition integrity validation failed.",
                Findings = result.Findings,
                PostDispositionIntegrity = integrity,
                Notes = result.Notes.Concat(["Post-disposition integrity FAILED."]).ToList(),
                SchemaHardeningReady = false,
                Prompt3JAuthorized = false,
            };
        }

        return new LegacySemesterHistoricalDispositionResultDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = result!.TenantId,
            IsSuccessful = result.IsSuccessful || string.Equals(result.ExecutionStatus, "AlreadyComplete", StringComparison.Ordinal),
            ExecutionStatus = result.ExecutionStatus,
            CorrelationId = correlationId,
            RolledBack = false,
            TransactionCommitted = true,
            ChangedCount = result.ChangedCount,
            AlreadyCompleteCount = result.AlreadyCompleteCount,
            ManualReviewCount = result.ManualReviewCount,
            DuplicateReviewCount = result.DuplicateReviewCount,
            BlockedCount = result.BlockedCount,
            JournalOnlyCount = result.JournalOnlyCount,
            Findings = result.Findings,
            PostDispositionIntegrity = integrity,
            Notes = result.Notes,
            SchemaHardeningReady = false,
            Prompt3JAuthorized = false,
        };
    }

    private async Task<LegacySemesterHistoricalDispositionPreviewDto> BuildPreviewCoreAsync(
        CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var candidates = await BuildCandidatesAsync(tenantId, ct);

        return new LegacySemesterHistoricalDispositionPreviewDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = tenantId,
            IsReadOnly = true,
            NoMutationsPerformed = true,
            PromptCode = LegacySemesterHistoricalDispositionCodes.PromptCode,
            LegacyNullGroupCount = candidates.Count(c => c.GroupId is null),
            HistoricalArchiveCount = candidates.Count(c => c.IsHistoricalArchive),
            PendingReviewCount = candidates.Count(c =>
                string.Equals(c.RecommendedDisposition,
                    LegacySemesterHistoricalDispositionCodes.RetainHistoricalPendingReview,
                    StringComparison.Ordinal)),
            DuplicateReviewCount = candidates.Count(c =>
                string.Equals(c.RecommendedDisposition,
                    LegacySemesterHistoricalDispositionCodes.DuplicateReview,
                    StringComparison.Ordinal)),
            ManualMappingRequiredCount = candidates.Count(c =>
                string.Equals(c.RecommendedDisposition,
                    LegacySemesterHistoricalDispositionCodes.ManualMappingRequired,
                    StringComparison.Ordinal)),
            EligibleForHistoricalArchiveCount = candidates.Count(c => c.EligibleForHistoricalArchive),
            Candidates = candidates,
            DependencyMatrix = BuildDependencyMatrix(),
            Notes =
            [
                "Preview is read-only; zero writes.",
                "PromptCode=P1-4-3JA. Does not collide with Subject Catalog remediation P1-4-3J.",
                "No Group guessing. No deletes. No merges. No NOT NULL / UNIQUE.",
                "HISTORICAL_ARCHIVE mutates Semester.IsHistoricalArchive only when ops refs are clear.",
                "MANUAL_MAPPING_REQUIRED / DUPLICATE_REVIEW / RETAIN_HISTORICAL_PENDING_REVIEW are journal/workflow states.",
                "SchemaHardeningReady remains FALSE while unresolved NULL-group / pending / duplicate rows remain.",
                "Prompt 3J (DDL) is NOT authorized by this preview.",
            ],
            SchemaHardeningReady = false,
            Prompt3JAuthorized = false,
        };
    }

    private async Task<LegacySemesterHistoricalDispositionResultDto> ExecuteCoreAsync(
        LegacySemesterHistoricalDispositionExecuteRequest request,
        string correlationId,
        CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var notes = new List<string>
        {
            "Execution: explicit per-Semester disposition only; no archive-all; no GroupId assignment.",
            $"CorrelationId={correlationId}",
            $"ActorUserId={_currentUser.UserId}",
        };

        if (request.Items is null || request.Items.Count == 0)
        {
            return new LegacySemesterHistoricalDispositionResultDto
            {
                GeneratedUtc = DateTime.UtcNow,
                TenantId = tenantId,
                IsSuccessful = false,
                ExecutionStatus = "Aborted",
                CorrelationId = correlationId,
                AbortReason = "Explicit disposition Items are required (no archive-all).",
                Notes = notes,
            };
        }

        if (request.Items.Select(i => i.SemesterId).Distinct().Count() != request.Items.Count)
        {
            return new LegacySemesterHistoricalDispositionResultDto
            {
                GeneratedUtc = DateTime.UtcNow,
                TenantId = tenantId,
                IsSuccessful = false,
                ExecutionStatus = "Aborted",
                CorrelationId = correlationId,
                AbortReason = "Duplicate SemesterId in request; fail closed.",
                Notes = notes,
            };
        }

        var candidates = (await BuildCandidatesAsync(tenantId, ct))
            .ToDictionary(c => c.SemesterId);
        var findings = new List<LegacySemesterHistoricalDispositionFindingDto>();
        var changed = 0;
        var already = 0;
        var manual = 0;
        var duplicate = 0;
        var blocked = 0;
        var journalOnly = 0;
        var anyMutation = false;

        foreach (var item in request.Items.OrderBy(i => i.SemesterId))
        {
            var disposition = (item.Disposition ?? "").Trim();
            if (!LegacySemesterHistoricalDispositionCodes.All.Contains(disposition))
            {
                blocked++;
                findings.Add(Finding(item.SemesterId, disposition, "", disposition, "Blocked",
                    false, 0, $"Unknown disposition '{disposition}'."));
                continue;
            }

            if (!candidates.TryGetValue(item.SemesterId, out var candidate))
            {
                // Fail closed: semester must belong to tenant and appear in inventory.
                var owned = await _db.Semesters.AsNoTracking()
                    .AnyAsync(s => s.Id == item.SemesterId && s.TenantId == tenantId && !s.IsDeleted, ct);
                blocked++;
                findings.Add(Finding(item.SemesterId, disposition, "", disposition, "Blocked",
                    false, 0,
                    owned
                        ? "Semester is not a legacy NULL-group / historical-disposition candidate."
                        : "Semester not found for tenant (fail closed)."));
                continue;
            }

            if (!candidate.AllowedDispositions.Contains(disposition, StringComparer.OrdinalIgnoreCase))
            {
                blocked++;
                findings.Add(Finding(item.SemesterId, disposition, candidate.RecommendedDisposition,
                    disposition, "Blocked", false, candidate.OperationalRefTotal,
                    $"Disposition '{disposition}' not allowed for Semester {item.SemesterId}: {candidate.Reason}"));
                continue;
            }

            // Re-read tracked row inside transaction.
            var semester = await _db.Semesters
                .FirstOrDefaultAsync(s => s.Id == item.SemesterId && s.TenantId == tenantId && !s.IsDeleted, ct);
            if (semester is null)
            {
                blocked++;
                findings.Add(Finding(item.SemesterId, disposition, "", disposition, "Blocked",
                    false, 0, "Semester disappeared or wrong tenant; fail closed."));
                continue;
            }

            // Never guess / mutate GroupId.
            if (semester.GroupId is not null
                && string.Equals(disposition, LegacySemesterHistoricalDispositionCodes.HistoricalArchive,
                    StringComparison.OrdinalIgnoreCase)
                && !semester.IsHistoricalArchive)
            {
                // Group-owned operational rows are not archived via this legacy path.
                blocked++;
                findings.Add(Finding(item.SemesterId, disposition,
                    semester.IsHistoricalArchive ? "HISTORICAL_ARCHIVE" : "OPERATIONAL",
                    disposition, "Blocked", false, 0,
                    "Group-owned Semesters are outside legacy historical disposition scope."));
                continue;
            }

            var liveOps = await CountOperationalRefsAsync(tenantId, semester.Id, ct);
            if (string.Equals(disposition, LegacySemesterHistoricalDispositionCodes.HistoricalArchive,
                    StringComparison.OrdinalIgnoreCase)
                && liveOps > 0)
            {
                blocked++;
                findings.Add(Finding(item.SemesterId, disposition,
                    DescribeState(semester), disposition, "Blocked", false, liveOps,
                    $"Operational downstream refs remain ({liveOps}); remap before HISTORICAL_ARCHIVE."));
                continue;
            }

            var previous = DescribeState(semester);
            var priorJournal = await _db.LegacySemesterDispositionJournals.AsNoTracking()
                .Where(j => j.TenantId == tenantId
                            && j.SemesterId == semester.Id
                            && j.PromptCode == LegacySemesterHistoricalDispositionCodes.PromptCode
                            && j.DispositionCode == disposition)
                .OrderByDescending(j => j.FinalizedUtc)
                .FirstOrDefaultAsync(ct);

            var alreadyArchive = string.Equals(disposition,
                    LegacySemesterHistoricalDispositionCodes.HistoricalArchive,
                    StringComparison.OrdinalIgnoreCase)
                && semester.IsHistoricalArchive
                && priorJournal is not null;

            var alreadyJournal = !LegacySemesterHistoricalDispositionCodes.MutatesSemesterRow(disposition)
                && priorJournal is not null;

            if (alreadyArchive || alreadyJournal)
            {
                already++;
                findings.Add(Finding(item.SemesterId, disposition, previous, previous, "AlreadyComplete",
                    false, liveOps, "Same disposition already applied; zero additional writes."));
                continue;
            }

            var mutated = false;
            if (string.Equals(disposition, LegacySemesterHistoricalDispositionCodes.HistoricalArchive,
                    StringComparison.OrdinalIgnoreCase))
            {
                semester.IsHistoricalArchive = true;
                semester.UpdatedDate = DateTime.UtcNow;
                semester.UpdatedBy = _currentUser.UserId;
                mutated = true;
                anyMutation = true;
                changed++;
            }
            else
            {
                journalOnly++;
                anyMutation = true;
                if (string.Equals(disposition, LegacySemesterHistoricalDispositionCodes.ManualMappingRequired,
                        StringComparison.OrdinalIgnoreCase))
                    manual++;
                else if (string.Equals(disposition, LegacySemesterHistoricalDispositionCodes.DuplicateReview,
                             StringComparison.OrdinalIgnoreCase))
                    duplicate++;
                else
                    changed++;
            }

            var evidence =
                $"corr={correlationId}; prev={previous}; reason={(request.Reason ?? candidate.Reason)}; " +
                $"opsRefs={liveOps}; subjectRefs={candidate.SubjectRefs}; groupId={semester.GroupId?.ToString() ?? "NULL"}; " +
                "noGroupGuess=true; noDelete=true; noTgMutation=true; noTimetableSectionWrite=true";

            await _db.AddAsync(new LegacySemesterDispositionJournal
            {
                TenantId = tenantId,
                SemesterId = semester.Id,
                DispositionCode = disposition.ToUpperInvariant(),
                Evidence = Truncate(evidence, 2000),
                PromptCode = LegacySemesterHistoricalDispositionCodes.PromptCode,
                AssignedGroupId = null,
                SemesterRowMutated = mutated,
                FinalizedUtc = DateTime.UtcNow,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId,
            });

            findings.Add(Finding(item.SemesterId, disposition, previous,
                mutated ? "HISTORICAL_ARCHIVE" : disposition.ToUpperInvariant(),
                mutated ? "Changed" : "Journaled",
                mutated, liveOps,
                mutated
                    ? "Semester.IsHistoricalArchive set; GroupId unchanged."
                    : "Workflow/journal disposition recorded; Semester row not archived."));
        }

        if (blocked > 0 && changed == 0 && journalOnly == 0 && already == 0)
        {
            return new LegacySemesterHistoricalDispositionResultDto
            {
                GeneratedUtc = DateTime.UtcNow,
                TenantId = tenantId,
                IsSuccessful = false,
                ExecutionStatus = "Aborted",
                CorrelationId = correlationId,
                AbortReason = "All requested dispositions were blocked; fail closed.",
                BlockedCount = blocked,
                Findings = findings,
                Notes = notes,
            };
        }

        if (blocked > 0)
        {
            // Partial request success is not allowed — roll back entire batch.
            throw new DomainException(
                $"One or more dispositions blocked ({blocked}); entire batch rolled back (no partial completion).");
        }

        if (!anyMutation && already > 0)
        {
            return new LegacySemesterHistoricalDispositionResultDto
            {
                GeneratedUtc = DateTime.UtcNow,
                TenantId = tenantId,
                IsSuccessful = true,
                ExecutionStatus = "AlreadyComplete",
                CorrelationId = correlationId,
                ChangedCount = 0,
                AlreadyCompleteCount = already,
                ManualReviewCount = manual,
                DuplicateReviewCount = duplicate,
                BlockedCount = 0,
                JournalOnlyCount = 0,
                Findings = findings,
                Notes = notes,
                SchemaHardeningReady = false,
                Prompt3JAuthorized = false,
            };
        }

        await ConcurrencyExceptionHelper.SaveChangesAsync(_db, ct);

        // In-transaction integrity check before commit.
        var integrity = await BuildPostIntegrityAsync(ct);
        if (!integrity.Passed)
            throw new DomainException(
                "Post-disposition integrity validation failed inside transaction: "
                + string.Join("; ", integrity.Findings));

        notes.Add($"Changed={changed}; AlreadyComplete={already}; JournalOnly={journalOnly}; Manual={manual}; Duplicate={duplicate}.");
        notes.Add("SchemaHardeningReady=FALSE; Prompt3JAuthorized=FALSE.");

        return new LegacySemesterHistoricalDispositionResultDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = tenantId,
            IsSuccessful = true,
            ExecutionStatus = "Completed",
            CorrelationId = correlationId,
            ChangedCount = changed + journalOnly,
            AlreadyCompleteCount = already,
            ManualReviewCount = manual,
            DuplicateReviewCount = duplicate,
            BlockedCount = 0,
            JournalOnlyCount = journalOnly,
            Findings = findings,
            PostDispositionIntegrity = integrity,
            Notes = notes,
            SchemaHardeningReady = false,
            Prompt3JAuthorized = false,
        };
    }

    private async Task<List<LegacySemesterHistoricalDispositionCandidateDto>> BuildCandidatesAsync(
        int tenantId, CancellationToken ct)
    {
        var fin = await _finalization.BuildAuditAsync(ct);
        var legacyIds = fin.LegacySemesters.Select(l => l.SemesterId).Distinct().ToList();

        // Inventory SSOT from finalization audit; DB supplies current IsHistoricalArchive / GroupId.
        var dbRows = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted
                        && (legacyIds.Contains(s.Id) || s.GroupId == null || s.IsHistoricalArchive))
            .Select(s => new
            {
                s.Id,
                s.CourseId,
                CourseName = s.Course != null ? s.Course.Name : "",
                s.Number,
                s.Name,
                s.GroupId,
                s.IsHistoricalArchive,
            })
            .ToListAsync(ct);
        var dbById = dbRows.ToDictionary(r => r.Id);

        var semesterIds = legacyIds.Concat(dbRows.Select(r => r.Id)).Distinct().ToList();
        var journals = await _db.LegacySemesterDispositionJournals.AsNoTracking()
            .Where(j => j.TenantId == tenantId
                        && j.PromptCode == LegacySemesterHistoricalDispositionCodes.PromptCode
                        && semesterIds.Contains(j.SemesterId))
            .OrderByDescending(j => j.FinalizedUtc)
            .ToListAsync(ct);
        var latestJournal = journals
            .GroupBy(j => j.SemesterId)
            .ToDictionary(g => g.Key, g => g.First().DispositionCode);

        var list = new List<LegacySemesterHistoricalDispositionCandidateDto>();

        foreach (var inv in fin.LegacySemesters.OrderBy(l => l.SemesterId))
        {
            dbById.TryGetValue(inv.SemesterId, out var row);
            var student = inv.StudentReferenceCount;
            var att = inv.AttendanceReferenceCount;
            var section = inv.SectionReferenceCount;
            var subject = inv.SubjectReferenceCount;
            var sa = inv.SubjectAllocationReferenceCount;
            var tt = inv.TimetableEntryReferenceCount;
            var tg = inv.TeachingGroupReferenceCount;
            var ops = student + att + section + sa + tt + tg;
            var groupId = row?.GroupId;
            var isHistorical = row?.IsHistoricalArchive ?? false;

            var (recommended, eligible, blocked, reason, allowed) = Classify(
                inv.SemesterId, groupId, isHistorical, ops, subject, inv);

            latestJournal.TryGetValue(inv.SemesterId, out var journalDisp);

            list.Add(new LegacySemesterHistoricalDispositionCandidateDto
            {
                SemesterId = inv.SemesterId,
                CourseId = row?.CourseId ?? inv.CourseId,
                CourseName = row?.CourseName ?? inv.CourseName ?? "",
                Number = row?.Number ?? inv.Number,
                Name = row?.Name ?? inv.Name ?? "",
                GroupId = groupId,
                IsHistoricalArchive = isHistorical,
                RecommendedDisposition = recommended,
                CurrentJournalDisposition = journalDisp ?? "",
                EligibleForHistoricalArchive = eligible,
                Blocked = blocked,
                Reason = reason,
                StudentRefs = student,
                AttendanceRefs = att,
                SectionRefs = section,
                SubjectRefs = subject,
                SubjectAllocationRefs = sa,
                TimetableEntryRefs = tt,
                TeachingGroupRefs = tg,
                OperationalRefTotal = ops,
                AllowedDispositions = allowed,
            });
        }

        return list;
    }

    private static (
        string Recommended,
        bool EligibleArchive,
        bool Blocked,
        string Reason,
        IReadOnlyList<string> Allowed)
        Classify(
            int semesterId,
            int? groupId,
            bool isHistorical,
            int ops,
            int subjectRefs,
            LegacySemesterInventoryRowDto? inv)
    {
        if (isHistorical)
        {
            return (
                LegacySemesterHistoricalDispositionCodes.HistoricalArchive,
                false,
                false,
                "Already historical archive.",
                [LegacySemesterHistoricalDispositionCodes.HistoricalArchive]);
        }

        var dispositionCode = inv?.DispositionCode ?? "";
        var isManual = string.Equals(dispositionCode, "MANUAL_MAPPING_REQUIRED", StringComparison.OrdinalIgnoreCase)
                       || semesterId == 1;
        var isDuplicate = string.Equals(dispositionCode, "DUPLICATE_REVIEW", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(dispositionCode, "DUPLICATE_LEGACY_NUMBER", StringComparison.OrdinalIgnoreCase);

        if (isManual)
        {
            return (
                LegacySemesterHistoricalDispositionCodes.ManualMappingRequired,
                false,
                true,
                "Semester requires explicit Architect/admin Group mapping; no automatic assignment; HISTORICAL_ARCHIVE deferred until decision.",
                [
                    LegacySemesterHistoricalDispositionCodes.ManualMappingRequired,
                    LegacySemesterHistoricalDispositionCodes.RetainHistoricalPendingReview,
                ]);
        }

        if (isDuplicate)
        {
            return (
                LegacySemesterHistoricalDispositionCodes.DuplicateReview,
                false,
                true,
                "Duplicate legacy Number on Course; no delete/merge/ID selection; journal DUPLICATE_REVIEW only.",
                [
                    LegacySemesterHistoricalDispositionCodes.DuplicateReview,
                    LegacySemesterHistoricalDispositionCodes.RetainHistoricalPendingReview,
                ]);
        }

        if (ops > 0)
        {
            return (
                LegacySemesterHistoricalDispositionCodes.RetainHistoricalPendingReview,
                false,
                true,
                $"Operational downstream refs remain ({ops}); remap before HISTORICAL_ARCHIVE.",
                [LegacySemesterHistoricalDispositionCodes.RetainHistoricalPendingReview]);
        }

        // Subject historical refs may remain (FK readable); ops must be zero.
        return (
            LegacySemesterHistoricalDispositionCodes.HistoricalArchive,
            true,
            false,
            subjectRefs > 0
                ? $"Ops refs cleared; {subjectRefs} Subject historical ref(s) may remain readable after archive."
                : "Ops refs cleared; eligible for HISTORICAL_ARCHIVE (GroupId remains NULL until Architect mapping if ever required).",
            [
                LegacySemesterHistoricalDispositionCodes.HistoricalArchive,
                LegacySemesterHistoricalDispositionCodes.RetainHistoricalPendingReview,
            ]);
    }

    private async Task<int> CountOperationalRefsAsync(int tenantId, int semesterId, CancellationToken ct)
    {
        var students = await _db.Students.AsNoTracking()
            .CountAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.SemesterId == semesterId, ct);
        var att = await _db.AttendanceSessions.AsNoTracking()
            .CountAsync(a => a.TenantId == tenantId && a.SemesterId == semesterId, ct);
        var sections = await _db.Sections.AsNoTracking()
            .CountAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.SemesterId == semesterId, ct);
        var sa = await _db.SchedulingSubjectAllocations.AsNoTracking()
            .CountAsync(a => a.TenantId == tenantId && !a.IsDeleted && a.SemesterId == semesterId, ct);
        var tt = await _db.SchedulingTimetableEntries.AsNoTracking()
            .CountAsync(e => e.TenantId == tenantId && !e.IsDeleted && e.SemesterId == semesterId, ct);
        var tg = await _db.SchedulingTeachingGroups.AsNoTracking()
            .CountAsync(t => t.TenantId == tenantId && !t.IsDeleted && t.SemesterId == semesterId, ct);
        return students + att + sections + sa + tt + tg;
    }

    private async Task<LegacySemesterHistoricalPostDispositionIntegrityDto> BuildPostIntegrityAsync(
        CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var findings = new List<string>();

        var historical = await _db.Semesters.AsNoTracking()
            .CountAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.IsHistoricalArchive, ct);
        var nullNonArchived = await _db.Semesters.AsNoTracking()
            .CountAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.GroupId == null && !s.IsHistoricalArchive, ct);

        // Historical archive must not have been given a guessed GroupId by this prompt.
        var historicalWithGroup = await _db.Semesters.AsNoTracking()
            .CountAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.IsHistoricalArchive && s.GroupId != null, ct);
        // Not a failure — Group-owned historical is unusual; warn only if journals claim AssignedGroupId.
        _ = historicalWithGroup;

        var crossTenant = await (
            from j in _db.LegacySemesterDispositionJournals.AsNoTracking()
            join s in _db.Semesters.AsNoTracking() on j.SemesterId equals s.Id
            where j.TenantId == tenantId
                  && j.PromptCode == LegacySemesterHistoricalDispositionCodes.PromptCode
                  && s.TenantId != tenantId
            select j.Id).CountAsync(ct);
        if (crossTenant > 0)
            findings.Add($"Cross-tenant journal/Semester mismatch count={crossTenant}.");

        var assignedGroupGuess = await _db.LegacySemesterDispositionJournals.AsNoTracking()
            .CountAsync(j => j.TenantId == tenantId
                             && j.PromptCode == LegacySemesterHistoricalDispositionCodes.PromptCode
                             && j.AssignedGroupId != null, ct);
        if (assignedGroupGuess > 0)
            findings.Add($"AssignedGroupId set on {assignedGroupGuess} P1-4-3JA journal(s); Group guessing forbidden.");

        var operationalFlagged = await _db.Semesters.AsNoTracking()
            .CountAsync(s => s.TenantId == tenantId && !s.IsDeleted
                             && s.IsHistoricalArchive && s.GroupId != null, ct);

        var passed = findings.Count == 0;
        return new LegacySemesterHistoricalPostDispositionIntegrityDto
        {
            Passed = passed,
            HistoricalArchiveCount = historical,
            NullGroupNonArchivedCount = nullNonArchived,
            OperationalWithHistoricalFlagCount = operationalFlagged,
            CrossTenantJournalViolationCount = crossTenant,
            Findings = findings,
        };
    }

    private static IReadOnlyList<LegacySemesterDependencyMatrixRowDto> BuildDependencyMatrix() =>
    [
        Row("Student", "Student.SemesterId", "Operational placement",
            canArchive: true, mustRemap: true,
            "New assignments must reject historical; existing FK may remain for audit until remapped."),
        Row("AttendanceSession", "AttendanceSession.SemesterId", "Operational attendance",
            canArchive: true, mustRemap: true,
            "New sessions must not target historical Semesters."),
        Row("Subject", "Subject.SemesterId", "Catalog (may be historical)",
            canArchive: true, mustRemap: false,
            "Historical Subject refs may remain readable after archive; Sem1 still MANUAL until Architect decision."),
        Row("SubjectAllocation", "SubjectAllocation.SemesterId", "Operational scheduling",
            canArchive: true, mustRemap: true,
            "New SA must reject historical Semesters."),
        Row("TimetableEntry", "TimetableEntry.SemesterId", "Operational scheduling",
            canArchive: true, mustRemap: true,
            "New TT entries must reject historical Semesters."),
        Row("Section", "Section.SemesterId", "Operational section scope",
            canArchive: true, mustRemap: true,
            "Must be remapped before HISTORICAL_ARCHIVE when refs remain."),
        Row("TeachingGroup", "TeachingGroup.SemesterId", "Operational TG",
            canArchive: true, mustRemap: true,
            "TG create inherits SA Semester; historical SA blocked. TG ownership implementation unchanged."),
        Row("TeachingGroupSection", "via TeachingGroup / Section", "Operational TG-section link",
            canArchive: true, mustRemap: true,
            "No TG/TGS mutation in this prompt."),
        Row("TimetableSection", "projector-owned", "Derived projection",
            canArchive: false, mustRemap: false,
            "No direct TimetableSection writes from Semester disposition."),
        Row("LegacySemesterDispositionJournal", "SemesterId", "Audit/workflow",
            canArchive: true, mustRemap: false,
            "Reused as disposition journal SSOT for P1-4-3JA."),
    ];

    private static LegacySemesterDependencyMatrixRowDto Row(
        string entity, string fk, string meaning, bool canArchive, bool mustRemap, string notes)
        => new()
        {
            Entity = entity,
            SemesterFk = fk,
            OperationalOrHistoricalMeaning = meaning,
            CanReferenceArchivedSemester = canArchive,
            MustRemapBeforeArchival = mustRemap,
            Notes = notes,
        };

    private static LegacySemesterHistoricalDispositionFindingDto Finding(
        int semesterId,
        string requested,
        string previous,
        string next,
        string result,
        bool mutated,
        int refs,
        string reason)
        => new()
        {
            SemesterId = semesterId,
            RequestedDisposition = requested,
            PreviousState = previous,
            NewState = next,
            Result = result,
            SemesterRowMutated = mutated,
            GroupIdMutated = false,
            AffectedDownstreamReferenceCount = refs,
            Reason = reason,
        };

    private static string DescribeState(Semester s)
        => s.IsHistoricalArchive
            ? "HISTORICAL_ARCHIVE"
            : s.GroupId is null
                ? "LEGACY_NULL_GROUP"
                : "OPERATIONAL_GROUP_OWNED";

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..(max - 3)] + "...";

    private static LegacySemesterHistoricalDispositionResultDto Aborted(
        int tenantId,
        string correlationId,
        string reason,
        string? concurrency,
        IReadOnlyList<LegacySemesterHistoricalDispositionFindingDto> findings)
        => new()
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = tenantId,
            IsSuccessful = false,
            ExecutionStatus = "Aborted",
            CorrelationId = correlationId,
            RolledBack = true,
            TransactionCommitted = false,
            AbortReason = reason,
            ConcurrencyResult = concurrency,
            Findings = findings,
            SchemaHardeningReady = false,
            Prompt3JAuthorized = false,
            Notes = ["Rolled back; no partial completion."],
        };
}
