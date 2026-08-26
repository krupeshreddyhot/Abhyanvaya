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
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3K-B (package 3KB) / PromptCode P1-4-3KB —
/// Archives ONLY Semesters reclassified as ARCHIVE_ELIGIBLE by the 3K-A audit classifier.
/// Reuses <c>IsHistoricalArchive</c> + <see cref="LegacySemesterDispositionJournal"/>.
/// No GroupId invention; no TG/Student/Attendance/SA/TT/Section mutation; no schema hardening.
/// Transaction model: ALL_OR_NOTHING.
/// </summary>
public sealed class HistoricalSemesterDispositionExecutionService : IHistoricalSemesterDispositionExecutionService
{
    public const string PromptCode = HistoricalSemesterDispositionExecutionCodes.PromptCode;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IHistoricalSemesterDispositionAuditService _audit;
    private readonly ILogger<HistoricalSemesterDispositionExecutionService> _logger;

    public HistoricalSemesterDispositionExecutionService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IHistoricalSemesterDispositionAuditService audit,
        ILogger<HistoricalSemesterDispositionExecutionService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    public async Task<HistoricalSemesterDispositionExecuteResultDto> ExecuteAsync(
        HistoricalSemesterDispositionExecuteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var correlationId = Guid.NewGuid().ToString("N");
        HistoricalSemesterDispositionExecuteResultDto? result = null;

        try
        {
            await _db.ExecuteInTransactionAsync(async ct =>
            {
                result = await ExecuteCoreAsync(request, correlationId, ct);
                if (result is null)
                    throw new DomainException("Historical archive execution produced no result.");
                if (!result.IsSuccessful
                    && !string.Equals(result.ExecutionStatus, "AlreadyComplete", StringComparison.Ordinal))
                    throw new DomainException(result.AbortReason ?? "Historical archive execution aborted.");
            }, cancellationToken);
        }
        catch (ConcurrencyConflictException ex)
        {
            _logger.LogWarning(ex, "P1-4-3KB historical archive concurrency conflict; rolled back.");
            return Aborted(correlationId, ex.Message, "ConcurrencyConflictException", result);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "P1-4-3KB historical archive EF concurrency conflict; rolled back.");
            return Aborted(correlationId, "Concurrency conflict while applying historical archive.",
                "DbUpdateConcurrencyException", result);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "P1-4-3KB historical archive aborted and rolled back.");
            return Aborted(correlationId, ex.Message, null, result);
        }

        return new HistoricalSemesterDispositionExecuteResultDto
        {
            GeneratedUtc = result!.GeneratedUtc,
            TenantId = result.TenantId,
            PromptCode = result.PromptCode,
            Disposition = result.Disposition,
            CorrelationId = result.CorrelationId,
            IsSuccessful = result.IsSuccessful,
            ExecutionStatus = result.ExecutionStatus,
            RolledBack = false,
            TransactionCommitted = true,
            TransactionModel = result.TransactionModel,
            AbortReason = result.AbortReason,
            ConcurrencyResult = result.ConcurrencyResult,
            Requested = result.Requested,
            Archived = result.Archived,
            AlreadyComplete = result.AlreadyComplete,
            Rejected = result.Rejected,
            Blocked = result.Blocked,
            Results = result.Results,
            Notes = result.Notes,
            SchemaHardeningDeferred = true,
            GroupIdInvented = false,
            DownstreamEntitiesMutated = false,
        };
    }

    private async Task<HistoricalSemesterDispositionExecuteResultDto> ExecuteCoreAsync(
        HistoricalSemesterDispositionExecuteRequest request,
        string correlationId,
        CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var notes = new List<string>
        {
            "Prompt 3K-B CONTROLLED HISTORICAL_ARCHIVE — ARCHIVE_ELIGIBLE only.",
            $"PromptCode={PromptCode}. Reuses IsHistoricalArchive + LegacySemesterDispositionJournals.",
            "ALL_OR_NOTHING transaction; no GroupId invention; no downstream entity mutation.",
            $"CorrelationId={correlationId}; ActorUserId={_currentUser.UserId}.",
        };

        var disposition = (request.Disposition ?? "").Trim();
        if (!string.Equals(disposition, HistoricalSemesterDispositionExecutionCodes.HistoricalArchive,
                StringComparison.OrdinalIgnoreCase))
        {
            return Fail(tenantId, correlationId, 0,
                "Only disposition HISTORICAL_ARCHIVE is supported by Prompt 3K-B.", notes);
        }

        var ids = (request.SemesterIds ?? []).Distinct().OrderBy(x => x).ToList();
        if (ids.Count == 0)
        {
            return Fail(tenantId, correlationId, 0,
                "Explicit semesterIds are required (no archive-all).", notes);
        }

        if ((request.SemesterIds?.Count ?? 0) != ids.Count)
        {
            return Fail(tenantId, correlationId, request.SemesterIds?.Count ?? 0,
                "Duplicate SemesterId in request; fail closed.", notes);
        }

        // Server-authoritative reclassification (3K-A audit).
        var audit = await _audit.BuildAuditAsync(ct);
        var byId = audit.Items.ToDictionary(i => i.SemesterId);
        var results = new List<HistoricalSemesterDispositionExecuteItemResultDto>();
        var pendingArchive = new List<(Semester Semester, HistoricalSemesterDispositionDto Classified)>();
        var already = 0;
        var rejected = 0;

        // Pass 1: classify only — never mutate until the full batch is eligible (ALL_OR_NOTHING).
        foreach (var semesterId in ids)
        {
            var semester = await _db.Semesters
                .FirstOrDefaultAsync(s => s.Id == semesterId && s.TenantId == tenantId && !s.IsDeleted, ct);

            if (semester is null)
            {
                rejected++;
                results.Add(Item(semesterId, "Rejected", "", null, null, false, false, false,
                    "Semester not found for tenant (fail closed / cross-tenant blocked)."));
                continue;
            }

            var groupBefore = semester.GroupId;

            if (semester.IsHistoricalArchive)
            {
                already++;
                results.Add(Item(semesterId, "AlreadyComplete",
                    HistoricalSemesterDispositionClassifications.Archived,
                    groupBefore, semester.GroupId, true, false, false,
                    "Already archived; zero additional writes."));
                continue;
            }

            if (!byId.TryGetValue(semesterId, out var classified))
            {
                rejected++;
                results.Add(Item(semesterId, "Rejected", "UNKNOWN", groupBefore, groupBefore, false, false, false,
                    "Semester not present in historical disposition audit inventory; fail closed."));
                continue;
            }

            if (!string.Equals(classified.Classification,
                    HistoricalSemesterDispositionClassifications.ArchiveEligible, StringComparison.Ordinal))
            {
                rejected++;
                results.Add(Item(semesterId, "Rejected", classified.Classification,
                    groupBefore, groupBefore, false, false, false,
                    $"Classification is {classified.Classification}; only ARCHIVE_ELIGIBLE may be archived. "
                    + classified.RecommendedAction));
                continue;
            }

            var liveOps = await CountOperationalRefsAsync(tenantId, semesterId, ct);
            if (liveOps > 0)
            {
                rejected++;
                results.Add(Item(semesterId, "Blocked",
                    HistoricalSemesterDispositionClassifications.BlockedByReference,
                    groupBefore, groupBefore, false, false, false,
                    $"Operational downstream refs appeared ({liveOps}); fail closed."));
                continue;
            }

            pendingArchive.Add((semester, classified));
        }

        if (rejected > 0)
        {
            notes.Add($"Rejected={rejected}; ALL_OR_NOTHING abort (zero mutations; no SaveChanges).");
            return new HistoricalSemesterDispositionExecuteResultDto
            {
                GeneratedUtc = DateTime.UtcNow,
                TenantId = tenantId,
                PromptCode = PromptCode,
                Disposition = HistoricalSemesterDispositionExecutionCodes.HistoricalArchive,
                CorrelationId = correlationId,
                IsSuccessful = false,
                ExecutionStatus = "Aborted",
                AbortReason =
                    $"One or more Semesters rejected/blocked ({rejected}); entire batch rolled back (ALL_OR_NOTHING).",
                TransactionModel = "ALL_OR_NOTHING",
                Requested = ids.Count,
                Archived = 0,
                AlreadyComplete = already,
                Rejected = rejected,
                Blocked = rejected,
                Results = results,
                Notes = notes,
                SchemaHardeningDeferred = true,
            };
        }

        if (pendingArchive.Count == 0 && already > 0)
        {
            notes.Add($"AlreadyComplete={already}; zero writes.");
            return new HistoricalSemesterDispositionExecuteResultDto
            {
                GeneratedUtc = DateTime.UtcNow,
                TenantId = tenantId,
                PromptCode = PromptCode,
                Disposition = HistoricalSemesterDispositionExecutionCodes.HistoricalArchive,
                CorrelationId = correlationId,
                IsSuccessful = true,
                ExecutionStatus = "AlreadyComplete",
                TransactionModel = "ALL_OR_NOTHING",
                Requested = ids.Count,
                Archived = 0,
                AlreadyComplete = already,
                Rejected = 0,
                Blocked = 0,
                Results = results,
                Notes = notes,
                SchemaHardeningDeferred = true,
            };
        }

        if (pendingArchive.Count == 0)
        {
            return Fail(tenantId, correlationId, ids.Count, "Nothing to archive.", notes, results);
        }

        // Pass 2: mutate only after full-batch eligibility confirmed.
        var archived = 0;
        foreach (var (semester, classified) in pendingArchive)
        {
            var groupBefore = semester.GroupId;
            semester.IsHistoricalArchive = true;
            semester.UpdatedDate = DateTime.UtcNow;
            semester.UpdatedBy = _currentUser.UserId;
            // GroupId intentionally untouched (may remain NULL).

            var evidence =
                $"corr={correlationId}; prevClassification=ARCHIVE_ELIGIBLE; " +
                $"reason={(request.Reason ?? classified.RecommendedAction)}; " +
                $"opsRefs=0; subjectHints={classified.DownstreamReferenceSummary.SubjectRefs}; " +
                $"groupId={semester.GroupId?.ToString() ?? "NULL"}; " +
                "noGroupGuess=true; noDelete=true; noDownstreamMutation=true; source=P1-4-3KB";

            await _db.AddAsync(new LegacySemesterDispositionJournal
            {
                TenantId = tenantId,
                SemesterId = semester.Id,
                DispositionCode = HistoricalSemesterDispositionExecutionCodes.HistoricalArchive,
                Evidence = Truncate(evidence, 2000),
                PromptCode = PromptCode,
                AssignedGroupId = null,
                SemesterRowMutated = true,
                FinalizedUtc = DateTime.UtcNow,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId,
            });

            archived++;
            results.Add(Item(semester.Id, "Archived",
                HistoricalSemesterDispositionClassifications.Archived,
                groupBefore, semester.GroupId, true, true, true,
                "IsHistoricalArchive set; GroupId unchanged; journal written (P1-4-3KB)."));
        }

        await ConcurrencyExceptionHelper.SaveChangesAsync(_db, ct);

        notes.Add($"Archived={archived}; AlreadyComplete={already}; SchemaHardeningDeferred=TRUE.");
        return new HistoricalSemesterDispositionExecuteResultDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = tenantId,
            PromptCode = PromptCode,
            Disposition = HistoricalSemesterDispositionExecutionCodes.HistoricalArchive,
            CorrelationId = correlationId,
            IsSuccessful = true,
            ExecutionStatus = "Completed",
            TransactionModel = "ALL_OR_NOTHING",
            Requested = ids.Count,
            Archived = archived,
            AlreadyComplete = already,
            Rejected = 0,
            Blocked = 0,
            Results = results,
            Notes = notes,
            SchemaHardeningDeferred = true,
            GroupIdInvented = false,
            DownstreamEntitiesMutated = false,
        };
    }

    private async Task<int> CountOperationalRefsAsync(int tenantId, int semesterId, CancellationToken ct)
    {
        var students = await _db.Students.AsNoTracking()
            .CountAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.SemesterId == semesterId, ct);
        var attendance = await _db.AttendanceSessions.AsNoTracking()
            .CountAsync(a => a.TenantId == tenantId && a.SemesterId == semesterId, ct);
        var sections = await _db.Sections.AsNoTracking()
            .CountAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.SemesterId == semesterId, ct);
        var sa = await _db.SchedulingSubjectAllocations.AsNoTracking()
            .CountAsync(a => a.TenantId == tenantId && !a.IsDeleted && a.SemesterId == semesterId, ct);
        var tt = await _db.SchedulingTimetableEntries.AsNoTracking()
            .CountAsync(e => e.TenantId == tenantId && !e.IsDeleted && e.SemesterId == semesterId, ct);
        var tg = await _db.SchedulingTeachingGroups.AsNoTracking()
            .CountAsync(t => t.TenantId == tenantId && !t.IsDeleted && t.SemesterId == semesterId, ct);
        return students + attendance + sections + sa + tt + tg;
    }

    private HistoricalSemesterDispositionExecuteResultDto Aborted(
        string correlationId,
        string reason,
        string? concurrency,
        HistoricalSemesterDispositionExecuteResultDto? partial)
        => new()
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = _currentUser.TenantId,
            PromptCode = PromptCode,
            Disposition = HistoricalSemesterDispositionExecutionCodes.HistoricalArchive,
            CorrelationId = correlationId,
            IsSuccessful = false,
            ExecutionStatus = "Aborted",
            RolledBack = true,
            TransactionCommitted = false,
            TransactionModel = "ALL_OR_NOTHING",
            AbortReason = reason,
            ConcurrencyResult = concurrency,
            Requested = partial?.Requested ?? 0,
            Archived = 0,
            AlreadyComplete = partial?.AlreadyComplete ?? 0,
            Rejected = partial?.Rejected ?? 0,
            Blocked = partial?.Blocked ?? 0,
            Results = partial?.Results ?? [],
            Notes = (partial?.Notes ?? []).Concat(["Rolled back."]).ToList(),
            SchemaHardeningDeferred = true,
        };

    private static HistoricalSemesterDispositionExecuteResultDto Fail(
        int tenantId,
        string correlationId,
        int requested,
        string reason,
        List<string> notes,
        IReadOnlyList<HistoricalSemesterDispositionExecuteItemResultDto>? results = null)
        => new()
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = tenantId,
            PromptCode = PromptCode,
            Disposition = HistoricalSemesterDispositionExecutionCodes.HistoricalArchive,
            CorrelationId = correlationId,
            IsSuccessful = false,
            ExecutionStatus = "Aborted",
            AbortReason = reason,
            TransactionModel = "ALL_OR_NOTHING",
            Requested = requested,
            Results = results ?? [],
            Notes = notes,
            SchemaHardeningDeferred = true,
        };

    private static HistoricalSemesterDispositionExecuteItemResultDto Item(
        int semesterId,
        string result,
        string classification,
        int? groupBefore,
        int? groupAfter,
        bool archivedAfter,
        bool mutated,
        bool journal,
        string reason)
        => new()
        {
            SemesterId = semesterId,
            Result = result,
            Classification = classification,
            GroupIdBefore = groupBefore,
            GroupIdAfter = groupAfter,
            IsHistoricalArchiveAfter = archivedAfter,
            SemesterRowMutated = mutated,
            JournalWritten = journal,
            Reason = reason,
        };

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];
}
