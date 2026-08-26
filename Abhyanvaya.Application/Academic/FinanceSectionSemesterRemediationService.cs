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
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3I —
/// Fail-closed Finance Section.SemesterId remediation (legacy 3 → Finance Sem 10).
/// Does not mutate TeachingGroup, TeachingGroupSection, Student, SA, TT, TimetableSection,
/// Attendance, or Semester ownership.
/// </summary>
public sealed class FinanceSectionSemesterRemediationService : IFinanceSectionSemesterRemediationService
{
    public const int ExpectedLegacySemesterId = 3;
    public const int ExpectedTargetSemesterId = 10;
    public const int ExpectedSemesterNumber = 3;
    public const int ExpectedFinanceGroupId = 1;
    public const int ExpectedCourseId = 1;
    public const string PromptCode = "P1-4-3I";
    public const string JournalDispositionCode = "FINANCE_SECTION_SEMESTER_REMAP";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<FinanceSectionSemesterRemediationService> _logger;

    public FinanceSectionSemesterRemediationService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ILogger<FinanceSectionSemesterRemediationService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    public Task<FinanceSectionRemediationResultDto> PreviewAsync(CancellationToken cancellationToken = default)
        => BuildAsync(mutate: false, cancellationToken);

    public async Task<FinanceSectionRemediationResultDto> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        FinanceSectionRemediationResultDto? result = null;
        try
        {
            await _db.ExecuteInTransactionAsync(async ct =>
            {
                result = await BuildAsync(mutate: true, ct);
                if (!string.Equals(result.ExecutionStatus, "Completed", StringComparison.Ordinal)
                    && !string.Equals(result.ExecutionStatus, "AlreadyComplete", StringComparison.Ordinal))
                {
                    throw new DomainException(result.AbortReason ?? "Finance Section remediation aborted.");
                }
            }, cancellationToken);
        }
        catch (ConcurrencyConflictException ex)
        {
            _logger.LogWarning(ex, "P1-4 Prompt 3I concurrency conflict.");
            return Aborted(result, ex.Message, "ConcurrencyConflictException");
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "P1-4 Prompt 3I EF concurrency conflict.");
            return Aborted(result, "Concurrency conflict while remediating Finance Sections.", "DbUpdateConcurrencyException");
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "P1-4 Prompt 3I Finance Section remediation aborted and rolled back.");
            return Aborted(result, ex.Message, result?.ConcurrencyResult);
        }

        return result ?? new FinanceSectionRemediationResultDto
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

    private FinanceSectionRemediationResultDto Aborted(
        FinanceSectionRemediationResultDto? result,
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
            TargetFinanceGroupId = result?.TargetFinanceGroupId,
            ChangedCount = 0,
            AlreadyCompleteCount = result?.AlreadyCompleteCount ?? 0,
            BlockedCount = result?.BlockedCount ?? 0,
            ManualReviewCount = result?.ManualReviewCount ?? 0,
            NotInScopeCount = result?.NotInScopeCount ?? 0,
            EligibleCount = result?.EligibleCount ?? 0,
            ApprovedSectionIds = result?.ApprovedSectionIds ?? [],
            Items = result?.Items ?? [],
            Notes = result?.Notes ?? [],
            AbortReason = reason,
            ConcurrencyResult = concurrency ?? "None",
            TransactionCommitted = false,
        };

    private async Task<FinanceSectionRemediationResultDto> BuildAsync(bool mutate, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var notes = new List<string>
        {
            mutate
                ? "Execution mode: Finance Section.SemesterId only (legacy 3 → Sem 10)."
                : "Read-only preview; zero writes.",
            $"Contract: legacy Sem={ExpectedLegacySemesterId} → Finance Sem={ExpectedTargetSemesterId}; Finance GroupId={ExpectedFinanceGroupId}.",
            "Identity is GroupId/Semester ownership — not Group name or Semester name.",
            "TeachingGroup / TeachingGroupSection / Student / SA / TT / TimetableSection / Attendance are not mutated.",
            "Does not re-execute Prompt 3F/3G.",
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

        var targetCandidates = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted
                        && s.Number == ExpectedSemesterNumber
                        && s.GroupId == ExpectedFinanceGroupId
                        && s.CourseId == ExpectedCourseId)
            .OrderBy(s => s.Id)
            .ToListAsync(ct);

        if (targetCandidates.Count == 0)
            return AbortPreview(tenantId, mutate, "Finance target Semester (GroupId=1, Number=3, CourseId=1) not found.", notes, []);
        if (targetCandidates.Count > 1)
        {
            return AbortPreview(
                tenantId, mutate,
                $"Multiple Finance Sem III candidates Ids=[{string.Join(",", targetCandidates.Select(s => s.Id))}]; fail closed.",
                notes, []);
        }

        var target = targetCandidates[0];
        if (target.Id != ExpectedTargetSemesterId)
        {
            return AbortPreview(
                tenantId, mutate,
                $"Finance Sem III resolved to Id={target.Id} but contract requires Id={ExpectedTargetSemesterId}; fail closed.",
                notes, []);
        }

        var financeGroup = await _db.Groups.AsNoTracking()
            .FirstOrDefaultAsync(g => g.TenantId == tenantId && !g.IsDeleted && g.Id == ExpectedFinanceGroupId, ct);
        if (financeGroup is null)
            return AbortPreview(tenantId, mutate, "Finance Group Id=1 not found.", notes, []);
        if (financeGroup.CourseId != ExpectedCourseId || financeGroup.CourseId != target.CourseId)
        {
            return AbortPreview(
                tenantId, mutate,
                $"Finance Group CourseId={financeGroup.CourseId} inconsistent with target CourseId={target.CourseId}.",
                notes, []);
        }

        var targetGroupId = ExpectedFinanceGroupId;
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

        var journaled = await ResolveJournaledSectionIdsAsync(tenantId, ct);
        var completeFilter = approvedOnLegacy.Concat(journaled).Distinct().ToList();

        var completeOnTarget = await _db.Sections.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted
                        && s.SemesterId == ExpectedTargetSemesterId
                        && s.CourseId == targetCourseId
                        && s.GroupId == targetGroupId
                        && completeFilter.Contains(s.Id))
            .OrderBy(s => s.Id)
            .ToListAsync(ct);

        // Also include Finance sections already on target that match course/group for AlreadyComplete reporting
        // when approvedOnLegacy is empty after first run — use journal or all Finance on target that were remapped.
        if (completeOnTarget.Count == 0 && journaled.Count > 0)
        {
            completeOnTarget = await _db.Sections.AsNoTracking()
                .Where(s => s.TenantId == tenantId && !s.IsDeleted
                            && s.SemesterId == ExpectedTargetSemesterId
                            && s.CourseId == targetCourseId
                            && s.GroupId == targetGroupId
                            && journaled.Contains(s.Id))
                .OrderBy(s => s.Id)
                .ToListAsync(ct);
        }

        var approvedIds = approvedOnLegacy
            .Concat(completeOnTarget.Select(s => s.Id))
            .Concat(journaled)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        if (approvedIds.Count == 0 && approvedOnLegacy.Count == 0 && completeOnTarget.Count == 0)
        {
            // Still report out-of-scope legacy sections
            notes.Add("No Finance Sections found on legacy Sem 3 or journaled as remapped.");
        }

        notes.Add($"ApprovedFinanceSectionIds=[{string.Join(",", approvedIds)}].");

        var tgLinks = await _db.SchedulingTeachingGroupSections.AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Select(x => new { x.SectionId, x.TeachingGroupId })
            .ToListAsync(ct);
        var tgBySection = tgLinks.GroupBy(x => x.SectionId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(x => x.TeachingGroupId).Distinct().OrderBy(x => x).ToList());

        var tgSemesters = await _db.SchedulingTeachingGroups.AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted)
            .Select(t => new { t.Id, t.SemesterId })
            .ToDictionaryAsync(t => t.Id, t => t.SemesterId, ct);

        var tgSemesterSnapshot = tgSemesters.ToDictionary(x => x.Key, x => x.Value);
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

        var items = new List<FinanceSectionRemediationItemDto>();
        var pending = new List<Section>();
        var already = 0;
        var blocked = 0;
        var manual = 0;
        var notInScope = 0;
        var eligible = 0;

        foreach (var section in legacySections)
        {
            var inApproved = approvedIds.Contains(section.Id)
                || (section.CourseId == targetCourseId && section.GroupId == targetGroupId);
            var item = Classify(
                section, targetCourseId, targetGroupId, inApproved, alreadyComplete: false,
                tgBySection.GetValueOrDefault(section.Id) ?? [],
                tgLinks.Count(x => x.SectionId == section.Id),
                studentCounts.GetValueOrDefault(section.Id),
                tgSemesters,
                targetCodes);
            items.Add(item);

            switch (item.StatusKind)
            {
                case FinanceSectionRemediationStatus.SafeToRemediate:
                    eligible++;
                    if (mutate)
                        pending.Add(section);
                    break;
                case FinanceSectionRemediationStatus.Blocked:
                    blocked++;
                    break;
                case FinanceSectionRemediationStatus.ManualReview:
                    manual++;
                    break;
                case FinanceSectionRemediationStatus.NotInScope:
                    notInScope++;
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
                tgSemesters,
                targetCodes);
            items.Add(item);
            if (item.StatusKind == FinanceSectionRemediationStatus.AlreadyComplete)
                already++;
            else
                manual++;
        }

        items = items.OrderBy(i => i.SectionId).ToList();

        var anyUnsafe = items.Any(i =>
            (i.InApprovedSet || i.StatusKind == FinanceSectionRemediationStatus.SafeToRemediate)
            && i.CurrentSemesterId == ExpectedLegacySemesterId
            && i.StatusKind is FinanceSectionRemediationStatus.Blocked
                or FinanceSectionRemediationStatus.ManualReview);

        var changed = 0;
        var affected = new List<int>();

        if (mutate && anyUnsafe)
        {
            return new FinanceSectionRemediationResultDto
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
                TargetFinanceGroupId = targetGroupId,
                ChangedCount = 0,
                AlreadyCompleteCount = already,
                BlockedCount = blocked,
                ManualReviewCount = manual,
                NotInScopeCount = notInScope,
                EligibleCount = eligible,
                ApprovedSectionIds = approvedIds,
                Items = items,
                Notes = notes,
                AbortReason =
                    "One or more approved Finance Sections are BLOCKED/MANUAL_REVIEW; batch rolled back (zero Section mutations).",
                ConcurrencyResult = "None",
                TransactionCommitted = false,
            };
        }

        if (mutate && pending.Count > 0)
        {
            var pendingIds = pending.Select(p => p.Id).OrderBy(x => x).ToList();
            if (!pendingIds.SequenceEqual(approvedOnLegacy))
            {
                throw new DomainException(
                    $"Approved Finance Section set drifted. Expected=[{string.Join(",", approvedOnLegacy)}] Actual=[{string.Join(",", pendingIds)}].");
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
                Evidence =
                    $"SectionIds=[{string.Join(",", affected)}]; legacy={ExpectedLegacySemesterId}; financeGroup={targetGroupId}; actor={_currentUser.UserId}",
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
                    throw new DomainException($"TeachingGroup Id={t.Id} SemesterId mutated; forbidden in Prompt 3I.");
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

        // If no finance work and only out-of-scope on legacy, AlreadyComplete when journal says done
        if (mutate && changed == 0 && approvedOnLegacy.Count == 0
            && journaled.Count > 0
            && approvedStates.All(s => s.SemesterId == ExpectedTargetSemesterId))
        {
            already = Math.Max(already, journaled.Count);
        }

        notes.Add($"Items={items.Count}; Eligible={eligible}; Changed={changed}; Already={already}; Blocked={blocked}; Manual={manual}; NotInScope={notInScope}.");

        string status;
        if (!mutate)
            status = "NotExecuted";
        else if (changed > 0)
            status = "Completed";
        else if (approvedStates.Count > 0 && approvedStates.All(s => s.SemesterId == ExpectedTargetSemesterId))
            status = "AlreadyComplete";
        else if (approvedOnLegacy.Count == 0 && journaled.Count > 0)
            status = "AlreadyComplete";
        else if (approvedOnLegacy.Count == 0 && eligible == 0 && !anyUnsafe)
            status = "AlreadyComplete";
        else
            status = "Aborted";

        return new FinanceSectionRemediationResultDto
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
            TargetFinanceGroupId = targetGroupId,
            ChangedCount = changed,
            AlreadyCompleteCount = already,
            BlockedCount = blocked,
            ManualReviewCount = manual,
            NotInScopeCount = notInScope,
            EligibleCount = eligible,
            ApprovedSectionIds = approvedIds,
            AffectedSectionIds = affected,
            Items = items,
            Notes = notes,
            ConcurrencyResult = "None",
            TransactionCommitted = mutate && (changed > 0 || status == "AlreadyComplete"),
        };
    }

    private FinanceSectionRemediationItemDto Classify(
        Section section,
        int targetCourseId,
        int targetGroupId,
        bool inApproved,
        bool alreadyComplete,
        IReadOnlyList<int> tgIds,
        int tgsLinkCount,
        int studentSectionCount,
        Dictionary<int, int> tgSemesters,
        List<(int Id, int AcademicYearId, int CourseId, int GroupId, string SectionCode)> targetCodes)
    {
        var currentClass = section.SemesterId == ExpectedLegacySemesterId
            ? "LEGACY_NULL_GROUP_SEM_III"
            : section.SemesterId == ExpectedTargetSemesterId
                ? "FINANCE_GROUP_SEM_III"
                : "OTHER";

        if (alreadyComplete)
        {
            var ok = section.CourseId == targetCourseId && section.GroupId == targetGroupId;
            return Item(section, targetGroupId, currentClass,
                ok ? FinanceSectionRemediationStatus.AlreadyComplete : FinanceSectionRemediationStatus.ManualReview,
                ok ? "Already on Finance Semester 10." : "On target Semester but Course/Group mismatch.",
                mutation: false, inApproved, tgIds, tgsLinkCount, studentSectionCount);
        }

        if (section.TenantId != _currentUser.TenantId)
        {
            return Item(section, targetGroupId, currentClass, FinanceSectionRemediationStatus.Blocked,
                "Cross-tenant Section; fail closed.", false, false, tgIds, tgsLinkCount, studentSectionCount);
        }

        if (section.GroupId != targetGroupId || section.CourseId != targetCourseId)
        {
            return Item(section, targetGroupId, currentClass, FinanceSectionRemediationStatus.NotInScope,
                $"Section GroupId={section.GroupId}/CourseId={section.CourseId} is not Finance GroupId={targetGroupId}/CourseId={targetCourseId}; out of Prompt 3I scope.",
                false, false, tgIds, tgsLinkCount, studentSectionCount);
        }

        var reasons = new List<string>();
        if (section.SemesterId != ExpectedLegacySemesterId)
            reasons.Add($"Current SemesterId={section.SemesterId} is not legacy {ExpectedLegacySemesterId}.");
        if (section.CourseId != targetCourseId)
            reasons.Add($"Section CourseId={section.CourseId} != target CourseId={targetCourseId}.");
        if (section.GroupId != targetGroupId)
            reasons.Add($"Section GroupId={section.GroupId} != Finance GroupId={targetGroupId}.");
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
            reasons.Add($"SectionCode '{section.SectionCode}' already exists under Finance Sem {ExpectedTargetSemesterId}.");

        // Teaching Group dependency: TG Sem must already equal target, else BLOCK (do not mutate TG).
        foreach (var tgId in tgIds)
        {
            if (!tgSemesters.TryGetValue(tgId, out var tgSem))
            {
                reasons.Add($"TeachingGroup Id={tgId} missing; fail closed.");
                continue;
            }

            if (tgSem != ExpectedTargetSemesterId)
            {
                reasons.Add(
                    $"TeachingGroup Id={tgId} SemesterId={tgSem} != Finance target {ExpectedTargetSemesterId}; TG frozen — BLOCK (do not mutate TG).");
            }
        }

        if (reasons.Count > 0)
        {
            var kind = tgIds.Count > 0 && reasons.Any(r => r.Contains("TeachingGroup", StringComparison.Ordinal))
                ? FinanceSectionRemediationStatus.Blocked
                : FinanceSectionRemediationStatus.ManualReview;
            return Item(section, targetGroupId, currentClass, kind, string.Join(" ", reasons),
                false, true, tgIds, tgsLinkCount, studentSectionCount);
        }

        return Item(section, targetGroupId, currentClass, FinanceSectionRemediationStatus.SafeToRemediate,
            "Validated; ready for Finance Section.SemesterId remediation.",
            true, true, tgIds, tgsLinkCount, studentSectionCount);
    }

    private static FinanceSectionRemediationItemDto Item(
        Section section,
        int targetGroupId,
        string currentClass,
        FinanceSectionRemediationStatus kind,
        string reason,
        bool mutation,
        bool inApproved,
        IReadOnlyList<int> tgIds,
        int tgsLinkCount,
        int studentSectionCount)
        => new()
        {
            SectionId = section.Id,
            SectionCode = section.SectionCode,
            SectionName = section.SectionName,
            TenantId = section.TenantId,
            CourseId = section.CourseId,
            GroupId = section.GroupId,
            AcademicYearId = section.AcademicYearId,
            CurrentSemesterId = section.SemesterId,
            CurrentSemesterClassification = currentClass,
            TargetFinanceGroupId = targetGroupId,
            TargetSemesterId = ExpectedTargetSemesterId,
            TargetSemesterNumber = ExpectedSemesterNumber,
            Status = section.Status,
            StatusKind = kind,
            StatusCode = kind switch
            {
                FinanceSectionRemediationStatus.SafeToRemediate => "SAFE_TO_REMEDIATE",
                FinanceSectionRemediationStatus.AlreadyComplete => "ALREADY_COMPLETE",
                FinanceSectionRemediationStatus.Blocked => "BLOCKED",
                FinanceSectionRemediationStatus.ManualReview => "MANUAL_REVIEW",
                _ => "NOT_IN_SCOPE",
            },
            Reason = reason,
            MutationAllowed = mutation,
            InApprovedSet = inApproved,
            ReferencingTeachingGroupIds = tgIds,
            TeachingGroupSectionLinkCount = tgsLinkCount,
            CurrentStudentSectionCount = studentSectionCount,
        };

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

    private FinanceSectionRemediationResultDto AbortPreview(
        int tenantId,
        bool mutate,
        string reason,
        List<string> notes,
        IReadOnlyList<int> approvedIds)
    {
        notes.Add($"ABORT: {reason}");
        return new FinanceSectionRemediationResultDto
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
        };
    }
}
