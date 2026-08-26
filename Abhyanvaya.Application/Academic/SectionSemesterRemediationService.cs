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
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3G —
/// Fail-closed remediation: approved Section.SemesterId only (legacy 3 → target 11).
/// Does not mutate TeachingGroup, TeachingGroupSection, membership, SA, TT, Attendance,
/// StudentSection, Student, or Semester ownership.
/// </summary>
public sealed class SectionSemesterRemediationService : ISectionSemesterRemediationService
{
    public const int ExpectedLegacySemesterId = 3;
    public const int ExpectedTargetSemesterId = 11;
    public const int ExpectedSemesterNumber = 3;
    public const int RequiredKnownBlockerSectionId = 5;
    public const string PromptCode = "P1-4-3G";
    public const string JournalDispositionCode = "SECTION_SEMESTER_REMEDIATION";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<SectionSemesterRemediationService> _logger;

    public SectionSemesterRemediationService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ILogger<SectionSemesterRemediationService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    public Task<SectionSemesterRemediationResultDto> PreviewAsync(CancellationToken cancellationToken = default)
        => BuildAsync(mutate: false, cancellationToken);

    public async Task<SectionSemesterRemediationResultDto> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        SectionSemesterRemediationResultDto? result = null;
        try
        {
            await _db.ExecuteInTransactionAsync(async ct =>
            {
                result = await BuildAsync(mutate: true, ct);
                if (!string.Equals(result.ExecutionStatus, "Completed", StringComparison.Ordinal)
                    && !string.Equals(result.ExecutionStatus, "AlreadyComplete", StringComparison.Ordinal))
                {
                    throw new DomainException(result.AbortReason ?? "Section Semester remediation aborted.");
                }
            }, cancellationToken);
        }
        catch (ConcurrencyConflictException ex)
        {
            _logger.LogWarning(ex, "P1-4 Prompt 3G concurrency conflict.");
            return Aborted(result, ex.Message, "ConcurrencyConflictException");
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "P1-4 Prompt 3G EF concurrency conflict.");
            return Aborted(result, "Concurrency conflict while remediating Sections.", "DbUpdateConcurrencyException");
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "P1-4 Prompt 3G Section remediation aborted and rolled back.");
            return Aborted(result, ex.Message, result?.ConcurrencyResult);
        }

        return result ?? new SectionSemesterRemediationResultDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = _currentUser.TenantId,
            IsReadOnly = false,
            ExecutionStatus = "Aborted",
            RolledBack = true,
            AbortReason = "Remediation produced no result.",
            LegacySemesterId = ExpectedLegacySemesterId,
            TargetSemesterId = ExpectedTargetSemesterId,
        };
    }

    private SectionSemesterRemediationResultDto Aborted(
        SectionSemesterRemediationResultDto? result,
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
            LegacySemesterId = ExpectedLegacySemesterId,
            TargetSemesterId = ExpectedTargetSemesterId,
            TargetCourseId = result?.TargetCourseId,
            TargetGroupId = result?.TargetGroupId,
            ChangedCount = 0,
            AlreadyCompleteCount = result?.AlreadyCompleteCount ?? 0,
            BlockedCount = result?.BlockedCount ?? 0,
            ManualReviewCount = result?.ManualReviewCount ?? 0,
            EligibleCount = result?.EligibleCount ?? 0,
            ApprovedSectionIds = result?.ApprovedSectionIds ?? [],
            Items = result?.Items ?? [],
            Notes = result?.Notes ?? [],
            AbortReason = reason,
            ConcurrencyResult = concurrency ?? "None",
            TransactionCommitted = false,
            TeachingGroupsUnchanged = true,
            TeachingGroupSectionsUnchanged = true,
        };

    private async Task<SectionSemesterRemediationResultDto> BuildAsync(bool mutate, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var notes = new List<string>
        {
            mutate
                ? "Execution mode: Section.SemesterId only for approved CA Section IDs."
                : "Read-only preview; zero writes.",
            $"Contract: legacy Sem={ExpectedLegacySemesterId} → target Sem={ExpectedTargetSemesterId}; required blocker SectionId={RequiredKnownBlockerSectionId}.",
            "Approved set = Sem-3 Sections matching target CourseId+GroupId (CA). Other Sem-3 Sections are blocked/out of scope.",
            "TeachingGroup / TeachingGroupSection / SA / TT / Attendance / StudentSection / Student / Semester ownership are not mutated.",
            "Does not re-execute Prompt 3F.",
        };

        var legacy = await _db.Semesters.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.Id == ExpectedLegacySemesterId, ct);
        if (legacy is null)
            return AbortPreview(tenantId, mutate, "Legacy Semester Id=3 not found.", notes, []);
        if (legacy.GroupId is not null || legacy.Number != ExpectedSemesterNumber)
        {
            return AbortPreview(
                tenantId, mutate,
                $"Legacy Semester Id=3 baseline mismatch (GroupId={legacy.GroupId}, Number={legacy.Number}).",
                notes, []);
        }

        var target = await _db.Semesters.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.Id == ExpectedTargetSemesterId, ct);
        if (target is null)
            return AbortPreview(tenantId, mutate, "Target Semester Id=11 not found.", notes, []);
        if (target.GroupId is null)
            return AbortPreview(tenantId, mutate, "Target Semester Id=11 is NULL-group; fail closed.", notes, []);
        if (target.Number != ExpectedSemesterNumber)
        {
            return AbortPreview(
                tenantId, mutate,
                $"Target Semester Id=11 Number={target.Number} expected {ExpectedSemesterNumber}.",
                notes, []);
        }

        var targetGroupId = target.GroupId.Value;
        var targetCourseId = target.CourseId;

        var legacySections = mutate
            ? await _db.Sections
                .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.SemesterId == ExpectedLegacySemesterId)
                .OrderBy(s => s.Id).ToListAsync(ct)
            : await _db.Sections.AsNoTracking()
                .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.SemesterId == ExpectedLegacySemesterId)
                .OrderBy(s => s.Id).ToListAsync(ct);

        var approvedOnLegacy = legacySections
            .Where(s => s.CourseId == targetCourseId && s.GroupId == targetGroupId)
            .Select(s => s.Id)
            .OrderBy(x => x)
            .ToList();

        var blocker = await _db.Sections.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.Id == RequiredKnownBlockerSectionId, ct);
        if (blocker is null)
        {
            return AbortPreview(
                tenantId, mutate,
                $"Required known blocker Section Id={RequiredKnownBlockerSectionId} not found for tenant.",
                notes, approvedOnLegacy);
        }

        if (blocker.SemesterId == ExpectedLegacySemesterId
            && (blocker.CourseId != targetCourseId || blocker.GroupId != targetGroupId))
        {
            return AbortPreview(
                tenantId, mutate,
                $"Required Section {RequiredKnownBlockerSectionId} on legacy Sem 3 has incompatible Course/Group.",
                notes, approvedOnLegacy);
        }

        if (blocker.SemesterId == ExpectedLegacySemesterId && !approvedOnLegacy.Contains(RequiredKnownBlockerSectionId))
        {
            return AbortPreview(
                tenantId, mutate,
                $"Required Section {RequiredKnownBlockerSectionId} missing from approved CA Sem-3 set.",
                notes, approvedOnLegacy);
        }

        var journaledSectionIds = await ResolveJournaledSectionIdsAsync(tenantId, ct);
        var completeIdFilter = approvedOnLegacy
            .Concat(journaledSectionIds)
            .Append(RequiredKnownBlockerSectionId)
            .Distinct()
            .ToList();

        var completeOnTarget = await _db.Sections.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted
                        && s.SemesterId == ExpectedTargetSemesterId
                        && s.CourseId == targetCourseId
                        && s.GroupId == targetGroupId
                        && completeIdFilter.Contains(s.Id))
            .OrderBy(s => s.Id)
            .ToListAsync(ct);

        var approvedIds = approvedOnLegacy
            .Concat(completeOnTarget.Select(s => s.Id))
            .Concat(journaledSectionIds)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        if (approvedIds.Count == 0)
        {
            return AbortPreview(
                tenantId, mutate,
                "No approved CA Sections found for Sem 3→11 remediation.",
                notes, []);
        }

        notes.Add($"ApprovedSectionIds=[{string.Join(",", approvedIds)}].");

        var tgLinks = await _db.SchedulingTeachingGroupSections.AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Select(x => new { x.SectionId, x.TeachingGroupId })
            .ToListAsync(ct);
        var tgBySection = tgLinks.GroupBy(x => x.SectionId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(x => x.TeachingGroupId).Distinct().OrderBy(x => x).ToList());

        var tgSemesterSnapshot = await _db.SchedulingTeachingGroups.AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted)
            .Select(t => new { t.Id, t.SemesterId })
            .ToDictionaryAsync(t => t.Id, t => t.SemesterId, ct);

        var tgsLinkSnapshot = tgLinks
            .GroupBy(x => x.TeachingGroupId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.SectionId).OrderBy(x => x).ToList());

        var studentCounts = await _db.StudentSections.AsNoTracking()
            .Where(ss => ss.TenantId == tenantId && ss.IsCurrent)
            .GroupBy(ss => ss.SectionId)
            .Select(g => new { SectionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SectionId, x => x.Count, ct);

        var targetCodes = (await _db.Sections.AsNoTracking()
                .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.SemesterId == ExpectedTargetSemesterId)
                .Select(s => new { s.Id, s.AcademicYearId, s.CourseId, s.GroupId, s.SectionCode })
                .ToListAsync(ct))
            .Select(t => (t.Id, t.AcademicYearId, t.CourseId, t.GroupId, t.SectionCode))
            .ToList();

        var items = new List<SectionSemesterRemediationItemDto>();
        var pending = new List<Section>();
        var already = 0;
        var blocked = 0;
        var manual = 0;
        var eligible = 0;

        foreach (var section in legacySections)
        {
            var inApproved = approvedIds.Contains(section.Id);
            var item = Classify(
                section, targetCourseId, targetGroupId, inApproved, alreadyComplete: false,
                tgBySection.GetValueOrDefault(section.Id) ?? [],
                tgLinks.Count(x => x.SectionId == section.Id),
                studentCounts.GetValueOrDefault(section.Id),
                targetCodes);
            items.Add(item);

            switch (item.StatusKind)
            {
                case SectionSemesterRemediationStatus.Ready:
                    eligible++;
                    if (mutate)
                        pending.Add(section);
                    break;
                case SectionSemesterRemediationStatus.Blocked:
                    blocked++;
                    break;
                case SectionSemesterRemediationStatus.ManualReviewRequired:
                    manual++;
                    break;
            }
        }

        foreach (var section in completeOnTarget)
        {
            if (items.Any(i => i.SectionId == section.Id))
                continue;
            var item = Classify(
                section, targetCourseId, targetGroupId, inApproved: true, alreadyComplete: true,
                tgBySection.GetValueOrDefault(section.Id) ?? [],
                tgLinks.Count(x => x.SectionId == section.Id),
                studentCounts.GetValueOrDefault(section.Id),
                targetCodes);
            items.Add(item);
            if (item.StatusKind == SectionSemesterRemediationStatus.AlreadyComplete)
                already++;
            else
                manual++;
        }

        items = items.OrderBy(i => i.SectionId).ToList();

        var anyUnsafe = items.Any(i =>
            approvedIds.Contains(i.SectionId)
            && i.CurrentSemesterId == ExpectedLegacySemesterId
            && i.StatusKind is SectionSemesterRemediationStatus.Blocked
                or SectionSemesterRemediationStatus.ManualReviewRequired);

        var changed = 0;
        var affected = new List<int>();

        if (mutate && anyUnsafe)
        {
            return new SectionSemesterRemediationResultDto
            {
                GeneratedUtc = DateTime.UtcNow,
                TenantId = tenantId,
                IsReadOnly = false,
                ExecutionStatus = "Aborted",
                RolledBack = true,
                ExecutionSafe = false,
                LegacySemesterId = ExpectedLegacySemesterId,
                TargetSemesterId = ExpectedTargetSemesterId,
                TargetCourseId = targetCourseId,
                TargetGroupId = targetGroupId,
                ChangedCount = 0,
                AlreadyCompleteCount = already,
                BlockedCount = blocked,
                ManualReviewCount = manual,
                EligibleCount = eligible,
                ApprovedSectionIds = approvedIds,
                Items = items,
                Notes = notes,
                AbortReason =
                    "One or more approved Sections require MANUAL_REVIEW or are BLOCKED; batch rolled back (zero Section mutations).",
                ConcurrencyResult = "None",
                TransactionCommitted = false,
                TeachingGroupsUnchanged = true,
                TeachingGroupSectionsUnchanged = true,
            };
        }

        if (mutate && pending.Count > 0)
        {
            var pendingIds = pending.Select(p => p.Id).OrderBy(x => x).ToList();
            if (!pendingIds.SequenceEqual(approvedOnLegacy))
            {
                throw new DomainException(
                    $"Approved Section set drifted. Expected=[{string.Join(",", approvedOnLegacy)}] Actual=[{string.Join(",", pendingIds)}].");
            }

            foreach (var section in pending)
            {
                if (section.SemesterId != ExpectedLegacySemesterId
                    || section.CourseId != targetCourseId
                    || section.GroupId != targetGroupId)
                {
                    throw new DomainException(
                        $"Section Id={section.Id} changed since validation; concurrency/baseline abort.");
                }

                section.SemesterId = ExpectedTargetSemesterId;
                section.UpdatedDate = DateTime.UtcNow;
                section.UpdatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null;
                changed++;
                affected.Add(section.Id);
            }

            await _db.AddAsync(new LegacySemesterDispositionJournal
            {
                TenantId = tenantId,
                SemesterId = ExpectedTargetSemesterId,
                DispositionCode = JournalDispositionCode,
                Evidence = $"SectionIds=[{string.Join(",", affected)}]; legacy={ExpectedLegacySemesterId}; actor={_currentUser.UserId}",
                PromptCode = PromptCode,
                AssignedGroupId = targetGroupId,
                SemesterRowMutated = false,
                FinalizedUtc = DateTime.UtcNow,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
            });

            await ConcurrencyExceptionHelper.SaveChangesAsync(_db, ct);

            var tgAfter = await _db.SchedulingTeachingGroups.AsNoTracking()
                .Where(t => tgSemesterSnapshot.Keys.Contains(t.Id))
                .Select(t => new { t.Id, t.SemesterId })
                .ToListAsync(ct);
            foreach (var t in tgAfter)
            {
                if (tgSemesterSnapshot[t.Id] != t.SemesterId)
                    throw new DomainException($"TeachingGroup Id={t.Id} SemesterId mutated; forbidden in Prompt 3G.");
            }

            var tgsAfter = await _db.SchedulingTeachingGroupSections.AsNoTracking()
                .Where(x => x.TenantId == tenantId && !x.IsDeleted)
                .Select(x => new { x.TeachingGroupId, x.SectionId })
                .ToListAsync(ct);
            var tgsAfterMap = tgsAfter.GroupBy(x => x.TeachingGroupId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.SectionId).OrderBy(x => x).ToList());
            foreach (var (tgId, before) in tgsLinkSnapshot)
            {
                var after = tgsAfterMap.GetValueOrDefault(tgId) ?? [];
                if (!before.SequenceEqual(after))
                    throw new DomainException($"TeachingGroupSection links mutated for TG {tgId}; forbidden.");
            }
        }

        var approvedStates = await _db.Sections.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && approvedIds.Contains(s.Id))
            .Select(s => new { s.Id, s.SemesterId })
            .ToListAsync(ct);

        if (mutate && changed == 0 && approvedStates.Count > 0
            && approvedStates.All(s => s.SemesterId == ExpectedTargetSemesterId)
            && !anyUnsafe)
        {
            already = approvedStates.Count;
        }

        notes.Add($"Items={items.Count}; Eligible={eligible}; Changed={changed}; Already={already}; Blocked={blocked}; Manual={manual}.");

        string status;
        if (!mutate)
            status = "NotExecuted";
        else if (changed > 0)
            status = "Completed";
        else if (approvedStates.Count > 0 && approvedStates.All(s => s.SemesterId == ExpectedTargetSemesterId))
            status = "AlreadyComplete";
        else
            status = "Aborted";

        return new SectionSemesterRemediationResultDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = tenantId,
            IsReadOnly = !mutate,
            ExecutionStatus = status,
            RolledBack = false,
            ExecutionSafe = !anyUnsafe && (eligible > 0 || already > 0 || status == "AlreadyComplete"),
            LegacySemesterId = ExpectedLegacySemesterId,
            TargetSemesterId = ExpectedTargetSemesterId,
            TargetCourseId = targetCourseId,
            TargetGroupId = targetGroupId,
            ChangedCount = changed,
            AlreadyCompleteCount = already,
            BlockedCount = blocked,
            ManualReviewCount = manual,
            EligibleCount = eligible,
            ApprovedSectionIds = approvedIds,
            AffectedSectionIds = affected,
            Items = items,
            Notes = notes,
            ConcurrencyResult = "None",
            TransactionCommitted = mutate && (changed > 0 || status == "AlreadyComplete"),
            TeachingGroupsUnchanged = true,
            TeachingGroupSectionsUnchanged = true,
        };
    }

    private SectionSemesterRemediationItemDto Classify(
        Section section,
        int targetCourseId,
        int targetGroupId,
        bool inApproved,
        bool alreadyComplete,
        IReadOnlyList<int> tgIds,
        int tgsLinkCount,
        int studentSectionCount,
        List<(int Id, int AcademicYearId, int CourseId, int GroupId, string SectionCode)> targetCodes)
    {
        var reasons = new List<string>();

        if (section.TenantId != _currentUser.TenantId)
            reasons.Add("Cross-tenant Section; fail closed.");

        if (alreadyComplete)
        {
            if (section.CourseId != targetCourseId || section.GroupId != targetGroupId)
                reasons.Add("Already on target Semester but Course/Group mismatch.");

            return new SectionSemesterRemediationItemDto
            {
                SectionId = section.Id,
                SectionCode = section.SectionCode,
                SectionName = section.SectionName,
                TenantId = section.TenantId,
                CourseId = section.CourseId,
                GroupId = section.GroupId,
                AcademicYearId = section.AcademicYearId,
                CurrentSemesterId = section.SemesterId,
                TargetSemesterId = ExpectedTargetSemesterId,
                Status = section.Status,
                StatusKind = reasons.Count == 0
                    ? SectionSemesterRemediationStatus.AlreadyComplete
                    : SectionSemesterRemediationStatus.ManualReviewRequired,
                StatusCode = reasons.Count == 0 ? "ALREADY_COMPLETE" : "MANUAL_REVIEW_REQUIRED",
                Reason = reasons.Count == 0 ? "Already on target Semester 11." : string.Join(" ", reasons),
                MutationAllowed = false,
                InApprovedSet = inApproved,
                ReferencingTeachingGroupIds = tgIds,
                TeachingGroupSectionLinkCount = tgsLinkCount,
                CurrentStudentSectionCount = studentSectionCount,
            };
        }

        if (!inApproved)
        {
            return new SectionSemesterRemediationItemDto
            {
                SectionId = section.Id,
                SectionCode = section.SectionCode,
                SectionName = section.SectionName,
                TenantId = section.TenantId,
                CourseId = section.CourseId,
                GroupId = section.GroupId,
                AcademicYearId = section.AcademicYearId,
                CurrentSemesterId = section.SemesterId,
                TargetSemesterId = ExpectedTargetSemesterId,
                Status = section.Status,
                StatusKind = SectionSemesterRemediationStatus.Blocked,
                StatusCode = "BLOCKED",
                Reason = $"Section GroupId={section.GroupId}/CourseId={section.CourseId} not in CA approved set for target GroupId={targetGroupId}/CourseId={targetCourseId}; out of Prompt 3G scope.",
                MutationAllowed = false,
                InApprovedSet = false,
                ReferencingTeachingGroupIds = tgIds,
                TeachingGroupSectionLinkCount = tgsLinkCount,
                CurrentStudentSectionCount = studentSectionCount,
            };
        }

        if (section.SemesterId != ExpectedLegacySemesterId)
            reasons.Add($"Current SemesterId={section.SemesterId} is not legacy {ExpectedLegacySemesterId}.");
        if (section.CourseId != targetCourseId)
            reasons.Add($"Section CourseId={section.CourseId} != target CourseId={targetCourseId}.");
        if (section.GroupId != targetGroupId)
            reasons.Add($"Section GroupId={section.GroupId} != target GroupId={targetGroupId}.");
        if (string.Equals(section.Status, "Archived", StringComparison.OrdinalIgnoreCase)
            || string.Equals(section.Status, "Merged", StringComparison.OrdinalIgnoreCase)
            || string.Equals(section.Status, "Split", StringComparison.OrdinalIgnoreCase))
            reasons.Add($"Section Status={section.Status} is not eligible.");

        var collision = targetCodes.Any(t =>
            t.Id != section.Id
            && t.AcademicYearId == section.AcademicYearId
            && t.CourseId == section.CourseId
            && t.GroupId == section.GroupId
            && string.Equals(t.SectionCode, section.SectionCode, StringComparison.OrdinalIgnoreCase));
        if (collision)
            reasons.Add($"SectionCode '{section.SectionCode}' already exists under target Semester {ExpectedTargetSemesterId}.");

        var ok = reasons.Count == 0;
        return new SectionSemesterRemediationItemDto
        {
            SectionId = section.Id,
            SectionCode = section.SectionCode,
            SectionName = section.SectionName,
            TenantId = section.TenantId,
            CourseId = section.CourseId,
            GroupId = section.GroupId,
            AcademicYearId = section.AcademicYearId,
            CurrentSemesterId = section.SemesterId,
            TargetSemesterId = ExpectedTargetSemesterId,
            Status = section.Status,
            StatusKind = ok ? SectionSemesterRemediationStatus.Ready : SectionSemesterRemediationStatus.ManualReviewRequired,
            StatusCode = ok ? "READY" : "MANUAL_REVIEW_REQUIRED",
            Reason = ok
                ? "Validated; ready for Section.SemesterId remediation (Teaching Groups unchanged)."
                : string.Join(" ", reasons),
            MutationAllowed = ok,
            InApprovedSet = true,
            ReferencingTeachingGroupIds = tgIds,
            TeachingGroupSectionLinkCount = tgsLinkCount,
            CurrentStudentSectionCount = studentSectionCount,
        };
    }

    private async Task<List<int>> ResolveJournaledSectionIdsAsync(int tenantId, CancellationToken ct)
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
            var start = row.IndexOf("SectionIds=[", StringComparison.Ordinal);
            if (start < 0)
                continue;
            start += "SectionIds=[".Length;
            var end = row.IndexOf(']', start);
            if (end < 0)
                continue;
            foreach (var part in row[start..end].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, out var id) && id > 0)
                    ids.Add(id);
            }
        }

        return ids.OrderBy(x => x).ToList();
    }

    private SectionSemesterRemediationResultDto AbortPreview(
        int tenantId,
        bool mutate,
        string reason,
        List<string> notes,
        IReadOnlyList<int> approvedIds)
    {
        notes.Add($"ABORT: {reason}");
        return new SectionSemesterRemediationResultDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = tenantId,
            IsReadOnly = !mutate,
            ExecutionStatus = "Aborted",
            RolledBack = mutate,
            ExecutionSafe = false,
            LegacySemesterId = ExpectedLegacySemesterId,
            TargetSemesterId = ExpectedTargetSemesterId,
            AbortReason = reason,
            ApprovedSectionIds = approvedIds,
            Notes = notes,
            ConcurrencyResult = "None",
            TeachingGroupsUnchanged = true,
            TeachingGroupSectionsUnchanged = true,
        };
    }
}
