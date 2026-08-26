using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3C —
/// Fail-closed, idempotent remediation of AttendanceSession / SubjectAllocation / TimetableEntry
/// referencing legacy NULL-group Semester III. TeachingGroup is identify-only.
/// </summary>
public sealed class LegacySemesterDownstreamRemediationService : ILegacySemesterDownstreamRemediationService
{
    public const int ExpectedLegacyNumber = 3;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISemesterPostMigrationIntegrityAuditService _integrityAudit;
    private readonly ILogger<LegacySemesterDownstreamRemediationService> _logger;

    public LegacySemesterDownstreamRemediationService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ISemesterPostMigrationIntegrityAuditService integrityAudit,
        ILogger<LegacySemesterDownstreamRemediationService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _integrityAudit = integrityAudit;
        _logger = logger;
    }

    public Task<DownstreamRemediationReportDto> AuditAsync(CancellationToken cancellationToken = default)
        => BuildPlanAsync(mutate: false, cancellationToken);

    public Task<DownstreamRemediationReportDto> PreviewAsync(CancellationToken cancellationToken = default)
        => BuildPlanAsync(mutate: false, cancellationToken);

    public async Task<DownstreamRemediationReportDto> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        DownstreamRemediationReportDto? result = null;
        try
        {
            await _db.ExecuteInTransactionAsync(async ct =>
            {
                result = await BuildPlanAsync(mutate: true, ct);
                if (!string.Equals(result.ExecutionStatus, "Completed", StringComparison.Ordinal)
                    && !string.Equals(result.ExecutionStatus, "AlreadyComplete", StringComparison.Ordinal))
                {
                    throw new DomainException(result.AbortReason ?? "Downstream remediation aborted.");
                }
            }, cancellationToken);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "P1-4 Prompt 3C downstream remediation aborted and rolled back.");
            return new DownstreamRemediationReportDto
            {
                GeneratedUtc = DateTime.UtcNow,
                TenantId = _currentUser.TenantId,
                IsReadOnly = false,
                LegacySemesterId = result?.LegacySemesterId,
                LegacySemesterNumber = ExpectedLegacyNumber,
                Summary = result?.Summary ?? new DownstreamRemediationSummaryDto(),
                Items = result?.Items ?? [],
                Notes = result?.Notes ?? [],
                AbortReason = ex.Message,
                RolledBack = true,
                ExecutionStatus = "Aborted",
            };
        }

        if (result is null)
        {
            return new DownstreamRemediationReportDto
            {
                GeneratedUtc = DateTime.UtcNow,
                TenantId = _currentUser.TenantId,
                IsReadOnly = false,
                LegacySemesterNumber = ExpectedLegacyNumber,
                AbortReason = "Remediation produced no result.",
                RolledBack = true,
                ExecutionStatus = "Aborted",
            };
        }

        var post = await _integrityAudit.BuildAuditAsync(cancellationToken);
        var notes = result.Notes.ToList();
        notes.Add(
            $"Post-integrity IsHealthy={post.IsHealthy}; Critical={post.Summary.Critical}; Errors={post.Summary.Errors}; Warnings={post.Summary.Warnings}.");

        return new DownstreamRemediationReportDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = result.TenantId,
            IsReadOnly = false,
            LegacySemesterId = result.LegacySemesterId,
            LegacySemesterNumber = result.LegacySemesterNumber,
            Summary = result.Summary,
            Items = result.Items,
            Notes = notes,
            AbortReason = result.AbortReason,
            RolledBack = false,
            ExecutionStatus = result.ExecutionStatus,
            PostIntegrityAudit = post,
        };
    }

    private async Task<DownstreamRemediationReportDto> BuildPlanAsync(bool mutate, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var notes = new List<string>
        {
            mutate ? "Execution mode: mutate approved entity types only." : "Read-only preview/audit; no mutations.",
            "TeachingGroup = DEFERRED / IDENTIFY-ONLY (frozen architecture).",
        };

        var legacyCandidates = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.Number == ExpectedLegacyNumber && s.GroupId == null)
            .Select(s => new TargetSemester(s.Id, s.TenantId, s.CourseId, -1, s.Number))
            .ToListAsync(ct);

        if (legacyCandidates.Count == 0)
            return Abort(tenantId, "No legacy NULL-group Semester Number=3 found for tenant.", notes, mutate);

        if (legacyCandidates.Count > 1)
        {
            return Abort(
                tenantId,
                $"Multiple legacy NULL-group Semester Number=3 found ({legacyCandidates.Count}); fail closed.",
                notes,
                mutate);
        }

        var legacy = legacyCandidates[0];
        var legacyId = legacy.Id;

        var targets = await _db.Semesters.AsNoTracking()
            .Where(s =>
                s.TenantId == tenantId
                && !s.IsDeleted
                && s.CourseId == legacy.CourseId
                && s.Number == ExpectedLegacyNumber
                && s.GroupId != null)
            .Select(s => new TargetSemester(s.Id, s.TenantId, s.CourseId, s.GroupId!.Value, s.Number))
            .ToListAsync(ct);

        var targetsByGroup = targets
            .GroupBy(t => t.GroupId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var items = new List<DownstreamRemediationItemDto>();
        var mutateCount = 0;
        var skippedManual = 0;

        var attendance = mutate
            ? await _db.AttendanceSessions.Where(a => a.TenantId == tenantId && a.SemesterId == legacyId).ToListAsync(ct)
            : await _db.AttendanceSessions.AsNoTracking().Where(a => a.TenantId == tenantId && a.SemesterId == legacyId).ToListAsync(ct);

        foreach (var a in attendance)
        {
            var item = Classify(
                "AttendanceSession",
                a.Id.ToString(),
                legacyId,
                legacy.Number,
                a.TenantId,
                a.CourseId,
                a.GroupId,
                legacy.CourseId,
                tenantId,
                targetsByGroup);
            items.Add(item);
            if (item.Status == DownstreamRemediationStatus.ManualReviewRequired)
                skippedManual++;
            if (mutate && item.MutationAllowed && item.ProposedSemesterId is int newSem)
            {
                a.SemesterId = newSem;
                mutateCount++;
            }
        }

        var allocations = mutate
            ? await _db.SchedulingSubjectAllocations
                .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.SemesterId == legacyId).ToListAsync(ct)
            : await _db.SchedulingSubjectAllocations.AsNoTracking()
                .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.SemesterId == legacyId).ToListAsync(ct);

        foreach (var a in allocations)
        {
            var item = Classify(
                "SubjectAllocation",
                a.Id.ToString(),
                legacyId,
                legacy.Number,
                a.TenantId,
                a.CourseId,
                a.GroupId,
                legacy.CourseId,
                tenantId,
                targetsByGroup);
            items.Add(item);
            if (item.Status == DownstreamRemediationStatus.ManualReviewRequired)
                skippedManual++;
            if (mutate && item.MutationAllowed && item.ProposedSemesterId is int newSem)
            {
                a.SemesterId = newSem;
                a.UpdatedDate = DateTime.UtcNow;
                mutateCount++;
            }
        }

        var entries = mutate
            ? await _db.SchedulingTimetableEntries
                .Where(e => e.TenantId == tenantId && !e.IsDeleted && e.SemesterId == legacyId).ToListAsync(ct)
            : await _db.SchedulingTimetableEntries.AsNoTracking()
                .Where(e => e.TenantId == tenantId && !e.IsDeleted && e.SemesterId == legacyId).ToListAsync(ct);

        foreach (var e in entries)
        {
            var item = Classify(
                "TimetableEntry",
                e.Id.ToString(),
                legacyId,
                legacy.Number,
                e.TenantId,
                e.CourseId,
                e.GroupId,
                legacy.CourseId,
                tenantId,
                targetsByGroup);
            items.Add(item);
            if (item.Status == DownstreamRemediationStatus.ManualReviewRequired)
                skippedManual++;
            if (mutate && item.MutationAllowed && item.ProposedSemesterId is int newSem)
            {
                e.SemesterId = newSem;
                e.UpdatedDate = DateTime.UtcNow;
                mutateCount++;
            }
        }

        var teachingGroups = await _db.SchedulingTeachingGroups.AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.SemesterId == legacyId)
            .Select(t => new { t.Id, t.TenantId, t.CourseId, t.GroupId })
            .ToListAsync(ct);

        foreach (var t in teachingGroups)
        {
            int? candidate = null;
            var reason =
                "Teaching Group architecture is frozen; remediation DEFERRED / IDENTIFY-ONLY.";
            if (targetsByGroup.TryGetValue(t.GroupId, out var list)
                && list.Count == 1
                && list[0].CourseId == t.CourseId
                && list[0].TenantId == t.TenantId)
            {
                candidate = list[0].Id;
                reason +=
                    $" Candidate target SemesterId={candidate} is identifiable but must not be applied in Prompt 3C.";
            }

            items.Add(new DownstreamRemediationItemDto
            {
                EntityType = "TeachingGroup",
                RecordId = t.Id.ToString(),
                OldSemesterId = legacyId,
                OldSemesterNumber = legacy.Number,
                GroupId = t.GroupId,
                CourseId = t.CourseId,
                ProposedSemesterId = candidate,
                TargetSemesterNumber = candidate is null ? null : ExpectedLegacyNumber,
                Status = DownstreamRemediationStatus.DeferredByArchitectureBoundary,
                StatusCode = "DEFERRED",
                Reason = reason,
                MutationAllowed = false,
            });
        }

        if (mutate)
        {
            if (mutateCount > 0)
                await _db.SaveChangesAsync(ct);

            notes.Add($"Mutated SemesterId on {mutateCount} record(s). Skipped MANUAL_REVIEW={skippedManual}. TeachingGroup writes=0.");
        }

        var ready = items.Count(i => i.Status == DownstreamRemediationStatus.Ready);
        var manual = items.Count(i => i.Status == DownstreamRemediationStatus.ManualReviewRequired);
        var deferred = items.Count(i => i.Status == DownstreamRemediationStatus.DeferredByArchitectureBoundary);
        var already = items.Count(i => i.Status == DownstreamRemediationStatus.AlreadyRemediated);

        var summary = new DownstreamRemediationSummaryDto
        {
            Audited = items.Count,
            Ready = ready,
            AlreadyRemediated = already,
            ManualReviewRequired = manual,
            DeferredByArchitectureBoundary = deferred,
            Remediated = mutate ? mutateCount : 0,
        };

        var executionStatus = "NotExecuted";
        if (mutate)
            executionStatus = mutateCount == 0 ? "AlreadyComplete" : "Completed";

        notes.Add($"Scope locked to legacy SemesterId={legacyId} Number={ExpectedLegacyNumber}.");

        return new DownstreamRemediationReportDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = tenantId,
            IsReadOnly = !mutate,
            LegacySemesterId = legacyId,
            LegacySemesterNumber = ExpectedLegacyNumber,
            Summary = summary,
            Items = items,
            Notes = notes,
            RolledBack = false,
            ExecutionStatus = executionStatus,
        };
    }

    private static DownstreamRemediationItemDto Classify(
        string entityType,
        string recordId,
        int legacyId,
        int legacyNumber,
        int recordTenantId,
        int recordCourseId,
        int recordGroupId,
        int legacyCourseId,
        int tenantId,
        IReadOnlyDictionary<int, List<TargetSemester>> targetsByGroup)
    {
        if (recordTenantId != tenantId)
        {
            return Item(
                entityType, recordId, legacyId, legacyNumber, recordGroupId, recordCourseId,
                null, DownstreamRemediationStatus.ManualReviewRequired, "MANUAL_REVIEW_REQUIRED",
                "Cross-tenant record; fail closed.", false);
        }

        if (recordGroupId <= 0)
        {
            return Item(
                entityType, recordId, legacyId, legacyNumber, recordGroupId, recordCourseId,
                null, DownstreamRemediationStatus.ManualReviewRequired, "MANUAL_REVIEW_REQUIRED",
                "GroupId cannot be resolved deterministically.", false);
        }

        if (recordCourseId != legacyCourseId)
        {
            return Item(
                entityType, recordId, legacyId, legacyNumber, recordGroupId, recordCourseId,
                null, DownstreamRemediationStatus.ManualReviewRequired, "MANUAL_REVIEW_REQUIRED",
                "Record CourseId does not match legacy Semester CourseId.", false);
        }

        if (!targetsByGroup.TryGetValue(recordGroupId, out var matches) || matches.Count == 0)
        {
            return Item(
                entityType, recordId, legacyId, legacyNumber, recordGroupId, recordCourseId,
                null, DownstreamRemediationStatus.ManualReviewRequired, "MANUAL_REVIEW_REQUIRED",
                "Missing target Group-specific Semester for GroupId + Number.", false);
        }

        if (matches.Count > 1)
        {
            return Item(
                entityType, recordId, legacyId, legacyNumber, recordGroupId, recordCourseId,
                null, DownstreamRemediationStatus.ManualReviewRequired, "MANUAL_REVIEW_REQUIRED",
                "Duplicate target Semesters for GroupId + Number.", false);
        }

        var target = matches[0];
        if (target.TenantId != recordTenantId
            || target.CourseId != recordCourseId
            || target.GroupId != recordGroupId
            || target.Number != legacyNumber)
        {
            return Item(
                entityType, recordId, legacyId, legacyNumber, recordGroupId, recordCourseId,
                null, DownstreamRemediationStatus.ManualReviewRequired, "MANUAL_REVIEW_REQUIRED",
                "Target Semester failed ownership validation.", false);
        }

        return Item(
            entityType, recordId, legacyId, legacyNumber, recordGroupId, recordCourseId,
            target.Id, DownstreamRemediationStatus.Ready, "READY",
            "Deterministic Group → Semester resolution.", true);
    }

    private static DownstreamRemediationItemDto Item(
        string entityType,
        string recordId,
        int oldSemId,
        int oldNumber,
        int? groupId,
        int? courseId,
        int? proposed,
        DownstreamRemediationStatus status,
        string code,
        string reason,
        bool allowed)
        => new()
        {
            EntityType = entityType,
            RecordId = recordId,
            OldSemesterId = oldSemId,
            OldSemesterNumber = oldNumber,
            GroupId = groupId,
            CourseId = courseId,
            ProposedSemesterId = proposed,
            TargetSemesterNumber = proposed is null ? null : ExpectedLegacyNumber,
            Status = status,
            StatusCode = code,
            Reason = reason,
            MutationAllowed = allowed,
        };

    private static DownstreamRemediationReportDto Abort(
        int tenantId, string reason, List<string> notes, bool mutate)
        => new()
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = tenantId,
            IsReadOnly = !mutate,
            LegacySemesterNumber = ExpectedLegacyNumber,
            Notes = notes,
            AbortReason = reason,
            RolledBack = false,
            ExecutionStatus = mutate ? "Aborted" : "NotExecuted",
        };

    private sealed record TargetSemester(int Id, int TenantId, int CourseId, int GroupId, int Number);
}
