using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3F —
/// Fail-closed remediation: TeachingGroup.SemesterId only for the two approved TG residuals
/// (legacy Sem 3 → Group-specific Sem 11). Does not mutate section links, membership,
/// attendance rows, or write TimetableSection directly.
/// </summary>
public sealed class TeachingGroupSemesterRemediationService : ITeachingGroupSemesterRemediationService
{
    /// <summary>Exact Teaching Group IDs approved by Prompt 3D/3E audits (local tenant baseline).</summary>
    public static readonly IReadOnlyList<int> ApprovedTeachingGroupIds = [1, 2];

    public const int ExpectedLegacySemesterId = 3;
    public const int ExpectedTargetSemesterId = 11;
    public const int ExpectedSemesterNumber = 3;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ITimetableSectionProjector _projector;
    private readonly ISemesterPostMigrationIntegrityAuditService _integrityAudit;
    private readonly ILegacySemesterFinalizationAuditService _finalizationAudit;
    private readonly ILogger<TeachingGroupSemesterRemediationService> _logger;

    public TeachingGroupSemesterRemediationService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ITimetableSectionProjector projector,
        ISemesterPostMigrationIntegrityAuditService integrityAudit,
        ILegacySemesterFinalizationAuditService finalizationAudit,
        ILogger<TeachingGroupSemesterRemediationService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _projector = projector;
        _integrityAudit = integrityAudit;
        _finalizationAudit = finalizationAudit;
        _logger = logger;
    }

    public Task<TeachingGroupSemesterRemediationResultDto> PreviewAsync(CancellationToken cancellationToken = default)
        => BuildAsync(mutate: false, cancellationToken);

    public async Task<TeachingGroupSemesterRemediationResultDto> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        TeachingGroupSemesterRemediationResultDto? result = null;
        try
        {
            await _db.ExecuteInTransactionAsync(async ct =>
            {
                result = await BuildAsync(mutate: true, ct);
                if (!string.Equals(result.ExecutionStatus, "Completed", StringComparison.Ordinal)
                    && !string.Equals(result.ExecutionStatus, "AlreadyComplete", StringComparison.Ordinal))
                {
                    throw new DomainException(result.AbortReason ?? "Teaching Group Semester remediation aborted.");
                }
            }, cancellationToken);
        }
        catch (ConcurrencyConflictException ex)
        {
            _logger.LogWarning(ex, "P1-4 Prompt 3F concurrency conflict.");
            return Aborted(result, ex.Message, concurrency: "ConcurrencyConflictException");
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "P1-4 Prompt 3F EF concurrency conflict.");
            return Aborted(result, "Concurrency conflict while remediating Teaching Groups.", concurrency: "DbUpdateConcurrencyException");
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "P1-4 Prompt 3F Teaching Group remediation aborted and rolled back.");
            return Aborted(result, ex.Message, concurrency: result?.ConcurrencyResult);
        }

        if (result is null)
        {
            return new TeachingGroupSemesterRemediationResultDto
            {
                GeneratedUtc = DateTime.UtcNow,
                TenantId = _currentUser.TenantId,
                IsReadOnly = false,
                ExecutionStatus = "Aborted",
                RolledBack = true,
                AbortReason = "Remediation produced no result.",
                ApprovedTeachingGroupIds = ApprovedTeachingGroupIds,
            };
        }

        var integrity = await _integrityAudit.BuildAuditAsync(cancellationToken);
        var finalization = await _finalizationAudit.BuildAuditAsync(cancellationToken);
        var notes = result.Notes.ToList();
        notes.Add(
            $"Post-integrity IsHealthy={integrity.IsHealthy}; Critical={integrity.Summary.Critical}; Errors={integrity.Summary.Errors}; Warnings={integrity.Summary.Warnings}.");
        notes.Add(
            $"Post-finalization NullGroup={finalization.Summary.LegacyNullGroupCount}; TG residuals={finalization.Summary.TeachingGroupResidualCount}; NotNullReady={finalization.Summary.NotNullReady}.");
        notes.Add("TeachingGroupSection / Membership / attendance tables were NOT mutated.");
        notes.Add("TimetableSection was not written directly; projector path used only when TT entries exist.");

        return new TeachingGroupSemesterRemediationResultDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = result.TenantId,
            IsReadOnly = false,
            ExecutionStatus = result.ExecutionStatus,
            RolledBack = false,
            ExecutionSafe = result.ExecutionSafe,
            ChangedCount = result.ChangedCount,
            AlreadyCompleteCount = result.AlreadyCompleteCount,
            BlockedCount = result.BlockedCount,
            ManualReviewCount = result.ManualReviewCount,
            DeferredCount = result.DeferredCount,
            ApprovedTeachingGroupIds = ApprovedTeachingGroupIds,
            AffectedTeachingGroupIds = result.AffectedTeachingGroupIds,
            OldSemesterIds = result.OldSemesterIds,
            NewSemesterIds = result.NewSemesterIds,
            Items = result.Items,
            Notes = notes,
            AbortReason = result.AbortReason,
            ConcurrencyResult = result.ConcurrencyResult ?? "None",
            TransactionCommitted = true,
            PostIntegrityAudit = integrity,
            PostFinalizationAudit = finalization,
        };
    }

    private TeachingGroupSemesterRemediationResultDto Aborted(
        TeachingGroupSemesterRemediationResultDto? result,
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
            BlockedCount = result?.BlockedCount ?? 0,
            ManualReviewCount = result?.ManualReviewCount ?? 0,
            DeferredCount = result?.DeferredCount ?? 0,
            ApprovedTeachingGroupIds = ApprovedTeachingGroupIds,
            AffectedTeachingGroupIds = result?.AffectedTeachingGroupIds ?? [],
            OldSemesterIds = result?.OldSemesterIds ?? [],
            NewSemesterIds = result?.NewSemesterIds ?? [],
            Items = result?.Items ?? [],
            Notes = result?.Notes ?? [],
            AbortReason = reason,
            ConcurrencyResult = concurrency ?? "None",
            TransactionCommitted = false,
        };

    private async Task<TeachingGroupSemesterRemediationResultDto> BuildAsync(bool mutate, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var notes = new List<string>
        {
            mutate
                ? "Execution mode: TeachingGroup.SemesterId only for approved TG IDs."
                : "Read-only preview; zero writes.",
            $"Approved TeachingGroupIds=[{string.Join(",", ApprovedTeachingGroupIds)}]; LegacySem={ExpectedLegacySemesterId}; TargetSem={ExpectedTargetSemesterId}.",
            "Arbitrary TeachingGroupId+SemesterId reassignment is not supported.",
        };

        // --- Baseline: legacy Sem 3 + target Sem 11 ---
        var legacy = await _db.Semesters.AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.TenantId == tenantId && !s.IsDeleted && s.Id == ExpectedLegacySemesterId,
                ct);
        if (legacy is null)
            return AbortPreview(tenantId, mutate, "Legacy Semester Id=3 not found for tenant.", notes);

        if (legacy.GroupId is not null || legacy.Number != ExpectedSemesterNumber)
        {
            return AbortPreview(
                tenantId,
                mutate,
                $"Legacy Semester Id=3 baseline mismatch (GroupId={legacy.GroupId}, Number={legacy.Number}).",
                notes);
        }

        var target = await _db.Semesters.AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.TenantId == tenantId && !s.IsDeleted && s.Id == ExpectedTargetSemesterId,
                ct);
        if (target is null)
            return AbortPreview(tenantId, mutate, "Target Semester Id=11 not found for tenant.", notes);

        if (target.GroupId is null)
            return AbortPreview(tenantId, mutate, "Target Semester Id=11 is NULL-group; fail closed.", notes);

        if (target.Number != ExpectedSemesterNumber)
        {
            return AbortPreview(
                tenantId,
                mutate,
                $"Target Semester Id=11 Number={target.Number} expected {ExpectedSemesterNumber}.",
                notes);
        }

        // Duplicate Group+Number operational Semesters
        var dupTargets = await _db.Semesters.AsNoTracking()
            .CountAsync(
                s => s.TenantId == tenantId
                     && !s.IsDeleted
                     && s.GroupId == target.GroupId
                     && s.Number == target.Number,
                ct);
        if (dupTargets != 1)
        {
            return AbortPreview(
                tenantId,
                mutate,
                $"Duplicate or missing Group-specific Semester for GroupId={target.GroupId} Number={target.Number} (count={dupTargets}).",
                notes);
        }

        // Unexpected extra TGs on legacy Sem 3 outside approved set
        var allLegacyTgIds = await _db.SchedulingTeachingGroups.AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.SemesterId == ExpectedLegacySemesterId)
            .Select(t => t.Id)
            .ToListAsync(ct);
        var unexpected = allLegacyTgIds.Except(ApprovedTeachingGroupIds).ToList();
        if (unexpected.Count > 0)
        {
            return AbortPreview(
                tenantId,
                mutate,
                $"Unexpected TeachingGroup(s) on legacy Sem 3 outside approved set: [{string.Join(",", unexpected)}].",
                notes);
        }

        var items = new List<TeachingGroupSemesterRemediationItemDto>();
        var already = 0;
        var manual = 0;
        var blocked = 0;
        var pendingReady = new List<TeachingGroup>();
        var membershipBefore = new Dictionary<int, int>();
        var tgsBefore = new Dictionary<int, List<int>>();

        foreach (var tgId in ApprovedTeachingGroupIds.OrderBy(x => x))
        {
            var tgTracked = mutate
                ? await _db.SchedulingTeachingGroups
                    .FirstOrDefaultAsync(t => t.Id == tgId && t.TenantId == tenantId && !t.IsDeleted, ct)
                : null;
            var tg = tgTracked ?? await _db.SchedulingTeachingGroups.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tgId && t.TenantId == tenantId && !t.IsDeleted, ct);

            if (tg is null)
            {
                blocked++;
                items.Add(BlockedItem(tgId, tenantId, $"Approved TeachingGroup Id={tgId} not found for tenant."));
                continue;
            }

            if (tg.TenantId != tenantId)
            {
                blocked++;
                items.Add(BlockedItem(tgId, tenantId, "Cross-tenant TeachingGroup; fail closed."));
                continue;
            }

            var membershipCount = await _db.SchedulingTeachingGroupMemberships.AsNoTracking()
                .CountAsync(m => m.TenantId == tenantId && !m.IsDeleted && m.TeachingGroupId == tgId, ct);
            membershipBefore[tgId] = membershipCount;

            var tgsLinks = await _db.SchedulingTeachingGroupSections.AsNoTracking()
                .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.TeachingGroupId == tgId)
                .Select(x => new { x.Id, x.SectionId })
                .ToListAsync(ct);
            tgsBefore[tgId] = tgsLinks.Select(x => x.SectionId).OrderBy(x => x).ToList();

            if (tg.SemesterId == ExpectedTargetSemesterId)
            {
                var alreadyItem = await ValidateItemAsync(
                    tg, target, legacy, tgsLinks.Select(x => (x.Id, x.SectionId)).ToList(),
                    membershipCount, alreadyComplete: true, ct);
                items.Add(alreadyItem);
                if (alreadyItem.StatusKind == TeachingGroupSemesterRemediationStatus.AlreadyComplete)
                    already++;
                else
                    manual++;
                continue;
            }

            if (tg.SemesterId != ExpectedLegacySemesterId)
            {
                blocked++;
                items.Add(new TeachingGroupSemesterRemediationItemDto
                {
                    TeachingGroupId = tg.Id,
                    Code = tg.Code,
                    Name = tg.Name,
                    TenantId = tg.TenantId,
                    CourseId = tg.CourseId,
                    GroupId = tg.GroupId,
                    SubjectId = tg.SubjectId,
                    SubjectAllocationId = tg.SubjectAllocationId,
                    AcademicYearId = tg.AcademicYearId,
                    Status = tg.Status.ToString(),
                    CurrentSemesterId = tg.SemesterId,
                    TargetSemesterId = ExpectedTargetSemesterId,
                    StatusKind = TeachingGroupSemesterRemediationStatus.Blocked,
                    StatusCode = "BLOCKED",
                    Reason = $"TeachingGroup SemesterId={tg.SemesterId} is neither legacy {ExpectedLegacySemesterId} nor target {ExpectedTargetSemesterId}; fail closed.",
                    MutationAllowed = false,
                    MembershipCount = membershipCount,
                    TeachingGroupSectionCount = tgsLinks.Count,
                });
                continue;
            }

            var item = await ValidateItemAsync(
                tg, target, legacy, tgsLinks.Select(x => (x.Id, x.SectionId)).ToList(),
                membershipCount, alreadyComplete: false, ct);
            items.Add(item);

            if (item.StatusKind == TeachingGroupSemesterRemediationStatus.ManualReviewRequired)
            {
                manual++;
                continue;
            }

            if (item.StatusKind == TeachingGroupSemesterRemediationStatus.Blocked)
            {
                blocked++;
                continue;
            }

            if (item.StatusKind == TeachingGroupSemesterRemediationStatus.Ready && tgTracked is not null)
                pendingReady.Add(tgTracked);
            else if (item.StatusKind == TeachingGroupSemesterRemediationStatus.Ready && !mutate)
            {
                // preview — nothing to queue
            }
        }

        var anyManualOrBlocked = manual > 0 || blocked > 0;
        var changed = 0;
        var affected = new List<int>();
        var oldSemesters = new List<int>();
        var newSemesters = new List<int>();

        if (mutate && anyManualOrBlocked)
        {
            throw new DomainException(
                "One or more approved Teaching Groups require MANUAL_REVIEW or are BLOCKED; batch rolled back (zero TG mutations).");
        }

        if (mutate && pendingReady.Count > 0)
        {
            foreach (var current in pendingReady)
            {
                if (current.SemesterId != ExpectedLegacySemesterId
                    || current.CourseId != target.CourseId
                    || current.GroupId != target.GroupId)
                {
                    throw new DomainException(
                        $"TeachingGroup Id={current.Id} changed since validation; concurrency/baseline abort.");
                }

                if (target.GroupId != current.GroupId
                    || target.CourseId != current.CourseId
                    || target.TenantId != current.TenantId)
                {
                    throw new DomainException(
                        $"Target Semester {ExpectedTargetSemesterId} ownership mismatch for TG {current.Id}.");
                }

                var oldSem = current.SemesterId;
                current.SemesterId = ExpectedTargetSemesterId;
                current.UpdatedDate = DateTime.UtcNow;
                changed++;
                affected.Add(current.Id);
                oldSemesters.Add(oldSem);
                newSemesters.Add(ExpectedTargetSemesterId);

                var entries = await _db.SchedulingTimetableEntries
                    .Where(e => e.TenantId == tenantId && !e.IsDeleted && e.TeachingGroupId == current.Id)
                    .ToListAsync(ct);
                foreach (var e in entries)
                {
                    if (e.CourseId != current.CourseId || e.GroupId != current.GroupId)
                    {
                        throw new DomainException(
                            $"TimetableEntry Id={e.Id} Course/Group mismatch with TG {current.Id}; fail closed.");
                    }

                    if (e.SemesterId == ExpectedTargetSemesterId)
                        continue;

                    if (e.SemesterId == ExpectedLegacySemesterId)
                    {
                        e.SemesterId = ExpectedTargetSemesterId;
                        e.UpdatedDate = DateTime.UtcNow;
                        continue;
                    }

                    throw new DomainException(
                        $"TimetableEntry Id={e.Id} SemesterId={e.SemesterId} cannot be safely aligned; fail closed.");
                }

                var hasSections = tgsBefore.GetValueOrDefault(current.Id)?.Count > 0;
                if (entries.Count > 0 || hasSections)
                {
                    await _projector.SyncTeachingGroupSectionsToTimetableEntriesAsync(current.Id, cancellationToken: ct);
                }
            }

            await ConcurrencyExceptionHelper.SaveChangesAsync(_db, ct);

            foreach (var tgId in ApprovedTeachingGroupIds)
            {
                var secAfter = await _db.SchedulingTeachingGroupSections.AsNoTracking()
                    .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.TeachingGroupId == tgId)
                    .Select(x => x.SectionId)
                    .OrderBy(x => x)
                    .ToListAsync(ct);
                var expectedSecs = tgsBefore.GetValueOrDefault(tgId) ?? [];
                if (!expectedSecs.SequenceEqual(secAfter))
                    throw new DomainException($"TeachingGroupSection mutated for TG {tgId}; forbidden.");

                var memAfter = await _db.SchedulingTeachingGroupMemberships.AsNoTracking()
                    .CountAsync(m => m.TenantId == tenantId && !m.IsDeleted && m.TeachingGroupId == tgId, ct);
                if (memAfter != membershipBefore.GetValueOrDefault(tgId))
                    throw new DomainException($"TeachingGroupMembership mutated for TG {tgId}; forbidden.");
            }

            foreach (var tgId in affected)
            {
                var canonical = await _db.SchedulingTeachingGroupSections.AsNoTracking()
                    .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.TeachingGroupId == tgId)
                    .Select(x => x.SectionId)
                    .OrderBy(x => x)
                    .ToListAsync(ct);
                var entryIds = await _db.SchedulingTimetableEntries.AsNoTracking()
                    .Where(e => e.TenantId == tenantId && !e.IsDeleted && e.TeachingGroupId == tgId)
                    .Select(e => e.Id)
                    .ToListAsync(ct);
                foreach (var entryId in entryIds)
                {
                    var projected = await _db.TimetableSections.AsNoTracking()
                        .Where(ts => ts.TenantId == tenantId && !ts.IsDeleted && ts.TimetableEntryId == entryId)
                        .Select(ts => ts.SectionId)
                        .OrderBy(x => x)
                        .ToListAsync(ct);
                    if (!canonical.SequenceEqual(projected))
                    {
                        throw new DomainException(
                            $"TimetableSection projection mismatch for TG {tgId} entry {entryId}.");
                    }
                }
            }
        }

        notes.Add(
            $"Items={items.Count}; Changed={changed}; Already={already}; Manual={manual}; Blocked={blocked}.");

        var executionSafe = items.All(i =>
            i.StatusKind is TeachingGroupSemesterRemediationStatus.Ready
                or TeachingGroupSemesterRemediationStatus.AlreadyComplete);

        string status;
        if (!mutate)
            status = "NotExecuted";
        else if (changed == 0 && already == ApprovedTeachingGroupIds.Count)
            status = "AlreadyComplete";
        else if (changed > 0 && !anyManualOrBlocked)
            status = "Completed";
        else
            status = "Aborted";

        return new TeachingGroupSemesterRemediationResultDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = tenantId,
            IsReadOnly = !mutate,
            ExecutionStatus = status,
            RolledBack = false,
            ExecutionSafe = executionSafe && !anyManualOrBlocked,
            ChangedCount = changed,
            AlreadyCompleteCount = already,
            BlockedCount = blocked,
            ManualReviewCount = manual,
            DeferredCount = 0,
            ApprovedTeachingGroupIds = ApprovedTeachingGroupIds,
            AffectedTeachingGroupIds = affected,
            OldSemesterIds = oldSemesters.Distinct().ToList(),
            NewSemesterIds = newSemesters.Distinct().ToList(),
            Items = items,
            Notes = notes,
            ConcurrencyResult = "None",
            TransactionCommitted = mutate && (changed > 0 || already > 0) && !anyManualOrBlocked,
        };
    }

    private async Task<TeachingGroupSemesterRemediationItemDto> ValidateItemAsync(
        TeachingGroup tg,
        Semester target,
        Semester legacy,
        IReadOnlyList<(int LinkId, int SectionId)> tgsLinks,
        int membershipCount,
        bool alreadyComplete,
        CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var reasons = new List<string>();
        var sectionChecks = new List<TeachingGroupSemesterRemediationSectionCheckDto>();

        // Target must match TG Course + Group + Tenant (never accept Id=11 alone)
        var targetOk = target.TenantId == tg.TenantId
                       && target.CourseId == tg.CourseId
                       && target.GroupId == tg.GroupId
                       && target.GroupId is not null
                       && target.Number == ExpectedSemesterNumber
                       && !target.IsDeleted;
        if (!targetOk)
            reasons.Add("Target Semester ownership does not match TeachingGroup Course/Group/Tenant.");

        // SubjectAllocation consistency
        var sa = await _db.SchedulingSubjectAllocations.AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.Id == tg.SubjectAllocationId && a.TenantId == tenantId && !a.IsDeleted,
                ct);
        var saOk = sa is not null
                   && sa.CourseId == tg.CourseId
                   && sa.GroupId == tg.GroupId
                   && sa.SemesterId == ExpectedTargetSemesterId;
        if (sa is null)
            reasons.Add($"SubjectAllocation Id={tg.SubjectAllocationId} missing.");
        else if (sa.CourseId != tg.CourseId || sa.GroupId != tg.GroupId)
            reasons.Add("SubjectAllocation Course/Group mismatch with TeachingGroup.");
        else if (sa.SemesterId != ExpectedTargetSemesterId)
            reasons.Add($"SubjectAllocation SemesterId={sa.SemesterId} is not target {ExpectedTargetSemesterId}; SA not mutated in 3F — MANUAL_REVIEW.");

        // TeachingGroupSection → Section compatibility with TARGET semester
        var allSectionsCompatible = true;
        foreach (var link in tgsLinks)
        {
            var section = await _db.Sections.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == link.SectionId && s.TenantId == tenantId && !s.IsDeleted, ct);
            if (section is null)
            {
                allSectionsCompatible = false;
                sectionChecks.Add(new TeachingGroupSemesterRemediationSectionCheckDto
                {
                    TeachingGroupSectionId = link.LinkId,
                    SectionId = link.SectionId,
                    IsCompatible = false,
                    Notes = "Section missing or deleted.",
                });
                reasons.Add($"Section Id={link.SectionId} missing for TeachingGroupSection.");
                continue;
            }

            var compatible = section.TenantId == tg.TenantId
                             && section.CourseId == target.CourseId
                             && section.GroupId == target.GroupId
                             && section.SemesterId == target.Id;
            if (!compatible)
                allSectionsCompatible = false;

            sectionChecks.Add(new TeachingGroupSemesterRemediationSectionCheckDto
            {
                TeachingGroupSectionId = link.LinkId,
                SectionId = section.Id,
                SectionCourseId = section.CourseId,
                SectionGroupId = section.GroupId,
                SectionSemesterId = section.SemesterId,
                IsCompatible = compatible,
                Notes = compatible
                    ? "Compatible with target Semester."
                    : $"Incompatible: Section Sem={section.SemesterId} Course={section.CourseId} Group={section.GroupId}; target Sem={target.Id} Course={target.CourseId} Group={target.GroupId}. Do not move Section.",
            });

            if (!compatible)
                reasons.Add($"TeachingGroupSection→Section Id={section.Id} incompatible with target Semester {target.Id}.");
        }

        // TimetableEntry
        var entries = await _db.SchedulingTimetableEntries.AsNoTracking()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted && e.TeachingGroupId == tg.Id)
            .Select(e => new { e.Id, e.CourseId, e.GroupId, e.SemesterId, e.TimetableId })
            .ToListAsync(ct);
        var ttOk = true;
        foreach (var e in entries)
        {
            if (e.CourseId != tg.CourseId || e.GroupId != tg.GroupId)
            {
                ttOk = false;
                reasons.Add($"TimetableEntry Id={e.Id} Course/Group mismatch.");
            }
            else if (e.SemesterId != ExpectedTargetSemesterId && e.SemesterId != ExpectedLegacySemesterId)
            {
                ttOk = false;
                reasons.Add($"TimetableEntry Id={e.Id} SemesterId={e.SemesterId} not alignable.");
            }
        }

        // Projection consistency (current)
        var projectionOk = true;
        var canonical = tgsLinks.Select(x => x.SectionId).OrderBy(x => x).ToList();
        foreach (var e in entries)
        {
            var projected = await _db.TimetableSections.AsNoTracking()
                .Where(ts => ts.TenantId == tenantId && !ts.IsDeleted && ts.TimetableEntryId == e.Id)
                .Select(ts => ts.SectionId)
                .OrderBy(x => x)
                .ToListAsync(ct);
            if (!canonical.SequenceEqual(projected))
            {
                projectionOk = false;
                reasons.Add($"Projection mismatch for TimetableEntry Id={e.Id}.");
            }
        }

        if (tg.IsDeleted || tg.Status == Domain.Enums.Scheduling.TeachingGroupStatus.Archived)
            reasons.Add("TeachingGroup is deleted or archived.");

        TeachingGroupSemesterRemediationStatus kind;
        string code;
        bool mutationAllowed;

        if (alreadyComplete)
        {
            kind = TeachingGroupSemesterRemediationStatus.AlreadyComplete;
            code = "ALREADY_COMPLETE";
            mutationAllowed = false;
            if (reasons.Count > 0)
            {
                // Already on target but inconsistent satellite state
                kind = TeachingGroupSemesterRemediationStatus.ManualReviewRequired;
                code = "MANUAL_REVIEW_REQUIRED";
            }
        }
        else if (!targetOk || !allSectionsCompatible || !saOk || !ttOk || !projectionOk || reasons.Count > 0)
        {
            kind = TeachingGroupSemesterRemediationStatus.ManualReviewRequired;
            code = "MANUAL_REVIEW_REQUIRED";
            mutationAllowed = false;
        }
        else
        {
            kind = TeachingGroupSemesterRemediationStatus.Ready;
            code = "READY";
            mutationAllowed = true;
        }

        return new TeachingGroupSemesterRemediationItemDto
        {
            TeachingGroupId = tg.Id,
            Code = tg.Code,
            Name = tg.Name,
            TenantId = tg.TenantId,
            CourseId = tg.CourseId,
            GroupId = tg.GroupId,
            SubjectId = tg.SubjectId,
            SubjectAllocationId = tg.SubjectAllocationId,
            AcademicYearId = tg.AcademicYearId,
            Status = tg.Status.ToString(),
            CurrentSemesterId = tg.SemesterId,
            TargetSemesterId = target.Id,
            TargetGroupId = target.GroupId,
            TargetCourseId = target.CourseId,
            TargetNumber = target.Number,
            StatusKind = kind,
            StatusCode = code,
            Reason = reasons.Count == 0
                ? (alreadyComplete ? "Already on target Semester." : "Validated; ready for SemesterId remediation.")
                : string.Join(" ", reasons),
            MutationAllowed = mutationAllowed,
            SubjectAllocationConsistent = saOk,
            TimetableEntryConsistent = ttOk,
            ProjectionConsistent = projectionOk,
            TeachingGroupSectionCount = tgsLinks.Count,
            MembershipCount = membershipCount,
            TimetableEntryCount = entries.Count,
            SectionChecks = sectionChecks,
            TimetableEntryIds = entries.Select(e => e.Id).ToList(),
        };
    }

    private static TeachingGroupSemesterRemediationItemDto BlockedItem(int tgId, int tenantId, string reason)
        => new()
        {
            TeachingGroupId = tgId,
            TenantId = tenantId,
            StatusKind = TeachingGroupSemesterRemediationStatus.Blocked,
            StatusCode = "BLOCKED",
            Reason = reason,
            MutationAllowed = false,
        };

    private TeachingGroupSemesterRemediationResultDto AbortPreview(
        int tenantId,
        bool mutate,
        string reason,
        List<string> notes)
    {
        notes.Add($"ABORT: {reason}");
        return new TeachingGroupSemesterRemediationResultDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = tenantId,
            IsReadOnly = !mutate,
            ExecutionStatus = "Aborted",
            RolledBack = mutate,
            ExecutionSafe = false,
            AbortReason = reason,
            ApprovedTeachingGroupIds = ApprovedTeachingGroupIds,
            Notes = notes,
            ConcurrencyResult = "None",
        };
    }
}
