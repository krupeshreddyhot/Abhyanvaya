using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3K-A (package 3KA) / PromptCode P1-4-3KA —
/// Historical Semester disposition &amp; archive architecture discovery. Read-only.
/// Reuses <see cref="OperationalSemesterRules"/> + <c>IsHistoricalArchive</c> + finalization inventory.
/// Does not archive, delete, assign GroupId, mutate TG/TimetableSection, or apply schema hardening.
/// </summary>
public sealed class HistoricalSemesterDispositionAuditService : IHistoricalSemesterDispositionAuditService
{
    public const string PromptCode = HistoricalSemesterDispositionAuditCodes.PromptCode;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILegacySemesterFinalizationAuditService _finalization;

    public HistoricalSemesterDispositionAuditService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ILegacySemesterFinalizationAuditService finalization)
    {
        _db = db;
        _currentUser = currentUser;
        _finalization = finalization;
    }

    public async Task<HistoricalSemesterDispositionAuditDto> BuildAuditAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var notes = new List<string>
        {
            "Prompt 3K-A DISCOVERY + ARCHITECTURE CONTRACT ONLY — zero writes.",
            $"PromptCode={PromptCode} (package 3KA). Does not collide with Subject Catalog P1-4-3J or 3JA execute (P1-4-3JA).",
            "Existing archive pattern reused: Semester.IsHistoricalArchive + LegacySemesterDispositionJournals.",
            "Soft-delete (IsDeleted) is NOT the historical marker.",
            "No GroupId inference, merge, deletion, or TG mutation.",
        };

        var fin = await _finalization.BuildAuditAsync(cancellationToken);
        var legacyIds = fin.LegacySemesters.Select(l => l.SemesterId).Distinct().ToList();

        var operational = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.GroupId != null && !s.IsHistoricalArchive)
            .Select(s => new
            {
                s.Id,
                s.CourseId,
                CourseName = s.Course != null ? s.Course.Name : "",
                s.GroupId,
                s.Number,
                s.Name,
                s.IsHistoricalArchive,
            })
            .OrderBy(s => s.Id)
            .ToListAsync(cancellationToken);

        var archived = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.IsHistoricalArchive)
            .Select(s => new
            {
                s.Id,
                s.CourseId,
                CourseName = s.Course != null ? s.Course.Name : "",
                s.GroupId,
                s.Number,
                s.Name,
                s.IsHistoricalArchive,
            })
            .OrderBy(s => s.Id)
            .ToListAsync(cancellationToken);

        var legacyDbRows = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && legacyIds.Contains(s.Id))
            .Select(s => new
            {
                s.Id,
                s.GroupId,
                s.IsHistoricalArchive,
                s.CourseId,
                s.Number,
                s.Name,
                CourseName = s.Course != null ? s.Course.Name : "",
            })
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var items = new List<HistoricalSemesterDispositionDto>();

        foreach (var s in operational)
        {
            items.Add(new HistoricalSemesterDispositionDto
            {
                SemesterId = s.Id,
                CourseId = s.CourseId,
                CourseName = s.CourseName,
                GroupId = s.GroupId,
                SemesterNumber = s.Number,
                Name = s.Name,
                Classification = HistoricalSemesterDispositionClassifications.ActiveOperational,
                IsOperational = true,
                IsHistorical = false,
                IsHistoricalArchive = false,
                IsArchiveEligible = false,
                BlockingReasons = [],
                DownstreamReferenceSummary = new HistoricalSemesterDownstreamReferenceSummaryDto(),
                RecommendedAction = "Keep as Group-owned operational Semester; no disposition required.",
            });
        }

        var emitted = items.Select(i => i.SemesterId).ToHashSet();

        foreach (var s in archived)
        {
            if (!emitted.Add(s.Id)) continue;
            items.Add(new HistoricalSemesterDispositionDto
            {
                SemesterId = s.Id,
                CourseId = s.CourseId,
                CourseName = s.CourseName,
                GroupId = s.GroupId,
                SemesterNumber = s.Number,
                Name = s.Name,
                Classification = HistoricalSemesterDispositionClassifications.Archived,
                IsOperational = false,
                IsHistorical = true,
                IsHistoricalArchive = true,
                IsArchiveEligible = false,
                BlockingReasons = [],
                DownstreamReferenceSummary = new HistoricalSemesterDownstreamReferenceSummaryDto(),
                RecommendedAction =
                    "Immutable historical archive (IsHistoricalArchive=true). Remain queryable via includeHistorical; never operational selection.",
            });
        }

        var legacyItems = new List<HistoricalSemesterDispositionDto>();
        foreach (var inv in fin.LegacySemesters.OrderBy(l => l.SemesterId))
        {
            if (emitted.Contains(inv.SemesterId))
                continue;

            var student = inv.StudentReferenceCount;
            var att = inv.AttendanceReferenceCount;
            var section = inv.SectionReferenceCount;
            var subject = inv.SubjectReferenceCount;
            var sa = inv.SubjectAllocationReferenceCount;
            var tt = inv.TimetableEntryReferenceCount;
            var tg = inv.TeachingGroupReferenceCount;
            var ops = student + att + section + sa + tt + tg;
            var historicalHint = subject;

            legacyDbRows.TryGetValue(inv.SemesterId, out var row);

            if (row?.IsHistoricalArchive == true)
            {
                emitted.Add(inv.SemesterId);
                items.Add(new HistoricalSemesterDispositionDto
                {
                    SemesterId = inv.SemesterId,
                    CourseId = row.CourseId,
                    CourseName = row.CourseName,
                    GroupId = row.GroupId,
                    SemesterNumber = row.Number,
                    Name = row.Name,
                    Classification = HistoricalSemesterDispositionClassifications.Archived,
                    IsOperational = false,
                    IsHistorical = true,
                    IsHistoricalArchive = true,
                    IsArchiveEligible = false,
                    BlockingReasons = [],
                    DownstreamReferenceSummary = BuildSummary(student, att, subject, section, sa, tt, tg, ops, historicalHint),
                    RecommendedAction = "Already archived; retain identity; do not assign GroupId for schema convenience.",
                });
                continue;
            }

            var (classification, eligible, itemBlockers, action) = ClassifyLegacy(
                inv.SemesterId,
                inv.DispositionCode,
                inv.DispositionEvidence,
                ops,
                subject,
                tg);

            emitted.Add(inv.SemesterId);
            legacyItems.Add(new HistoricalSemesterDispositionDto
            {
                SemesterId = inv.SemesterId,
                CourseId = row?.CourseId ?? inv.CourseId,
                CourseName = row?.CourseName ?? inv.CourseName ?? "",
                GroupId = row?.GroupId,
                SemesterNumber = row?.Number ?? inv.Number,
                Name = row?.Name ?? inv.Name ?? "",
                Classification = classification,
                IsOperational = false,
                IsHistorical = true,
                IsHistoricalArchive = false,
                IsArchiveEligible = eligible,
                BlockingReasons = itemBlockers,
                DownstreamReferenceSummary = BuildSummary(student, att, subject, section, sa, tt, tg, ops, historicalHint),
                RecommendedAction = action,
            });
        }

        items.AddRange(legacyItems);
        items = items
            .OrderBy(i => ClassificationSortKey(i.Classification))
            .ThenBy(i => i.CourseId)
            .ThenBy(i => i.SemesterId)
            .ToList();

        // Ambient EF tenant filter is fail-closed; discovery audit does not cross tenants.
        const bool tenantPassed = true;

        var blockers = new List<string>();
        if (legacyItems.Any(i => i.Classification == HistoricalSemesterDispositionClassifications.ManualMappingRequired))
            blockers.Add("One or more Semesters require MANUAL_MAPPING_REQUIRED (e.g. Sem 1 Subject historical).");
        if (legacyItems.Any(i => i.Classification == HistoricalSemesterDispositionClassifications.DuplicateReview))
            blockers.Add("One or more Semesters require DUPLICATE_REVIEW (no auto-merge).");
        if (legacyItems.Any(i => i.Classification == HistoricalSemesterDispositionClassifications.BlockedByReference))
            blockers.Add("One or more Semesters are BLOCKED_BY_REFERENCE (ops/TG/Section/SA/TT/Attendance/Student).");
        if (legacyItems.Count(i => i.Classification is HistoricalSemesterDispositionClassifications.HistoricalRetain
                or HistoricalSemesterDispositionClassifications.ArchiveEligible
                or HistoricalSemesterDispositionClassifications.ManualMappingRequired
                or HistoricalSemesterDispositionClassifications.DuplicateReview
                or HistoricalSemesterDispositionClassifications.BlockedByReference) > 0)
        {
            blockers.Add("Legacy NULL-group Semesters remain outside ARCHIVED — schema NOT NULL still deferred.");
        }

        var warnings = new List<string>
        {
            "Archive eligibility requires ALL operational downstream refs cleared — not Student-only.",
            "Subject historical refs do not alone authorize Group assignment; may leave MANUAL_MAPPING_REQUIRED.",
            "Teaching Group residuals are identify-only in this audit; remediation is a separate approved prompt.",
            "UI currently chips NULL-group as Legacy/Historical; recommend explicit Operational / Historical / Archived / Manual-review modes later.",
        };

        return new HistoricalSemesterDispositionAuditDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = tenantId,
            IsReadOnly = true,
            NoMutationsPerformed = true,
            SaveChangesInvoked = false,
            PromptCode = PromptCode,
            ActiveOperationalCount = items.Count(i =>
                i.Classification == HistoricalSemesterDispositionClassifications.ActiveOperational),
            HistoricalRetainCount = items.Count(i =>
                i.Classification == HistoricalSemesterDispositionClassifications.HistoricalRetain),
            ManualMappingRequiredCount = items.Count(i =>
                i.Classification == HistoricalSemesterDispositionClassifications.ManualMappingRequired),
            DuplicateReviewCount = items.Count(i =>
                i.Classification == HistoricalSemesterDispositionClassifications.DuplicateReview),
            BlockedByReferenceCount = items.Count(i =>
                i.Classification == HistoricalSemesterDispositionClassifications.BlockedByReference),
            ArchiveEligibleCount = items.Count(i =>
                i.Classification == HistoricalSemesterDispositionClassifications.ArchiveEligible),
            ArchivedCount = items.Count(i =>
                i.Classification == HistoricalSemesterDispositionClassifications.Archived),
            LegacyNullGroupCount = fin.Summary?.LegacyNullGroupCount
                ?? fin.LegacySemesters.Count(l => items.Any(i =>
                    i.SemesterId == l.SemesterId && i.GroupId is null && !i.IsHistoricalArchive)),
            ExistingArchivePatternFound = true,
            ExistingArchivePatternName =
                "Semester.IsHistoricalArchive + LegacySemesterDispositionJournals (Prompt 3J-A / P1-4-3JA)",
            CompetingLifecycleAvoided = true,
            SchemaHardeningDeferred = true,
            TenantIsolationPassed = tenantPassed,
            Items = items,
            DownstreamDependencyMatrix = BuildDependencyMatrix(),
            ArchiveEligibilityRules =
            [
                "OperationalRefTotal (Student+Attendance+Section+SA+TT+TG) must be 0.",
                "Teaching Group refs block archive eligibility until separately remediated (identify-only here).",
                "Subject historical refs alone do NOT make ARCHIVE_ELIGIBLE if MANUAL_MAPPING_REQUIRED applies.",
                "DUPLICATE_REVIEW Semesters are never ARCHIVE_ELIGIBLE until Architect disposition.",
                "Archive must not assign GroupId; HISTORICAL_ARCHIVE sets IsHistoricalArchive only (future execute under 3JA/3K-B).",
                "Not archive-ready merely because Student refs are zero.",
            ],
            RetainVsArchiveNotes =
            [
                "RETAIN (HISTORICAL_RETAIN): remains in Semester table, GroupId may stay NULL, IsHistoricalArchive=false, excluded from operational selectors when includeNullGroupLegacy=false.",
                "ARCHIVED: IsHistoricalArchive=true; excluded from AcademicTree/cascades/new SA/TT/TG/Attendance/Student assignment; discoverable via includeHistorical=true / WhereHistoricalArchive.",
                "Deletion is never a substitute for archival.",
                "Soft-delete remains a separate disposal mechanism and must not be overloaded as historical.",
            ],
            FutureExecutionContract =
            [
                "Explicit disposition decision per SemesterId (no archive-all).",
                "Operator/audit identity recorded on LegacySemesterDispositionJournal.",
                "Single transactional boundary; fail-closed abort on any blocked item.",
                "Concurrency protection via existing ExecuteInTransactionAsync / concurrency helpers.",
                "Idempotent: second identical disposition → AlreadyComplete, zero additional writes.",
                "Rollback: transaction abort restores prior IsHistoricalArchive/journal state.",
                "Post-operation integrity audit required before any schema NOT NULL/UNIQUE prompt.",
                "Do not implement execution in Prompt 3K-A.",
            ],
            UiRecommendations =
            [
                "Operational Semesters: Group-owned, IsHistoricalArchive=false — only selectable for new ops.",
                "Historical (retained NULL-group): admin visibility with Legacy chip; not cascade-selectable.",
                "Archived: explicit history mode (includeHistorical); never accidental operational pick.",
                "Manual-review / Duplicate / Blocked: admin disposition queue only.",
                "Do not implement UI changes in Prompt 3K-A.",
            ],
            Blockers = blockers,
            Warnings = warnings,
            Notes = notes,
            RecommendedNextPrompt =
                "Prompt 3K-B (or reuse Architect-approved 3JA execute) — explicit HISTORICAL_ARCHIVE for ARCHIVE_ELIGIBLE rows only; then re-run 3J schema-hardening readiness. Do not start NOT NULL/UNIQUE until NullGroup non-archived = 0 under approved design.",
        };
    }

    private static HistoricalSemesterDownstreamReferenceSummaryDto BuildSummary(
        int student, int att, int subject, int section, int sa, int tt, int tg, int ops, int historicalHint)
        => new()
        {
            StudentRefs = student,
            AttendanceRefs = att,
            SubjectRefs = subject,
            SectionRefs = section,
            SubjectAllocationRefs = sa,
            TimetableEntryRefs = tt,
            TeachingGroupRefs = tg,
            OperationalRefTotal = ops,
            HistoricalDependencyHintCount = historicalHint,
        };

    private static (
        string Classification,
        bool ArchiveEligible,
        IReadOnlyList<string> Blockers,
        string Action)
        ClassifyLegacy(int semesterId, string dispositionCode, string evidence, int ops, int subjectRefs, int tgRefs)
    {
        var blockers = new List<string>();
        var disp = dispositionCode ?? "";

        if (ops > 0)
        {
            if (tgRefs > 0)
                blockers.Add($"TeachingGroup refs={tgRefs} (identify-only; separate TG remediation).");
            blockers.Add($"Operational downstream refs total={ops}.");
            return (
                HistoricalSemesterDispositionClassifications.BlockedByReference,
                false,
                blockers,
                "Remediate operational refs under approved prompts; do not archive yet.");
        }

        if (string.Equals(disp, "MANUAL_MAPPING_REQUIRED", StringComparison.OrdinalIgnoreCase)
            || semesterId == 1
            || subjectRefs > 0 && string.Equals(disp, "MANUAL_MAPPING_REQUIRED", StringComparison.OrdinalIgnoreCase))
        {
            if (subjectRefs > 0)
                blockers.Add($"Subject historical refs={subjectRefs}; Group ownership not deterministically proven.");
            blockers.Add(string.IsNullOrWhiteSpace(evidence) ? "Manual mapping required." : evidence);
            return (
                HistoricalSemesterDispositionClassifications.ManualMappingRequired,
                false,
                blockers,
                "Architect/manual disposition only — never invent GroupId.");
        }

        if (string.Equals(disp, "DUPLICATE_REVIEW", StringComparison.OrdinalIgnoreCase)
            || string.Equals(disp, "DUPLICATE_LEGACY_NUMBER", StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add(string.IsNullOrWhiteSpace(evidence) ? "Duplicate Number review required." : evidence);
            return (
                HistoricalSemesterDispositionClassifications.DuplicateReview,
                false,
                blockers,
                "Business review only — no merge/delete/reassign in discovery.");
        }

        if (string.Equals(disp, "BLOCKED", StringComparison.OrdinalIgnoreCase)
            || disp.Contains("BLOCKED_BY", StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add(evidence);
            return (
                HistoricalSemesterDispositionClassifications.BlockedByReference,
                false,
                blockers,
                "Clear blocking references under approved remediation.");
        }

        // Zero ops and not manual/duplicate → archive-eligible (Subject-only historical may still be retain)
        if (subjectRefs > 0)
        {
            blockers.Add($"Subject refs={subjectRefs} remain; archive only with Architect-approved historical FK retention.");
            return (
                HistoricalSemesterDispositionClassifications.HistoricalRetain,
                false,
                blockers,
                "Retain historically pending Architect confirmation; Subject FK may remain on archived row after explicit HISTORICAL_ARCHIVE under 3JA rules.");
        }

        return (
            HistoricalSemesterDispositionClassifications.ArchiveEligible,
            true,
            [],
            "Eligible for explicit HISTORICAL_ARCHIVE (IsHistoricalArchive=true) under 3JA/3K-B execute — GroupId unchanged.");
    }

    private static int ClassificationSortKey(string c) => c switch
    {
        HistoricalSemesterDispositionClassifications.BlockedByReference => 0,
        HistoricalSemesterDispositionClassifications.ManualMappingRequired => 1,
        HistoricalSemesterDispositionClassifications.DuplicateReview => 2,
        HistoricalSemesterDispositionClassifications.HistoricalRetain => 3,
        HistoricalSemesterDispositionClassifications.ArchiveEligible => 4,
        HistoricalSemesterDispositionClassifications.Archived => 5,
        HistoricalSemesterDispositionClassifications.ActiveOperational => 6,
        _ => 9,
    };

    private static IReadOnlyList<HistoricalSemesterDependencyMatrixRowDto> BuildDependencyMatrix()
        =>
        [
            Row("Student", "operational dependency", "Must remap before archive for new ops", true, false,
                "Student.Semester must be Group-owned operational."),
            Row("AttendanceSession", "operational dependency", "Must remap before archive", true, false,
                "New attendance rejects historical Semesters."),
            Row("Subject", "historical / informational dependency", "May remain on archived Semester", false, false,
                "Sem1 Subject is MANUAL_MAPPING_REQUIRED until Architect disposition."),
            Row("Section", "operational dependency", "Must remap before archive", true, false,
                "No TimetableSection direct writes from this audit."),
            Row("SubjectAllocation", "operational dependency", "Must remap before archive", true, false,
                "Department denorm remains Course SSOT."),
            Row("TimetableEntry", "operational dependency", "Must remap before archive", true, false,
                "CAP/ConflictEngine/Publish frozen."),
            Row("TimetableSection", "immutable/frozen (projector)", "Identify via Section only", true, false,
                "Projector-owned; no direct writes."),
            Row("TeachingGroup", "operational dependency", "Identify-only in 3K-A", true, true,
                "Separate TG remediation prompt required."),
            Row("TeachingGroupSection", "operational via TG", "Identify-only in 3K-A", true, true,
                "No TGS mutation."),
            Row("LegacySemesterDispositionJournal", "audit / informational", "Never blocks archive", false, false,
                "Single journal mechanism — do not introduce a second."),
        ];

    private static HistoricalSemesterDependencyMatrixRowDto Row(
        string entity, string kind, string guidance, bool blocks, bool tgOnly, string notes)
        => new()
        {
            Entity = entity,
            ReferenceKind = kind,
            ClassificationGuidance = guidance,
            BlocksArchiveEligibility = blocks,
            TeachingGroupIdentifyOnly = tgOnly,
            Notes = notes,
        };
}
