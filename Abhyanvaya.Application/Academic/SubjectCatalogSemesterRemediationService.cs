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
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3J —
/// Fail-closed Subject.SemesterId remediation: legacy NULL-group → deterministic Group-specific Semester.
/// Does not mutate TeachingGroup, SA, TT, TimetableSection, Semester ownership, or Sections.
/// </summary>
public sealed class SubjectCatalogSemesterRemediationService : ISubjectCatalogSemesterRemediationService
{
    public const string PromptCode = "P1-4-3J";
    public const string JournalDispositionCode = "SUBJECT_CATALOG_SEMESTER_REMAP";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<SubjectCatalogSemesterRemediationService> _logger;

    public SubjectCatalogSemesterRemediationService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ILogger<SubjectCatalogSemesterRemediationService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    public Task<SubjectCatalogRemediationResultDto> PreviewAsync(CancellationToken cancellationToken = default)
        => BuildAsync(mutate: false, cancellationToken);

    public async Task<SubjectCatalogRemediationResultDto> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        SubjectCatalogRemediationResultDto? result = null;
        try
        {
            await _db.ExecuteInTransactionAsync(async ct =>
            {
                result = await BuildAsync(mutate: true, ct);
                if (!string.Equals(result.ExecutionStatus, "Completed", StringComparison.Ordinal)
                    && !string.Equals(result.ExecutionStatus, "AlreadyComplete", StringComparison.Ordinal))
                {
                    throw new DomainException(result.AbortReason ?? "Subject Catalog remediation aborted.");
                }
            }, cancellationToken);
        }
        catch (ConcurrencyConflictException ex)
        {
            _logger.LogWarning(ex, "P1-4 Prompt 3J concurrency conflict.");
            return Aborted(result, ex.Message, "ConcurrencyConflictException");
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "P1-4 Prompt 3J EF concurrency conflict.");
            return Aborted(result, "Concurrency conflict while remediating Subjects.", "DbUpdateConcurrencyException");
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "P1-4 Prompt 3J Subject Catalog remediation aborted and rolled back.");
            return Aborted(result, ex.Message, result?.ConcurrencyResult);
        }

        return result ?? new SubjectCatalogRemediationResultDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = _currentUser.TenantId,
            IsReadOnly = false,
            ExecutionStatus = "Aborted",
            RolledBack = true,
            AbortReason = "Remediation produced no result.",
            CorrelationId = Guid.NewGuid().ToString("N"),
        };
    }

    private SubjectCatalogRemediationResultDto Aborted(
        SubjectCatalogRemediationResultDto? result,
        string reason,
        string? concurrency)
        => new()
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = _currentUser.TenantId,
            IsReadOnly = false,
            ExecutionStatus = "Aborted",
            RolledBack = true,
            ExecutionSafe = false,
            ChangedCount = 0,
            AlreadyCompleteCount = result?.AlreadyCompleteCount ?? 0,
            SafeToRemapCount = result?.SafeToRemapCount ?? 0,
            ManualMappingCount = result?.ManualMappingCount ?? 0,
            BlockedCount = result?.BlockedCount ?? 0,
            HistoricalRetainCount = result?.HistoricalRetainCount ?? 0,
            AlreadyCorrectCount = result?.AlreadyCorrectCount ?? 0,
            Items = result?.Items ?? [],
            Notes = result?.Notes ?? [],
            AbortReason = reason,
            ConcurrencyResult = concurrency ?? "None",
            TransactionCommitted = false,
            CorrelationId = result?.CorrelationId ?? Guid.NewGuid().ToString("N"),
        };

    private async Task<SubjectCatalogRemediationResultDto> BuildAsync(bool mutate, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var correlationId = Guid.NewGuid().ToString("N");
        var notes = new List<string>
        {
            mutate
                ? "Execution mode: Subject.SemesterId only for SAFE_TO_REMAP deterministic mappings."
                : "Read-only preview; zero writes.",
            "Target resolution: Subject.GroupId + CourseId + Legacy.Number → unique Group-specific Semester.",
            "No name matching. No Semester.GroupId assignment. No TG/SA/TT/TimetableSection mutation.",
            $"CorrelationId={correlationId}.",
        };

        var semesters = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Select(s => new { s.Id, s.CourseId, s.GroupId, s.Number })
            .ToListAsync(ct);

        var semesterById = semesters.ToDictionary(s => s.Id);
        var nullGroupIds = semesters.Where(s => s.GroupId is null).Select(s => s.Id).ToHashSet();

        var subjects = mutate
            ? await _db.Subjects
                .Where(s => s.TenantId == tenantId && !s.IsDeleted)
                .OrderBy(s => s.Id).ToListAsync(ct)
            : await _db.Subjects.AsNoTracking()
                .Where(s => s.TenantId == tenantId && !s.IsDeleted)
                .OrderBy(s => s.Id).ToListAsync(ct);

        var journaled = await ResolveJournaledAsync(tenantId, ct);

        var tgBySubject = await _db.SchedulingTeachingGroups.AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted)
            .Select(t => new { t.Id, t.SubjectId, t.SemesterId })
            .ToListAsync(ct);
        var tgIdsBySubject = tgBySubject.GroupBy(t => t.SubjectId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(x => x.Id).OrderBy(x => x).ToList());
        var tgSemById = tgBySubject.ToDictionary(t => t.Id, t => t.SemesterId);
        var tgSemesterSnapshot = tgSemById.ToDictionary(x => x.Key, x => x.Value);

        var saCounts = await _db.SchedulingSubjectAllocations.AsNoTracking()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted)
            .GroupBy(a => a.SubjectId)
            .Select(g => new { SubjectId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SubjectId, x => x.Count, ct);

        // Subjects sharing TenantSubjectId+Course+Group on a target semester (duplicate risk)
        var subjectsByKey = subjects
            .GroupBy(s => (s.TenantSubjectId, s.CourseId, s.GroupId, s.SemesterId))
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        var items = new List<SubjectCatalogRemediationItemDto>();
        var pending = new List<(Subject Subject, int TargetSemesterId, int OldSemesterId)>();
        var safe = 0;
        var manual = 0;
        var blocked = 0;
        var historical = 0;
        var alreadyCorrect = 0;
        var alreadyComplete = 0;

        foreach (var subject in subjects)
        {
            if (!semesterById.TryGetValue(subject.SemesterId, out var currentSem))
            {
                items.Add(Item(subject, null, null, [], SubjectCatalogRemediationStatus.Blocked,
                    "Current Semester missing; fail closed.", false, tgIdsBySubject, saCounts));
                blocked++;
                continue;
            }

            var isNullGroup = currentSem.GroupId is null;
            var tgIds = tgIdsBySubject.GetValueOrDefault(subject.Id) ?? Array.Empty<int>();

            if (!isNullGroup)
            {
                if (currentSem.GroupId == subject.GroupId && currentSem.CourseId == subject.CourseId)
                {
                    var wasRemapped = journaled.Contains(subject.Id);
                    var kind = wasRemapped
                        ? SubjectCatalogRemediationStatus.AlreadyComplete
                        : SubjectCatalogRemediationStatus.AlreadyCorrect;
                    items.Add(Item(subject, currentSem.Number, currentSem.Id, [currentSem.Id], kind,
                        wasRemapped ? "Already on Group-specific Semester (journaled remapping)." : "Already Course/Group aligned with Semester.",
                        false, tgIdsBySubject, saCounts, isNullGroup: false));
                    if (kind == SubjectCatalogRemediationStatus.AlreadyComplete)
                        alreadyComplete++;
                    else
                        alreadyCorrect++;
                }
                else
                {
                    items.Add(Item(subject, currentSem.Number, null, [], SubjectCatalogRemediationStatus.Blocked,
                        $"Group-specific Semester Course/Group mismatch (Sem Group={currentSem.GroupId}, Course={currentSem.CourseId}).",
                        false, tgIdsBySubject, saCounts, isNullGroup: false));
                    blocked++;
                }

                continue;
            }

            // Legacy NULL-group — resolve deterministic target
            if (subject.GroupId <= 0)
            {
                items.Add(Item(subject, currentSem.Number, null, [], SubjectCatalogRemediationStatus.Blocked,
                    "Subject.GroupId missing; cannot resolve target.", false, tgIdsBySubject, saCounts));
                blocked++;
                continue;
            }

            var candidates = semesters
                .Where(s => s.GroupId == subject.GroupId
                            && s.CourseId == subject.CourseId
                            && s.Number == currentSem.Number)
                .Select(s => s.Id)
                .OrderBy(x => x)
                .ToList();

            if (candidates.Count == 0)
            {
                // No operational target — historical retain if no SA/TG pressure, else blocked
                var saCount = saCounts.GetValueOrDefault(subject.Id);
                if (saCount == 0 && tgIds.Count == 0)
                {
                    items.Add(Item(subject, currentSem.Number, null, [], SubjectCatalogRemediationStatus.HistoricalRetain,
                        $"No Group-specific Semester for GroupId={subject.GroupId} Number={currentSem.Number}; HISTORICAL_RETAIN (no SA/TG).",
                        false, tgIdsBySubject, saCounts));
                    historical++;
                }
                else
                {
                    items.Add(Item(subject, currentSem.Number, null, [], SubjectCatalogRemediationStatus.Blocked,
                        $"No Group-specific Semester for GroupId={subject.GroupId} Number={currentSem.Number}; SA/TG refs present.",
                        false, tgIdsBySubject, saCounts));
                    blocked++;
                }

                continue;
            }

            if (candidates.Count > 1)
            {
                items.Add(Item(subject, currentSem.Number, null, candidates, SubjectCatalogRemediationStatus.ManualMappingRequired,
                    $"Multiple target Semesters Ids=[{string.Join(",", candidates)}] for GroupId={subject.GroupId} Number={currentSem.Number}; do not guess.",
                    false, tgIdsBySubject, saCounts));
                manual++;
                continue;
            }

            var targetId = candidates[0];
            var target = semesterById[targetId];

            // Cross-tenant already filtered by query; double-check ownership
            if (target.CourseId != subject.CourseId || target.GroupId != subject.GroupId)
            {
                items.Add(Item(subject, currentSem.Number, null, candidates, SubjectCatalogRemediationStatus.Blocked,
                    "Target Course/Group ownership mismatch after resolution.", false, tgIdsBySubject, saCounts));
                blocked++;
                continue;
            }

            // Duplicate catalog key risk on target
            var dupKey = (subject.TenantSubjectId, subject.CourseId, subject.GroupId, targetId);
            if (subjectsByKey.TryGetValue(dupKey, out var existingIds)
                && existingIds.Any(id => id != subject.Id))
            {
                items.Add(Item(subject, currentSem.Number, targetId, candidates, SubjectCatalogRemediationStatus.Blocked,
                    $"Duplicate Subject TenantSubjectId={subject.TenantSubjectId} already exists on target Sem {targetId}.",
                    false, tgIdsBySubject, saCounts));
                blocked++;
                continue;
            }

            // TG dependency: report + block if TG Sem != target (would create catalog inconsistency)
            var tgBlockReasons = new List<string>();
            foreach (var tgId in tgIds)
            {
                if (tgSemById.TryGetValue(tgId, out var tgSem) && tgSem != targetId)
                {
                    tgBlockReasons.Add(
                        $"TeachingGroup Id={tgId} SemesterId={tgSem} != target {targetId}; TG frozen — BLOCK (do not mutate TG).");
                }
            }

            if (tgBlockReasons.Count > 0)
            {
                items.Add(Item(subject, currentSem.Number, targetId, candidates, SubjectCatalogRemediationStatus.Blocked,
                    string.Join(" ", tgBlockReasons), false, tgIdsBySubject, saCounts));
                blocked++;
                continue;
            }

            items.Add(Item(subject, currentSem.Number, targetId, candidates, SubjectCatalogRemediationStatus.SafeToRemap,
                $"Deterministic remap Sem {subject.SemesterId} → {targetId} (GroupId={subject.GroupId}, Number={currentSem.Number}).",
                true, tgIdsBySubject, saCounts));
            safe++;
            if (mutate)
                pending.Add((subject, targetId, subject.SemesterId));
        }

        items = items.OrderBy(i => i.SubjectId).ToList();

        // Execute path: only SAFE_TO_REMAP; if any SAFE was reclassified unsafe mid-flight we already counted.
        // Fail closed if mutate requested but manual/blocked exist among NULL-group subjects that could have been remapped?
        // Prompt: execute approved deterministic set. Manual/Blocked stay unchanged — do not abort entire batch for MANUAL on other subjects.
        // Only abort if a pending SAFE fails revalidation or transaction issue.

        var changed = 0;
        var affected = new List<int>();

        if (mutate && pending.Count > 0)
        {
            foreach (var (subject, targetId, oldId) in pending)
            {
                if (subject.TenantId != tenantId)
                    throw new DomainException($"Cross-tenant Subject Id={subject.Id}; abort.");
                if (subject.SemesterId != oldId)
                    throw new DomainException($"Subject Id={subject.Id} Semester changed since validation; abort.");
                if (!semesterById.TryGetValue(targetId, out var target)
                    || target.GroupId != subject.GroupId
                    || target.CourseId != subject.CourseId)
                {
                    throw new DomainException($"Subject Id={subject.Id} target Sem {targetId} invalid; abort.");
                }

                subject.SemesterId = targetId;
                subject.UpdatedDate = DateTime.UtcNow;
                subject.UpdatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null;
                changed++;
                affected.Add(subject.Id);
            }

            await _db.AddAsync(new LegacySemesterDispositionJournal
            {
                TenantId = tenantId,
                SemesterId = pending[0].TargetSemesterId,
                DispositionCode = JournalDispositionCode,
                Evidence =
                    $"SubjectIds=[{string.Join(",", affected)}]; pairs=[{string.Join(";", pending.Select(p => $"{p.OldSemesterId}->{p.TargetSemesterId}"))}]; correlation={correlationId}; actor={_currentUser.UserId}",
                PromptCode = PromptCode,
                AssignedGroupId = null,
                SemesterRowMutated = false,
                FinalizedUtc = DateTime.UtcNow,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
            });

            await ConcurrencyExceptionHelper.SaveChangesAsync(_db, ct);

            // Immutability: TG Semesters unchanged
            var tgAfter = await _db.SchedulingTeachingGroups.AsNoTracking()
                .Where(t => tgSemesterSnapshot.Keys.Contains(t.Id))
                .Select(t => new { t.Id, t.SemesterId })
                .ToListAsync(ct);
            foreach (var t in tgAfter)
            {
                if (tgSemesterSnapshot[t.Id] != t.SemesterId)
                    throw new DomainException($"TeachingGroup Id={t.Id} SemesterId mutated; forbidden in Prompt 3J.");
            }
        }

        // AlreadyComplete when no SAFE left and journal covers remapped set / all null-group cleared for remappable
        var remainingLegacy = items.Count(i =>
            i.CurrentSemesterIsNullGroup
            && i.StatusKind is SubjectCatalogRemediationStatus.SafeToRemap);

        if (mutate && changed == 0 && remainingLegacy == 0
            && (alreadyComplete > 0 || journaled.Count > 0 || safe == 0))
        {
            if (safe == 0 && items.All(i =>
                    i.StatusKind is SubjectCatalogRemediationStatus.AlreadyCorrect
                        or SubjectCatalogRemediationStatus.AlreadyComplete
                        or SubjectCatalogRemediationStatus.HistoricalRetain
                        or SubjectCatalogRemediationStatus.ManualMappingRequired
                        or SubjectCatalogRemediationStatus.Blocked))
            {
                // If there were remappable and they're done via journal:
                alreadyComplete = Math.Max(alreadyComplete, journaled.Count);
            }
        }

        notes.Add(
            $"Items={items.Count}; Safe={safe}; Changed={changed}; AlreadyCorrect={alreadyCorrect}; AlreadyComplete={alreadyComplete}; Manual={manual}; Blocked={blocked}; Historical={historical}.");

        string status;
        if (!mutate)
            status = "NotExecuted";
        else if (changed > 0)
            status = "Completed";
        else if (safe == 0 && remainingLegacy == 0)
            status = "AlreadyComplete";
        else
            status = "Aborted";

        if (mutate && status == "Aborted" && string.IsNullOrEmpty(notes.FirstOrDefault(n => n.StartsWith("ABORT", StringComparison.Ordinal))))
        {
            // Should not reach with pending empty and safe>0 unless drift
        }

        return new SubjectCatalogRemediationResultDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = tenantId,
            IsReadOnly = !mutate,
            ExecutionStatus = status,
            RolledBack = false,
            ExecutionSafe = !mutate || pending.Count == 0 || changed > 0 || status == "AlreadyComplete",
            ChangedCount = changed,
            AlreadyCompleteCount = alreadyComplete,
            SafeToRemapCount = safe,
            ManualMappingCount = manual,
            BlockedCount = blocked,
            HistoricalRetainCount = historical,
            AlreadyCorrectCount = alreadyCorrect,
            AffectedSubjectIds = affected,
            Items = items,
            Notes = notes,
            ConcurrencyResult = "None",
            TransactionCommitted = mutate && (changed > 0 || status == "AlreadyComplete"),
            CorrelationId = correlationId,
            AbortReason = status == "Aborted" ? "No durable Subject remaps applied." : null,
        };
    }

    private SubjectCatalogRemediationItemDto Item(
        Subject subject,
        int? currentNumber,
        int? targetId,
        IReadOnlyList<int> candidates,
        SubjectCatalogRemediationStatus kind,
        string reason,
        bool mutation,
        Dictionary<int, IReadOnlyList<int>> tgBySubject,
        Dictionary<int, int> saCounts,
        bool isNullGroup = true)
        => new()
        {
            SubjectId = subject.Id,
            TenantSubjectId = subject.TenantSubjectId,
            TenantId = subject.TenantId,
            CourseId = subject.CourseId,
            GroupId = subject.GroupId,
            CurrentSemesterId = subject.SemesterId,
            CurrentSemesterNumber = currentNumber,
            CurrentSemesterIsNullGroup = isNullGroup,
            TargetSemesterId = targetId,
            TargetSemesterNumber = currentNumber,
            CandidateTargetSemesterIds = candidates,
            StatusKind = kind,
            StatusCode = kind switch
            {
                SubjectCatalogRemediationStatus.AlreadyCorrect => "ALREADY_CORRECT",
                SubjectCatalogRemediationStatus.SafeToRemap => "SAFE_TO_REMAP",
                SubjectCatalogRemediationStatus.ManualMappingRequired => "MANUAL_MAPPING_REQUIRED",
                SubjectCatalogRemediationStatus.Blocked => "BLOCKED",
                SubjectCatalogRemediationStatus.HistoricalRetain => "HISTORICAL_RETAIN",
                _ => "ALREADY_COMPLETE",
            },
            Reason = reason,
            MutationAllowed = mutation,
            ReferencingTeachingGroupIds = tgBySubject.GetValueOrDefault(subject.Id) ?? [],
            SubjectAllocationCount = saCounts.GetValueOrDefault(subject.Id),
        };

    private async Task<HashSet<int>> ResolveJournaledAsync(int tenantId, CancellationToken ct)
    {
        var evidence = await _db.LegacySemesterDispositionJournals.AsNoTracking()
            .Where(j => j.TenantId == tenantId
                        && j.PromptCode == PromptCode
                        && j.DispositionCode == JournalDispositionCode)
            .Select(j => j.Evidence)
            .ToListAsync(ct);

        var ids = new HashSet<int>();
        foreach (var row in evidence)
        {
            if (string.IsNullOrWhiteSpace(row))
                continue;
            var start = row.IndexOf("SubjectIds=[", StringComparison.Ordinal);
            if (start < 0)
                continue;
            start += "SubjectIds=[".Length;
            var end = row.IndexOf(']', start);
            if (end < 0)
                continue;
            foreach (var part in row[start..end].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, out var id) && id > 0)
                    ids.Add(id);
            }
        }

        return ids;
    }
}
