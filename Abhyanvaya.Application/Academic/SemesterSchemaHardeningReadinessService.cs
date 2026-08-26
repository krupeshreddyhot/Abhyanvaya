using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3J (Architect package 3J3) / PromptCode P1-4-3J3 —
/// Formal GO/NO-GO schema-hardening readiness audit. Zero mutations. Zero schema DDL.
/// Reuses disposition journals + finalization inventory; does not compete with 3D/3E/3H frameworks.
/// PromptCode avoids Subject Catalog remediation (P1-4-3J) and prior package 3J1 (P1-4-3M).
/// </summary>
public sealed class SemesterSchemaHardeningReadinessService : ISemesterSchemaHardeningReadinessService
{
    public const string PromptCode = "P1-4-3J3";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILegacySemesterFinalizationAuditService _finalization;

    public SemesterSchemaHardeningReadinessService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ILegacySemesterFinalizationAuditService finalization)
    {
        _db = db;
        _currentUser = currentUser;
        _finalization = finalization;
    }

    public async Task<SemesterSchemaHardeningReadinessResult> BuildAsync(
        CancellationToken cancellationToken = default)
    {
        var notes = new List<string>
        {
            "Prompt 3J FINAL SCHEMA HARDENING READINESS — AUDIT + CONTRACT ONLY (package 3J3).",
            "PromptCode=P1-4-3J3 (Subject Catalog remediation owns P1-4-3J; prior 3J1 used P1-4-3M).",
            "Zero SaveChanges. Zero DDL. Zero TG/Section/Attendance/SA/TT mutation.",
            "Tenant scope: EF global filter (ambient tenant, or SuperAdmin cross-tenant when Role=SuperAdmin).",
        };

        var fin = await _finalization.BuildAuditAsync(cancellationToken);

        var semesters = await _db.Semesters.AsNoTracking()
            .Select(s => new { s.Id, s.TenantId, s.CourseId, s.GroupId, s.Number, s.Name, s.IsDeleted, s.IsHistoricalArchive })
            .ToListAsync(cancellationToken);

        var groups = await _db.Groups.AsNoTracking()
            .Select(g => new { g.Id, g.TenantId, g.CourseId, g.IsDeleted })
            .ToDictionaryAsync(g => g.Id, cancellationToken);

        var courses = await _db.Courses.AsNoTracking()
            .Select(c => new { c.Id, c.TenantId, c.IsDeleted })
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var tenantCount = semesters.Select(s => s.TenantId).Distinct().Count();
        if (tenantCount == 0)
            tenantCount = _currentUser.TenantId > 0 ? 1 : 0;

        var findings = new List<SemesterSchemaHardeningFindingDto>();
        var warnings = new List<string>
        {
            "Soft-deleted Semesters are excluded by EF global IsDeleted filter. Eventual UNIQUE should be a filtered index (WHERE IsDeleted=0) and DBA must scan soft-deleted NULL GroupId rows before ALTER NOT NULL.",
        };

        var nullGroup = semesters.Where(s => s.GroupId is null).OrderBy(s => s.Id).ToList();
        var groupSpecific = semesters.Where(s => s.GroupId is not null).ToList();

        // Ownership / course consistency / cross-tenant
        var invalidOwnership = 0;
        var crossTenant = 0;
        foreach (var s in groupSpecific)
        {
            if (!groups.TryGetValue(s.GroupId!.Value, out var g) || g.IsDeleted)
            {
                invalidOwnership++;
                findings.Add(Finding("SEMESTER_GROUP_MISSING", SemesterSchemaHardeningFindingSeverity.Critical,
                    "Semester", s.Id, s.TenantId,
                    $"GroupId={s.GroupId}", "Existing same-tenant Group",
                    "Group missing or deleted.",
                    "Restore Group or remap Semester under approved remediation.",
                    "Academic/Semester"));
                continue;
            }

            if (g.TenantId != s.TenantId)
            {
                crossTenant++;
                findings.Add(Finding("CROSS_TENANT_SEMESTER_GROUP", SemesterSchemaHardeningFindingSeverity.Critical,
                    "Semester", s.Id, s.TenantId,
                    $"Semester.TenantId={s.TenantId}; Group.TenantId={g.TenantId}",
                    "Same TenantId",
                    "Semester→Group cross-tenant relationship.",
                    "Remediate cross-tenant FK under Architect-approved prompt.",
                    "Security/TenantIsolation"));
            }

            if (!courses.TryGetValue(s.CourseId, out var c) || c.IsDeleted)
            {
                invalidOwnership++;
                findings.Add(Finding("SEMESTER_COURSE_MISSING", SemesterSchemaHardeningFindingSeverity.Critical,
                    "Semester", s.Id, s.TenantId,
                    $"CourseId={s.CourseId}", "Existing Course",
                    "Course missing or deleted.",
                    "Restore Course or remap Semester.",
                    "Academic/Semester"));
                continue;
            }

            if (c.TenantId != s.TenantId)
            {
                crossTenant++;
                findings.Add(Finding("CROSS_TENANT_SEMESTER_COURSE", SemesterSchemaHardeningFindingSeverity.Critical,
                    "Semester", s.Id, s.TenantId,
                    $"Semester.TenantId={s.TenantId}; Course.TenantId={c.TenantId}",
                    "Same TenantId",
                    "Semester→Course cross-tenant relationship.",
                    "Remediate cross-tenant FK under Architect-approved prompt.",
                    "Security/TenantIsolation"));
            }

            if (g.CourseId != s.CourseId)
            {
                invalidOwnership++;
                findings.Add(Finding("SEMESTER_COURSE_GROUP_MISMATCH", SemesterSchemaHardeningFindingSeverity.Critical,
                    "Semester", s.Id, s.TenantId,
                    $"Semester.CourseId={s.CourseId}; Group.CourseId={g.CourseId}",
                    "Semester.CourseId == Group.CourseId",
                    "Course denormalization does not match Group ownership.",
                    "Correct CourseId from Group or remap under approved remediation.",
                    "Academic/Semester"));
            }
        }

        // Duplicates among Group-specific (non-deleted visible rows)
        var duplicates = groupSpecific
            .GroupBy(s => new { s.TenantId, GroupId = s.GroupId!.Value, s.Number })
            .Where(g => g.Count() > 1)
            .Select(g => new DuplicateSemesterKeyRowDto
            {
                TenantId = g.Key.TenantId,
                GroupId = g.Key.GroupId,
                Number = g.Key.Number,
                SemesterIds = g.Select(x => x.Id).OrderBy(x => x).ToList(),
            })
            .OrderBy(d => d.TenantId).ThenBy(d => d.GroupId).ThenBy(d => d.Number)
            .ToList();

        foreach (var d in duplicates)
        {
            findings.Add(Finding("SEMESTER_DUPLICATE_GROUP_NUMBER", SemesterSchemaHardeningFindingSeverity.Critical,
                "Semester", d.SemesterIds.FirstOrDefault(), d.TenantId,
                $"Ids=[{string.Join(",", d.SemesterIds)}] Tenant={d.TenantId} Group={d.GroupId} Number={d.Number}",
                "UNIQUE(TenantId, GroupId, Number)",
                "Duplicate Group-specific Semester numbers.",
                "Merge/retire duplicates under Architect-approved remediation before UNIQUE.",
                "Academic/Semester"));
        }

        // Journals for disposition
        var journals = await _db.LegacySemesterDispositionJournals.AsNoTracking()
            .Where(j => !j.IsDeleted)
            .Select(j => new JournalSnap(j.SemesterId, j.DispositionCode, j.PromptCode, j.Evidence, j.FinalizedUtc))
            .ToListAsync(cancellationToken);
        var latestJournalBySem = journals
            .GroupBy(j => j.SemesterId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.FinalizedUtc).First());

        var finById = fin.LegacySemesters.ToDictionary(r => r.SemesterId);

        var nullRows = new List<NullGroupSemesterAuditRowDto>();
        foreach (var s in nullGroup)
        {
            var (disp, code, evidence) = ClassifyNullGroup(s.Id, finById, latestJournalBySem);
            if (disp == NullGroupSemesterDisposition.Unexplained)
            {
                findings.Add(Finding("SEMESTER_NULL_GROUP_UNEXPLAINED", SemesterSchemaHardeningFindingSeverity.Critical,
                    "Semester", s.Id, s.TenantId,
                    "GroupId=NULL without disposition",
                    "Explicit disposition (RETAIN/MANUAL/BLOCKED/…)",
                    "Unexplained NULL-group Semester.",
                    "Assign Architect disposition or remediate under approved prompt.",
                    "Academic/LegacyDisposition"));
            }
            else
            {
                findings.Add(Finding("SEMESTER_NULL_GROUP", SemesterSchemaHardeningFindingSeverity.Critical,
                    "Semester", s.Id, s.TenantId,
                    "GroupId=NULL",
                    "GroupId NOT NULL (operational model)",
                    $"NULL-group Semester disposition={code}. {evidence}",
                    "Map to Group, archive under approved historical model, or exclude via Architect-approved schema design before NOT NULL.",
                    "Academic/Semester"));
            }

            nullRows.Add(new NullGroupSemesterAuditRowDto
            {
                TenantId = s.TenantId,
                SemesterId = s.Id,
                Number = s.Number,
                Name = s.Name,
                CourseId = s.CourseId,
                GroupId = null,
                Disposition = disp,
                DispositionCode = code,
                Evidence = evidence,
                BlocksNotNull = true,
                IsHistoricalArchive = s.IsHistoricalArchive,
            });
        }
        nullRows = nullRows.OrderBy(r => r.TenantId).ThenBy(r => r.SemesterId).ToList();

        var nullIds = nullGroup.Select(s => s.Id).ToHashSet();

        // Downstream refs on NULL-group Semesters
        var downstream = new List<DownstreamLegacyReferenceRowDto>();
        await CollectDownstreamAsync(downstream, nullGroup.Select(s => (s.Id, s.TenantId, s.Number, s.CourseId)).ToList(), cancellationToken);

        var downstreamLegacyCount = downstream.Sum(d => d.ReferenceCount);
        foreach (var row in nullRows)
        {
            row.DownstreamReferenceCount = downstream
                .Where(d => d.SemesterId == row.SemesterId)
                .Sum(d => d.ReferenceCount);
        }
        foreach (var d in downstream.Where(x =>
                     x.ReferenceEntity is "Student" or "AttendanceSession" or "Section"
                         or "SubjectAllocation" or "TimetableEntry" or "TeachingGroup"
                         or "TeachingGroupSection" or "StudentSection" or "TimetableSection"))
        {
            if (d.ReferenceCount <= 0) continue;
            findings.Add(Finding("DOWNSTREAM_LEGACY_REFERENCE", SemesterSchemaHardeningFindingSeverity.Critical,
                d.ReferenceEntity, d.SemesterId, d.TenantId,
                $"{d.ReferenceCount} refs → Sem {d.SemesterId}",
                "Zero operational refs to NULL-group Semesters",
                d.BlockingReason,
                "Run the owning remediation prompt; do not weaken TG/CAP.",
                d.ReferenceEntity));
        }

        // Include Subject historical refs in blocking findings (still prevents NullGroup=0 GO path)
        foreach (var d in downstream.Where(x => x.ReferenceEntity == "Subject" && x.ReferenceCount > 0))
        {
            findings.Add(Finding("DOWNSTREAM_LEGACY_REFERENCE", SemesterSchemaHardeningFindingSeverity.Critical,
                d.ReferenceEntity, d.SemesterId, d.TenantId,
                $"{d.ReferenceCount} Subject refs → Sem {d.SemesterId}",
                "Zero refs to NULL-group Semesters (or exclude Semesters from operational model under Architect prompt)",
                d.BlockingReason,
                "Subject Catalog remediation / formal historical retention excluding FK before NOT NULL.",
                "Academic/Subject"));
        }
        // TG / Section boundary
        var tgLegacy = await _db.SchedulingTeachingGroups.AsNoTracking()
            .Where(t => !t.IsDeleted && nullIds.Contains(t.SemesterId))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var sectionLegacy = await _db.Sections.AsNoTracking()
            .Where(s => !s.IsDeleted && nullIds.Contains(s.SemesterId))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var tsLegacyViaSection = downstream
            .Where(d => d.ReferenceEntity == "TimetableSection")
            .Sum(d => d.ReferenceCount);
        var tgsLegacy = downstream
            .Where(d => d.ReferenceEntity == "TeachingGroupSection")
            .Sum(d => d.ReferenceCount);

        var tgBoundary = TgSectionBoundaryClassification.SafeForHardening;
        if (tgLegacy.Count > 0 || tgsLegacy > 0)
            tgBoundary = TgSectionBoundaryClassification.BlockedByTg;
        else if (sectionLegacy.Count > 0 || tsLegacyViaSection > 0)
            tgBoundary = TgSectionBoundaryClassification.BlockedBySection;

        var tgSectionSummary = new TgSectionBoundarySummaryDto
        {
            Classification = tgBoundary,
            ClassificationCode = tgBoundary switch
            {
                TgSectionBoundaryClassification.BlockedByTg => "BLOCKED_BY_TG",
                TgSectionBoundaryClassification.BlockedBySection => "BLOCKED_BY_SECTION",
                TgSectionBoundaryClassification.ManualReview => "MANUAL_REVIEW",
                _ => "SAFE_FOR_HARDENING",
            },
            TeachingGroupLegacyRefs = tgLegacy.Count,
            TeachingGroupSectionMismatches = tgsLegacy,
            SectionLegacyRefs = sectionLegacy.Count,
            TimetableSectionLegacyRefs = tsLegacyViaSection,
            Notes = "Classify-only; no TG/Section mutation. TimetableSection audited via Section.SemesterId.",
        };

        // Student integrity
        var studentSummary = await AuditStudentsAsync(cancellationToken, findings);

        // Scheduling integrity
        var scheduling = await AuditSchedulingAsync(nullIds, cancellationToken, findings);

        // Attendance + Section integrity (Prompt 3J3 explicit gates)
        var attendance = await AuditAttendanceAsync(nullIds, cancellationToken, findings);
        var sectionIntegrityErrors = await AuditSectionsAsync(nullIds, cancellationToken, findings);
        var tgIntegrityErrors = tgLegacy.Count + tgsLegacy;

        // Sample downstream consumer findings (deterministic, capped) for admin review
        var consumerFindings = await BuildConsumerFindingsAsync(nullIds, cancellationToken);

        // Wildcard dependency audit (source + catalog)
        var wildcards = AuditWildcardDependencies();
        var activeWildcardCount = wildcards.Count(w => w.Kind == WildcardDependencyKind.ActiveProduction
                                                        || w.BlocksHardening);
        foreach (var w in wildcards.Where(x => x.BlocksHardening))
        {
            findings.Add(Finding("WILDCARD_ACTIVE_PRODUCTION", SemesterSchemaHardeningFindingSeverity.Critical,
                "CodePath", null, _currentUser.TenantId,
                w.Path, "No active NULL-group operational resolution",
                w.Notes,
                "Retire operational wildcard under approved prompt (do not reintroduce course-wide semantics).",
                "AcademicTree/UI"));
        }

        // Write-path verification (source)
        var (writeOk, noNullWrite, writeNotes) = VerifyWritePaths();
        notes.AddRange(writeNotes);
        if (!writeOk)
        {
            findings.Add(Finding("WRITE_PATH_NOT_GROUP_OWNED", SemesterSchemaHardeningFindingSeverity.Critical,
                "SemesterWritePath", null, _currentUser.TenantId,
                "Write path allows NULL GroupId or Course mismatch",
                "Group required; CourseId from Group",
                "Semester write-path contract violated.",
                "Fix Semester create/update/import paths under Architect prompt.",
                "API/Semester"));
        }

        var architectureGuardsIntact = VerifyArchitectureGuardsIntact();
        if (!architectureGuardsIntact)
        {
            findings.Add(Finding("ARCHITECTURE_GUARD_WEAKENED", SemesterSchemaHardeningFindingSeverity.Critical,
                "ArchitectureGuard", null, _currentUser.TenantId,
                "Guard weakened", "Guards intact",
                "Existing TG/CAP architecture guard contract appears weakened.",
                "Restore guards; do not trade GO for weakened CAP/TG.",
                "Scheduling/CAP"));
        }

        // Constraint simulation
        var notNullReady = nullGroup.Count == 0;
        var uniqueReady = duplicates.Count == 0 && notNullReady;
        var simSummary =
            $"ALTER GroupId NOT NULL would {(notNullReady ? "SUCCEED (visible non-deleted rows)" : $"FAIL on SemIds=[{string.Join(",", nullGroup.Select(s => s.Id))}]")}. " +
            $"UNIQUE(TenantId,GroupId,Number) would {(duplicates.Count == 0 ? "SUCCEED for Group-specific non-deleted keys" : $"FAIL on {duplicates.Count} duplicate key(s)")}" +
            (notNullReady ? "." : " (also blocked while NULL GroupId rows remain under plain UNIQUE without filtered design).");

        // GO criteria (strict) — Prompt 3J §17
        var studentViolations = studentSummary.Invalid + studentSummary.OrphanedSemester;
        var schedulingViolations = scheduling.SubjectAllocationInvalid
            + scheduling.TimetableEntryInvalid
            + scheduling.TimetableSectionInvalid;
        var writePathViolations = (!writeOk || !noNullWrite) ? 1 : 0;
        var manualReview = nullRows.Count(r =>
            r.Disposition is NullGroupSemesterDisposition.ManualMappingRequired
                or NullGroupSemesterDisposition.Unexplained);
        // Duplicate-review NULL rows also count as manual review blockers for readiness codes
        manualReview += nullRows.Count(r =>
            r.DispositionCode.Contains("DUPLICATE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(r.DispositionCode, "OTHER_EXPLICIT_APPROVED_STATE", StringComparison.Ordinal)
               && r.Evidence.Contains("DUPLICATE_REVIEW", StringComparison.OrdinalIgnoreCase));

        var semesterIntegrityErrors = invalidOwnership + nullGroup.Count + duplicates.Count;

        var go =
            nullGroup.Count == 0
            && invalidOwnership == 0
            && duplicates.Count == 0
            && downstreamLegacyCount == 0
            && tgLegacy.Count == 0
            && sectionLegacy.Count == 0
            && studentViolations == 0
            && attendance.SessionsInvalid == 0
            && sectionIntegrityErrors == 0
            && schedulingViolations == 0
            && tgIntegrityErrors == 0
            && activeWildcardCount == 0
            && crossTenant == 0
            && writePathViolations == 0
            && manualReview == 0
            && notNullReady
            && uniqueReady
            && writeOk
            && noNullWrite
            && architectureGuardsIntact;

        // Deduplicate + deterministic sort: blocking → entity → tenant → record → semester
        var blocking = findings
            .Where(f => f.Severity is SemesterSchemaHardeningFindingSeverity.Critical
                or SemesterSchemaHardeningFindingSeverity.Error)
            .GroupBy(f => $"{f.Code}:{f.Entity}:{f.EntityId}:{f.TenantId}:{f.SemesterId}")
            .Select(g => g.First())
            .OrderByDescending(f => f.IsBlocking)
            .ThenBy(f => f.Entity, StringComparer.Ordinal)
            .ThenBy(f => f.TenantId ?? 0)
            .ThenBy(f => f.EntityId ?? 0)
            .ThenBy(f => f.SemesterId ?? 0)
            .ThenBy(f => f.Code, StringComparer.Ordinal)
            .ToList();

        var readinessCodes = ComputeReadinessCodes(
            go,
            nullGroup.Count,
            duplicates.Count,
            downstreamLegacyCount,
            activeWildcardCount,
            tgLegacy.Count + tgsLegacy,
            writePathViolations,
            crossTenant,
            manualReview,
            semesterIntegrityErrors,
            studentViolations,
            schedulingViolations + attendance.SessionsInvalid + sectionIntegrityErrors);

        var primaryCode = go
            ? SemesterSchemaHardeningReadinessCodes.ReadyForSchemaHardening
            : readinessCodes.FirstOrDefault(c =>
                  !string.Equals(c, SemesterSchemaHardeningReadinessCodes.ReadyForSchemaHardening, StringComparison.Ordinal))
              ?? SemesterSchemaHardeningReadinessCodes.NotReadyNullSemesters;

        var wildcardClosure = activeWildcardCount == 0 ? "CLOSED" : "OPEN";

        notes.Add($"Decision={(go ? "READY_FOR_SCHEMA_HARDENING" : primaryCode)}; NullGroup={nullGroup.Count}; DupKeys={duplicates.Count}; ActiveWildcards={activeWildcardCount}; WildcardClosure={wildcardClosure}.");
        notes.Add(simSummary);
        notes.Add($"ReadinessCodes=[{string.Join(",", readinessCodes)}].");

        downstream = downstream
            .OrderBy(d => d.TenantId)
            .ThenBy(d => d.ReferenceEntity, StringComparer.Ordinal)
            .ThenBy(d => d.SemesterId)
            .ToList();

        return new SemesterSchemaHardeningReadinessResult
        {
            GeneratedAt = DateTime.UtcNow,
            PromptCode = PromptCode,
            IsReadOnly = true,
            NoMutationsPerformed = true,
            SaveChangesInvoked = false,
            IsReady = go,
            Decision = go ? SemesterSchemaHardeningDecision.Go : SemesterSchemaHardeningDecision.NoGo,
            DecisionCode = primaryCode,
            ReadinessCodes = readinessCodes,
            TenantCount = tenantCount,
            SemesterCount = semesters.Count,
            NullGroupSemesterCount = nullGroup.Count,
            DuplicateGroupSemesterCount = duplicates.Count,
            InvalidOwnershipCount = invalidOwnership,
            DuplicateKeyCount = duplicates.Count,
            SemesterIntegrityErrorCount = semesterIntegrityErrors,
            StudentIntegrityErrorCount = studentViolations,
            AttendanceIntegrityErrorCount = attendance.SessionsInvalid,
            SectionIntegrityErrorCount = sectionIntegrityErrors,
            SubjectAllocationIntegrityErrorCount = scheduling.SubjectAllocationInvalid,
            TimetableIntegrityErrorCount = scheduling.TimetableEntryInvalid + scheduling.TimetableSectionInvalid,
            TeachingGroupIntegrityErrorCount = tgIntegrityErrors,
            DownstreamLegacyReferenceCount = downstreamLegacyCount,
            TeachingGroupBlockingCount = tgLegacy.Count,
            SectionBlockingCount = sectionLegacy.Count,
            StudentIntegrityViolationCount = studentViolations,
            SchedulingIntegrityViolationCount = schedulingViolations,
            WildcardConsumerCount = activeWildcardCount,
            WildcardProductionDependencyCount = activeWildcardCount,
            ActiveWritePathViolationCount = writePathViolations,
            CrossTenantViolationCount = crossTenant,
            ManualReviewCount = manualReview,
            NotNullReady = notNullReady,
            UniqueReady = uniqueReady,
            WritePathsGroupOwned = writeOk,
            NoActiveNullGroupWritePath = noNullWrite,
            ArchitectureGuardsIntact = architectureGuardsIntact,
            WildcardConsumerClosureStatus = wildcardClosure,
            LifecycleScopeNote =
                "Includes non-deleted Semesters visible under EF tenant+IsDeleted filters. Soft-deleted excluded from counts; DBA must verify soft-deleted NULL GroupId before ALTER NOT NULL. Recommended eventual UNIQUE is filtered WHERE IsDeleted=0.",
            ConstraintSimulationSummary = simSummary,
            EvidenceSummary = go
                ? "All GO criteria satisfied for visible operational data."
                : $"NOT_READY: {blocking.Count} blocking finding(s). Codes=[{string.Join(",", readinessCodes)}]. NullGroup={nullGroup.Count}; Dup={duplicates.Count}; StudentViol={studentViolations}; SchedViol={schedulingViolations}; AttViol={attendance.SessionsInvalid}; ActiveWildcards={activeWildcardCount}.",
            BlockingFindings = blocking,
            Warnings = warnings,
            NullGroupSemesters = nullRows,
            DownstreamLegacyReferences = downstream,
            DownstreamConsumerFindings = consumerFindings,
            DuplicateKeys = duplicates,
            WildcardDependencies = wildcards,
            StudentIntegrity = studentSummary,
            SchedulingIntegrity = scheduling,
            AttendanceIntegrity = attendance,
            TeachingGroupSectionBoundary = tgSectionSummary,
            Notes = notes,
            RecommendedNextPrompt = go
                ? "Proposed Prompt 3K — Semester Database Schema Hardening Execution (NOT NULL + filtered UNIQUE) with rollback plan. DO NOT implement until Chief Architect authorizes."
                : "Do NOT begin schema DDL. Clear BlockingFindings / ReadinessCodes via Architect-approved remediation (historical archive excluding FKs, Sem1 Subject disposition, Sem4/5 duplicate review, soft-deleted NULL scan).",
        };
    }

    private static IReadOnlyList<string> ComputeReadinessCodes(
        bool go,
        int nullGroup,
        int duplicates,
        int downstreamLegacy,
        int activeWildcards,
        int tgLegacy,
        int writePathViolations,
        int crossTenant,
        int manualReview,
        int semesterIntegrity,
        int studentViolations,
        int schedulingViolations)
    {
        if (go)
            return [SemesterSchemaHardeningReadinessCodes.ReadyForSchemaHardening];

        var codes = new List<string>();
        if (nullGroup > 0)
            codes.Add(SemesterSchemaHardeningReadinessCodes.NotReadyNullSemesters);
        if (duplicates > 0)
            codes.Add(SemesterSchemaHardeningReadinessCodes.NotReadyDuplicates);
        if (semesterIntegrity > 0 && nullGroup == 0 && duplicates == 0)
            codes.Add(SemesterSchemaHardeningReadinessCodes.NotReadySemesterIntegrity);
        if (downstreamLegacy > 0)
            codes.Add(SemesterSchemaHardeningReadinessCodes.NotReadyDownstreamReferences);
        if (activeWildcards > 0)
            codes.Add(SemesterSchemaHardeningReadinessCodes.NotReadyWildcardConsumers);
        if (tgLegacy > 0)
            codes.Add(SemesterSchemaHardeningReadinessCodes.NotReadyTgReferences);
        if (writePathViolations > 0)
            codes.Add(SemesterSchemaHardeningReadinessCodes.NotReadyWritePath);
        if (crossTenant > 0)
            codes.Add(SemesterSchemaHardeningReadinessCodes.NotReadyTenantIsolation);
        if (manualReview > 0)
            codes.Add(SemesterSchemaHardeningReadinessCodes.NotReadyManualReview);
        if (studentViolations > 0)
            codes.Add(SemesterSchemaHardeningReadinessCodes.NotReadyStudentIntegrity);
        if (schedulingViolations > 0)
            codes.Add(SemesterSchemaHardeningReadinessCodes.NotReadySchedulingIntegrity);
        if (codes.Count == 0)
            codes.Add(SemesterSchemaHardeningReadinessCodes.NotReadyNullSemesters);
        return codes;
    }

    private static (NullGroupSemesterDisposition, string, string) ClassifyNullGroup(
        int semesterId,
        IReadOnlyDictionary<int, LegacySemesterInventoryRowDto> finById,
        IReadOnlyDictionary<int, JournalSnap> journals)
    {
        if (journals.TryGetValue(semesterId, out var j))
        {
            var code = j.DispositionCode;
            if (string.Equals(code, "RETAIN_HISTORICAL", StringComparison.OrdinalIgnoreCase)
                || string.Equals(code, "HISTORICAL_RETAIN", StringComparison.OrdinalIgnoreCase))
                return (NullGroupSemesterDisposition.RetainHistorical, "RETAIN_HISTORICAL", j.Evidence);
            if (string.Equals(code, "MANUAL_MAPPING_REQUIRED", StringComparison.OrdinalIgnoreCase))
                return (NullGroupSemesterDisposition.ManualMappingRequired, "MANUAL_MAPPING_REQUIRED", j.Evidence);
            if (code.Contains("BLOCKED", StringComparison.OrdinalIgnoreCase))
                return (NullGroupSemesterDisposition.Blocked, "BLOCKED", j.Evidence);
            if (string.Equals(code, "OPERATIONAL_WILDCARD_RETIRED", StringComparison.OrdinalIgnoreCase)
                || string.Equals(code, "FINALIZED_LEGACY", StringComparison.OrdinalIgnoreCase))
                return (NullGroupSemesterDisposition.OtherExplicitApprovedState, "OTHER_EXPLICIT_APPROVED_STATE", j.Evidence);
            if (code.Contains("REMEDIAT", StringComparison.OrdinalIgnoreCase))
                return (NullGroupSemesterDisposition.Remediated, "REMEDIATED", j.Evidence);
        }

        if (finById.TryGetValue(semesterId, out var row))
        {
            return row.Disposition switch
            {
                LegacySemesterFinalizationDisposition.HistoricalRetain =>
                    (NullGroupSemesterDisposition.RetainHistorical, "RETAIN_HISTORICAL", row.DispositionEvidence),
                LegacySemesterFinalizationDisposition.ManualMappingRequired
                    or LegacySemesterFinalizationDisposition.SplitRequired
                    or LegacySemesterFinalizationDisposition.UnknownRequiresArchitectDecision =>
                    (NullGroupSemesterDisposition.ManualMappingRequired, "MANUAL_MAPPING_REQUIRED", row.DispositionEvidence),
                LegacySemesterFinalizationDisposition.BlockedByTeachingGroupReference =>
                    (NullGroupSemesterDisposition.Blocked, "BLOCKED", row.DispositionEvidence),
                LegacySemesterFinalizationDisposition.DuplicateReview =>
                    (NullGroupSemesterDisposition.OtherExplicitApprovedState, "OTHER_EXPLICIT_APPROVED_STATE",
                        $"DUPLICATE_REVIEW: {row.DispositionEvidence}"),
                _ => (NullGroupSemesterDisposition.OtherExplicitApprovedState, "OTHER_EXPLICIT_APPROVED_STATE",
                    $"{row.DispositionCode}: {row.DispositionEvidence}"),
            };
        }

        return (NullGroupSemesterDisposition.Unexplained, "UNEXPLAINED", "No journal or finalization disposition.");
    }

    private async Task CollectDownstreamAsync(
        List<DownstreamLegacyReferenceRowDto> sink,
        List<(int Id, int TenantId, int Number, int CourseId)> nullSemesters,
        CancellationToken ct)
    {
        if (nullSemesters.Count == 0) return;
        var ids = nullSemesters.Select(s => s.Id).ToList();
        var meta = nullSemesters.ToDictionary(s => s.Id);

        async Task AddEntity(string entity, IQueryable<IdSem> query)
        {
            var rows = await query.ToListAsync(ct);
            foreach (var g in rows.GroupBy(r => r.SemesterId))
            {
                if (!meta.TryGetValue(g.Key, out var m)) continue;
                sink.Add(new DownstreamLegacyReferenceRowDto
                {
                    TenantId = m.TenantId,
                    SemesterId = m.Id,
                    SemesterNumber = m.Number,
                    CourseId = m.CourseId,
                    GroupId = null,
                    ReferenceEntity = entity,
                    ReferenceCount = g.Count(),
                    ReferenceIds = g.Select(x => x.Id).Take(25).ToList(),
                    Disposition = "BLOCKED",
                    BlockingReason = $"{entity} still references NULL-group Semester {m.Id}.",
                });
            }
        }

        await AddEntity("Student", _db.Students.AsNoTracking()
            .Where(s => !s.IsDeleted && ids.Contains(s.SemesterId))
            .Select(s => new IdSem(s.Id.ToString(), s.SemesterId)));
        await AddEntity("AttendanceSession", _db.AttendanceSessions.AsNoTracking()
            .Where(a => ids.Contains(a.SemesterId))
            .Select(a => new IdSem(a.Id.ToString(), a.SemesterId)));
        await AddEntity("Section", _db.Sections.AsNoTracking()
            .Where(s => !s.IsDeleted && ids.Contains(s.SemesterId))
            .Select(s => new IdSem(s.Id.ToString(), s.SemesterId)));
        await AddEntity("Subject", _db.Subjects.AsNoTracking()
            .Where(s => !s.IsDeleted && ids.Contains(s.SemesterId))
            .Select(s => new IdSem(s.Id.ToString(), s.SemesterId)));
        await AddEntity("SubjectAllocation", _db.SchedulingSubjectAllocations.AsNoTracking()
            .Where(a => !a.IsDeleted && ids.Contains(a.SemesterId))
            .Select(a => new IdSem(a.Id.ToString(), a.SemesterId)));
        await AddEntity("TimetableEntry", _db.SchedulingTimetableEntries.AsNoTracking()
            .Where(e => !e.IsDeleted && ids.Contains(e.SemesterId))
            .Select(e => new IdSem(e.Id.ToString(), e.SemesterId)));
        await AddEntity("TeachingGroup", _db.SchedulingTeachingGroups.AsNoTracking()
            .Where(t => !t.IsDeleted && ids.Contains(t.SemesterId))
            .Select(t => new IdSem(t.Id.ToString(), t.SemesterId)));

        // TeachingGroupSection via TeachingGroup.SemesterId
        var tgOnLegacy = await _db.SchedulingTeachingGroups.AsNoTracking()
            .Where(t => !t.IsDeleted && ids.Contains(t.SemesterId))
            .Select(t => new { t.Id, t.SemesterId })
            .ToListAsync(ct);
        if (tgOnLegacy.Count > 0)
        {
            var tgIds = tgOnLegacy.Select(t => t.Id).ToList();
            var tgs = await _db.SchedulingTeachingGroupSections.AsNoTracking()
                .Where(x => !x.IsDeleted && tgIds.Contains(x.TeachingGroupId))
                .Select(x => new { x.Id, x.TeachingGroupId })
                .ToListAsync(ct);
            var tgToSem = tgOnLegacy.ToDictionary(t => t.Id, t => t.SemesterId);
            foreach (var g in tgs.GroupBy(x => tgToSem[x.TeachingGroupId]))
            {
                if (!meta.TryGetValue(g.Key, out var m)) continue;
                sink.Add(new DownstreamLegacyReferenceRowDto
                {
                    TenantId = m.TenantId,
                    SemesterId = m.Id,
                    SemesterNumber = m.Number,
                    CourseId = m.CourseId,
                    ReferenceEntity = "TeachingGroupSection",
                    ReferenceCount = g.Count(),
                    ReferenceIds = g.Select(x => x.Id.ToString()).Take(25).ToList(),
                    Disposition = "BLOCKED",
                    BlockingReason = $"TeachingGroupSection via TeachingGroup on NULL-group Semester {m.Id}.",
                });
            }
        }

        // StudentSection via Section.SemesterId
        var sectionIdsOnLegacy = await _db.Sections.AsNoTracking()
            .Where(s => !s.IsDeleted && ids.Contains(s.SemesterId))
            .Select(s => new { s.Id, s.SemesterId })
            .ToListAsync(ct);
        if (sectionIdsOnLegacy.Count > 0)
        {
            var secIds = sectionIdsOnLegacy.Select(s => s.Id).ToList();
            var ss = await _db.StudentSections.AsNoTracking()
                .Where(x => !x.IsDeleted && secIds.Contains(x.SectionId))
                .Select(x => new { x.Id, x.SectionId })
                .ToListAsync(ct);
            var secToSem = sectionIdsOnLegacy.ToDictionary(s => s.Id, s => s.SemesterId);
            foreach (var g in ss.GroupBy(x => secToSem[x.SectionId]))
            {
                if (!meta.TryGetValue(g.Key, out var m)) continue;
                sink.Add(new DownstreamLegacyReferenceRowDto
                {
                    TenantId = m.TenantId,
                    SemesterId = m.Id,
                    SemesterNumber = m.Number,
                    CourseId = m.CourseId,
                    ReferenceEntity = "StudentSection",
                    ReferenceCount = g.Count(),
                    ReferenceIds = g.Select(x => x.Id.ToString()).Take(25).ToList(),
                    Disposition = "BLOCKED",
                    BlockingReason = $"StudentSection via Section on NULL-group Semester {m.Id}.",
                });
            }

            // TimetableSection via Section.SemesterId
            var tts = await _db.TimetableSections.AsNoTracking()
                .Where(x => !x.IsDeleted && secIds.Contains(x.SectionId))
                .Select(x => new { x.Id, x.SectionId })
                .ToListAsync(ct);
            foreach (var g in tts.GroupBy(x => secToSem[x.SectionId]))
            {
                if (!meta.TryGetValue(g.Key, out var m)) continue;
                sink.Add(new DownstreamLegacyReferenceRowDto
                {
                    TenantId = m.TenantId,
                    SemesterId = m.Id,
                    SemesterNumber = m.Number,
                    CourseId = m.CourseId,
                    ReferenceEntity = "TimetableSection",
                    ReferenceCount = g.Count(),
                    ReferenceIds = g.Select(x => x.Id.ToString()).Take(25).ToList(),
                    Disposition = "BLOCKED",
                    BlockingReason = $"TimetableSection via Section on NULL-group Semester {m.Id}.",
                });
            }
        }
    }

    private async Task<StudentIntegrityAuditSummaryDto> AuditStudentsAsync(
        CancellationToken ct,
        List<SemesterSchemaHardeningFindingDto> findings)
    {
        var semesters = await _db.Semesters.AsNoTracking()
            .Select(s => new { s.Id, s.TenantId, s.CourseId, s.GroupId })
            .ToDictionaryAsync(s => s.Id, ct);
        var groups = await _db.Groups.AsNoTracking()
            .Select(g => new { g.Id, g.TenantId, g.CourseId })
            .ToDictionaryAsync(g => g.Id, ct);

        var students = await _db.Students.AsNoTracking()
            .Where(s => !s.IsDeleted)
            .Select(s => new { s.Id, s.TenantId, s.CourseId, s.GroupId, s.SemesterId })
            .ToListAsync(ct);

        var valid = 0;
        var invalid = 0;
        var legacy = 0;
        var orphan = 0;

        foreach (var st in students)
        {
            if (!semesters.TryGetValue(st.SemesterId, out var sem))
            {
                orphan++;
                findings.Add(Finding("STUDENT_ORPHAN_SEMESTER", SemesterSchemaHardeningFindingSeverity.Critical,
                    "Student", st.Id, st.TenantId,
                    $"SemesterId={st.SemesterId}", "Existing Semester",
                    "Student references missing Semester.",
                    "Repair Student.SemesterId under approved remediation.",
                    "Academic/Student"));
                continue;
            }

            if (sem.TenantId != st.TenantId)
            {
                invalid++;
                findings.Add(Finding("CROSS_TENANT_STUDENT_SEMESTER", SemesterSchemaHardeningFindingSeverity.Critical,
                    "Student", st.Id, st.TenantId,
                    $"Student.Tenant={st.TenantId}; Sem.Tenant={sem.TenantId}",
                    "Same TenantId",
                    "Cross-tenant Student→Semester.",
                    "Remediate under Architect-approved prompt.",
                    "Security/TenantIsolation"));
                continue;
            }

            if (!groups.TryGetValue(st.GroupId, out var g) || g.CourseId != st.CourseId)
            {
                invalid++;
                findings.Add(Finding("STUDENT_COURSE_GROUP_MISMATCH", SemesterSchemaHardeningFindingSeverity.Critical,
                    "Student", st.Id, st.TenantId,
                    $"Course={st.CourseId}; Group={st.GroupId}",
                    "Student.CourseId == Group.CourseId",
                    "Student Course/Group hierarchy invalid.",
                    "Repair Student hierarchy under approved remediation.",
                    "Academic/Student"));
                continue;
            }

            if (g.TenantId != st.TenantId)
            {
                invalid++;
                findings.Add(Finding("CROSS_TENANT_STUDENT_GROUP", SemesterSchemaHardeningFindingSeverity.Critical,
                    "Student", st.Id, st.TenantId,
                    $"Student.Tenant={st.TenantId}; Group.Tenant={g.TenantId}",
                    "Same TenantId",
                    "Cross-tenant Student→Group.",
                    "Remediate under Architect-approved prompt.",
                    "Security/TenantIsolation"));
                continue;
            }

            if (sem.GroupId is null)
            {
                legacy++;
                invalid++;
                findings.Add(Finding("STUDENT_LEGACY_NULL_GROUP_SEMESTER", SemesterSchemaHardeningFindingSeverity.Critical,
                    "Student", st.Id, st.TenantId,
                    $"SemesterId={st.SemesterId} GroupId=NULL",
                    "Group-specific Semester matching Student.GroupId",
                    "Student on NULL-group Semester.",
                    "Remap Student.SemesterId to Group-specific Semester.",
                    "Academic/Student"));
                continue;
            }

            if (sem.GroupId.Value != st.GroupId || sem.CourseId != st.CourseId)
            {
                invalid++;
                findings.Add(Finding("STUDENT_SEMESTER_HIERARCHY_MISMATCH", SemesterSchemaHardeningFindingSeverity.Critical,
                    "Student", st.Id, st.TenantId,
                    $"Student C/G={st.CourseId}/{st.GroupId}; Sem C/G={sem.CourseId}/{sem.GroupId}",
                    "Matching Course/Group",
                    "Student Semester hierarchy mismatch.",
                    "Remap Student.SemesterId under approved remediation.",
                    "Academic/Student"));
                continue;
            }

            valid++;
        }

        return new StudentIntegrityAuditSummaryDto
        {
            TotalAudited = students.Count,
            Valid = valid,
            Invalid = invalid,
            Legacy = legacy,
            OrphanedSemester = orphan,
        };
    }

    private async Task<SchedulingIntegrityAuditSummaryDto> AuditSchedulingAsync(
        HashSet<int> nullIds,
        CancellationToken ct,
        List<SemesterSchemaHardeningFindingDto> findings)
    {
        var semesters = await _db.Semesters.AsNoTracking()
            .Select(s => new { s.Id, s.CourseId, s.GroupId })
            .ToDictionaryAsync(s => s.Id, ct);

        var sa = await _db.SchedulingSubjectAllocations.AsNoTracking()
            .Where(a => !a.IsDeleted)
            .Select(a => new { a.Id, a.TenantId, a.SemesterId, a.CourseId, a.GroupId })
            .ToListAsync(ct);
        var tt = await _db.SchedulingTimetableEntries.AsNoTracking()
            .Where(e => !e.IsDeleted)
            .Select(e => new { e.Id, e.TenantId, e.SemesterId, e.CourseId, e.GroupId })
            .ToListAsync(ct);
        var tsCount = await _db.TimetableSections.AsNoTracking()
            .CountAsync(t => !t.IsDeleted, ct);

        var saInvalid = 0;
        foreach (var a in sa)
        {
            if (nullIds.Contains(a.SemesterId)
                || !semesters.TryGetValue(a.SemesterId, out var sem)
                || sem.GroupId is null
                || sem.GroupId.Value != a.GroupId
                || sem.CourseId != a.CourseId)
            {
                saInvalid++;
                findings.Add(Finding("SA_INVALID_SEMESTER", SemesterSchemaHardeningFindingSeverity.Critical,
                    "SubjectAllocation", a.Id, a.TenantId,
                    $"SemesterId={a.SemesterId}", "Group-specific matching Course/Group",
                    "SubjectAllocation Semester invalid for hardening.",
                    "Remediate SA under approved downstream prompt (do not alter CAP).",
                    "Scheduling/SubjectAllocation"));
            }
        }

        var ttInvalid = 0;
        foreach (var e in tt)
        {
            if (nullIds.Contains(e.SemesterId)
                || !semesters.TryGetValue(e.SemesterId, out var sem)
                || sem.GroupId is null
                || sem.GroupId.Value != e.GroupId
                || sem.CourseId != e.CourseId)
            {
                ttInvalid++;
                findings.Add(Finding("TT_INVALID_SEMESTER", SemesterSchemaHardeningFindingSeverity.Critical,
                    "TimetableEntry", e.Id, e.TenantId,
                    $"SemesterId={e.SemesterId}", "Group-specific matching Course/Group",
                    "TimetableEntry Semester invalid for hardening.",
                    "Remediate TT under approved downstream prompt (do not alter CAP/Publish).",
                    "Scheduling/TimetableEntry"));
            }
        }

        var tsInvalid = 0;
        if (nullIds.Count > 0)
        {
            var secOnNull = await _db.Sections.AsNoTracking()
                .Where(s => !s.IsDeleted && nullIds.Contains(s.SemesterId))
                .Select(s => s.Id)
                .ToListAsync(ct);
            if (secOnNull.Count > 0)
            {
                tsInvalid = await _db.TimetableSections.AsNoTracking()
                    .CountAsync(t => !t.IsDeleted && secOnNull.Contains(t.SectionId), ct);
                if (tsInvalid > 0)
                {
                    findings.Add(Finding("TS_INVALID_SEMESTER", SemesterSchemaHardeningFindingSeverity.Critical,
                        "TimetableSection", null, _currentUser.TenantId,
                        $"{tsInvalid} TimetableSection(s) via Section on NULL-group Semester",
                        "Zero TimetableSection refs to NULL-group Semesters",
                        "TimetableSection still linked to Section on legacy Semester.",
                        "Remediate Sections/TimetableSection under approved prompt (do not weaken TG).",
                        "Scheduling/TimetableSection"));
                }
            }
        }

        return new SchedulingIntegrityAuditSummaryDto
        {
            SubjectAllocationChecked = sa.Count,
            SubjectAllocationInvalid = saInvalid,
            TimetableEntryChecked = tt.Count,
            TimetableEntryInvalid = ttInvalid,
            TimetableSectionChecked = tsCount,
            TimetableSectionInvalid = tsInvalid,
        };
    }

    private async Task<AttendanceIntegrityAuditSummaryDto> AuditAttendanceAsync(
        HashSet<int> nullIds,
        CancellationToken ct,
        List<SemesterSchemaHardeningFindingDto> findings)
    {
        var semesters = await _db.Semesters.AsNoTracking()
            .Select(s => new { s.Id, s.TenantId, s.CourseId, s.GroupId })
            .ToDictionaryAsync(s => s.Id, ct);

        var sessions = await _db.AttendanceSessions.AsNoTracking()
            .Select(a => new { a.Id, a.TenantId, a.SemesterId, a.CourseId, a.GroupId })
            .ToListAsync(ct);

        var invalid = 0;
        foreach (var a in sessions)
        {
            if (!semesters.TryGetValue(a.SemesterId, out var sem))
            {
                invalid++;
                findings.Add(Finding("ATTENDANCE_ORPHAN_SEMESTER", SemesterSchemaHardeningFindingSeverity.Critical,
                    "AttendanceSession", null, a.TenantId,
                    $"Session={a.Id}; SemesterId={a.SemesterId}", "Existing Semester",
                    "AttendanceSession references missing Semester.",
                    "Remediate Attendance under approved prompt.",
                    "Attendance"));
                continue;
            }

            if (sem.TenantId != a.TenantId)
            {
                invalid++;
                findings.Add(Finding("CROSS_TENANT_ATTENDANCE_SEMESTER", SemesterSchemaHardeningFindingSeverity.Critical,
                    "AttendanceSession", null, a.TenantId,
                    $"Session.Tenant={a.TenantId}; Sem.Tenant={sem.TenantId}",
                    "Same TenantId",
                    "Cross-tenant Attendance→Semester.",
                    "Remediate under Architect-approved prompt.",
                    "Security/TenantIsolation"));
                continue;
            }

            if (nullIds.Contains(a.SemesterId) || sem.GroupId is null
                || sem.GroupId.Value != a.GroupId
                || sem.CourseId != a.CourseId)
            {
                invalid++;
                findings.Add(Finding("ATTENDANCE_INVALID_SEMESTER", SemesterSchemaHardeningFindingSeverity.Critical,
                    "AttendanceSession", null, a.TenantId,
                    $"Session={a.Id}; SemesterId={a.SemesterId}; Group={a.GroupId}",
                    "Group-specific matching Course/Group",
                    "AttendanceSession Semester invalid for hardening.",
                    "Remediate Attendance under approved downstream prompt.",
                    "Attendance"));
            }
        }

        return new AttendanceIntegrityAuditSummaryDto
        {
            SessionsChecked = sessions.Count,
            SessionsInvalid = invalid,
        };
    }

    private async Task<int> AuditSectionsAsync(
        HashSet<int> nullIds,
        CancellationToken ct,
        List<SemesterSchemaHardeningFindingDto> findings)
    {
        var semesters = await _db.Semesters.AsNoTracking()
            .Select(s => new { s.Id, s.TenantId, s.CourseId, s.GroupId })
            .ToDictionaryAsync(s => s.Id, ct);

        var sections = await _db.Sections.AsNoTracking()
            .Where(s => !s.IsDeleted)
            .Select(s => new { s.Id, s.TenantId, s.SemesterId, s.CourseId, s.GroupId })
            .ToListAsync(ct);

        var invalid = 0;
        foreach (var s in sections)
        {
            if (!semesters.TryGetValue(s.SemesterId, out var sem))
            {
                invalid++;
                findings.Add(Finding("SECTION_ORPHAN_SEMESTER", SemesterSchemaHardeningFindingSeverity.Critical,
                    "Section", s.Id, s.TenantId,
                    $"SemesterId={s.SemesterId}", "Existing Semester",
                    "Section references missing Semester.",
                    "Remediate Section under approved prompt.",
                    "Academic/Section"));
                continue;
            }

            if (sem.TenantId != s.TenantId)
            {
                invalid++;
                findings.Add(Finding("CROSS_TENANT_SECTION_SEMESTER", SemesterSchemaHardeningFindingSeverity.Critical,
                    "Section", s.Id, s.TenantId,
                    $"Section.Tenant={s.TenantId}; Sem.Tenant={sem.TenantId}",
                    "Same TenantId",
                    "Cross-tenant Section→Semester.",
                    "Remediate under Architect-approved prompt.",
                    "Security/TenantIsolation"));
                continue;
            }

            if (nullIds.Contains(s.SemesterId) || sem.GroupId is null
                || sem.GroupId.Value != s.GroupId
                || sem.CourseId != s.CourseId)
            {
                invalid++;
                findings.Add(Finding("SECTION_INVALID_SEMESTER", SemesterSchemaHardeningFindingSeverity.Critical,
                    "Section", s.Id, s.TenantId,
                    $"SemesterId={s.SemesterId}; Group={s.GroupId}",
                    "Group-specific matching Course/Group",
                    "Section Semester invalid for hardening.",
                    "Remediate Section under approved prompt (do not write TimetableSection directly).",
                    "Academic/Section"));
            }
        }

        return invalid;
    }

    private async Task<IReadOnlyList<DownstreamConsumerFindingDto>> BuildConsumerFindingsAsync(
        HashSet<int> nullIds,
        CancellationToken ct)
    {
        if (nullIds.Count == 0)
            return [];

        var semesters = await _db.Semesters.AsNoTracking()
            .Where(s => nullIds.Contains(s.Id))
            .Select(s => new { s.Id, s.TenantId, s.GroupId })
            .ToDictionaryAsync(s => s.Id, ct);

        var list = new List<DownstreamConsumerFindingDto>();

        void Add(string entity, string recordId, int tenantId, int semesterId, int? entityGroupId, DownstreamConsumerStatus status, string evidence)
        {
            semesters.TryGetValue(semesterId, out var sem);
            list.Add(new DownstreamConsumerFindingDto
            {
                Entity = entity,
                RecordId = recordId,
                TenantId = tenantId,
                SemesterId = semesterId,
                SemesterGroupId = sem?.GroupId,
                EntityGroupId = entityGroupId,
                ExpectedGroupId = entityGroupId,
                Status = status,
                StatusCode = status.ToString().ToUpperInvariant(),
                Evidence = evidence,
            });
        }

        var subjects = await _db.Subjects.AsNoTracking()
            .Where(s => !s.IsDeleted && nullIds.Contains(s.SemesterId))
            .Select(s => new { s.Id, s.TenantId, s.SemesterId, s.GroupId })
            .OrderBy(s => s.TenantId).ThenBy(s => s.Id)
            .Take(50)
            .ToListAsync(ct);
        foreach (var s in subjects)
            Add("Subject", s.Id.ToString(), s.TenantId, s.SemesterId, s.GroupId,
                DownstreamConsumerStatus.Legacy,
                "Subject references NULL-group Semester (historical catalog).");

        var students = await _db.Students.AsNoTracking()
            .Where(s => !s.IsDeleted && nullIds.Contains(s.SemesterId))
            .Select(s => new { s.Id, s.TenantId, s.SemesterId, s.GroupId })
            .OrderBy(s => s.TenantId).ThenBy(s => s.Id)
            .Take(50)
            .ToListAsync(ct);
        foreach (var s in students)
            Add("Student", s.Id.ToString(), s.TenantId, s.SemesterId, s.GroupId,
                DownstreamConsumerStatus.Legacy,
                "Student references NULL-group Semester.");

        return list
            .OrderBy(f => f.Status)
            .ThenBy(f => f.Entity, StringComparer.Ordinal)
            .ThenBy(f => f.TenantId)
            .ThenBy(f => f.RecordId, StringComparer.Ordinal)
            .ThenBy(f => f.SemesterId)
            .ToList();
    }

    private static IReadOnlyList<WildcardDependencyAuditRowDto> AuditWildcardDependencies()
    {
        var root = FindRepoRoot();
        var rows = new List<WildcardDependencyAuditRowDto>();

        void Add(string path, string location, WildcardDependencyKind kind, string notes, bool blocks)
        {
            rows.Add(new WildcardDependencyAuditRowDto
            {
                Path = path,
                Location = location,
                Kind = kind,
                KindCode = kind switch
                {
                    WildcardDependencyKind.ActiveProduction => "ACTIVE_PRODUCTION",
                    WildcardDependencyKind.LegacyReadCompatibility => "LEGACY_READ_COMPATIBILITY",
                    WildcardDependencyKind.HistoricalDisplayOnly => "HISTORICAL_DISPLAY_ONLY",
                    _ => "DEAD_UNREACHABLE",
                },
                Notes = notes,
                BlocksHardening = blocks,
                ClosureStatus = blocks ? "ACTIVE_LEGACY_DEPENDENCY" : "CLOSED",
            });
        }

        if (root is null)
        {
            Add("RepoRoot", "runtime", WildcardDependencyKind.ActiveProduction,
                "Unable to locate repo root for source wildcard verification.", true);
            return rows;
        }

        var tree = SafeRead(Path.Combine(root, "Abhyanvaya.Application", "Academic", "AcademicTreeService.cs"));
        var setup = SafeRead(Path.Combine(root, "abhyanvaya-ui", "src", "services", "setupService.ts"));
        var form = SafeRead(Path.Combine(root, "abhyanvaya-ui", "src", "pages", "setup", "scheduling", "schedulingFormUtils.ts"));
        var cascade = SafeRead(Path.Combine(root, "abhyanvaya-ui", "src", "utils", "academicCascade.ts"));
        var elective = SafeRead(Path.Combine(root, "abhyanvaya-ui", "src", "pages", "setup", "ElectiveGroupsPage.tsx"));
        var students = SafeRead(Path.Combine(root, "abhyanvaya-ui", "src", "pages", "StudentsPage.tsx"));
        var semestersPage = SafeRead(Path.Combine(root, "abhyanvaya-ui", "src", "pages", "setup", "SemestersPage.tsx"));
        var subjectsPage = SafeRead(Path.Combine(root, "abhyanvaya-ui", "src", "pages", "setup", "SubjectsPage.tsx"));
        var semesterController = SafeRead(Path.Combine(root, "Abhyanvaya.API", "Controllers", "SemesterController.cs"));

        bool HasActive(string src, string pattern) =>
            !string.IsNullOrEmpty(src) && src.Contains(pattern, StringComparison.Ordinal);

        if (HasActive(tree, "s.GroupId == null || s.GroupId == g.Id"))
            Add("AcademicTreeService", "AcademicTreeService.cs", WildcardDependencyKind.ActiveProduction,
                "Operational tree still places NULL-group Semesters under Groups.", true);
        else
            Add("AcademicTreeService", "AcademicTreeService.cs", WildcardDependencyKind.DeadUnreachable,
                "Operational wildcard retired (Group-specific only).", false);

        if (HasActive(setup, "s.groupId == null || Number(s.groupId) === gid"))
            Add("filterSemestersForScope", "setupService.ts", WildcardDependencyKind.ActiveProduction,
                "UI scope filter still includes NULL-group Semesters.", true);
        else
            Add("filterSemestersForScope", "setupService.ts", WildcardDependencyKind.DeadUnreachable,
                "Operational filter excludes NULL-group Semesters.", false);

        if (HasActive(form, "s.groupId == null || s.groupId === groupId"))
            Add("schedulingFormUtils", "schedulingFormUtils.ts", WildcardDependencyKind.ActiveProduction,
                "Scheduling semester resolver still includes NULL-group.", true);
        else
            Add("schedulingFormUtils", "schedulingFormUtils.ts", WildcardDependencyKind.DeadUnreachable,
                "Scheduling resolver is Group-specific.", false);

        if (HasActive(cascade, "groupId == null") && HasActive(cascade, "return semesters.filter((s) => Number(s.courseId) === Number(courseId));"))
            Add("academicCascade", "academicCascade.ts", WildcardDependencyKind.ActiveProduction,
                "Cascade may still return NULL-group when group unset.", true);
        else
            Add("academicCascade", "academicCascade.ts", WildcardDependencyKind.DeadUnreachable,
                "Cascade excludes NULL-group operational rows.", false);

        if (HasActive(elective, "s.groupId == null || s.groupId ==="))
            Add("ElectiveGroupsPage", "ElectiveGroupsPage.tsx", WildcardDependencyKind.ActiveProduction,
                "Elective Groups still selects NULL-group Semesters.", true);
        else
            Add("ElectiveGroupsPage", "ElectiveGroupsPage.tsx", WildcardDependencyKind.DeadUnreachable,
                "Elective Groups uses Group-specific Semesters.", false);

        if (HasActive(students, "s.groupId == null || Number(s.groupId) === groupFilter"))
            Add("StudentsPage", "StudentsPage.tsx", WildcardDependencyKind.ActiveProduction,
                "Students filter still includes NULL-group Semesters.", true);
        else
            Add("StudentsPage", "StudentsPage.tsx", WildcardDependencyKind.DeadUnreachable,
                "Students filter is Group-specific.", false);

        if (HasActive(subjectsPage, "s.groupId == null || Number(s.groupId)")
            || HasActive(subjectsPage, "x.groupId == null || x.groupId ==="))
            Add("SubjectsPage", "SubjectsPage.tsx", WildcardDependencyKind.ActiveProduction,
                "Subjects page still selects NULL-group Semesters as wildcards.", true);
        else
            Add("SubjectsPage", "SubjectsPage.tsx", WildcardDependencyKind.DeadUnreachable,
                "Subjects page uses filterSemestersForScope (Group-specific).", false);

        if (HasActive(semestersPage, "Legacy / Historical") || HasActive(semestersPage, "IsLegacyCourseWide"))
            Add("SemestersPage", "SemestersPage.tsx", WildcardDependencyKind.HistoricalDisplayOnly,
                "Admin list labels historical NULL-group rows; not operational resolution.", false);

        if (HasActive(semesterController, "IsLegacyCourseWide = x.GroupId == null"))
            Add("SemesterController", "SemesterController.cs", WildcardDependencyKind.LegacyReadCompatibility,
                "API exposes IsLegacyCourseWide for historical display; create/update require Group.", false);

        return rows;
    }

    private static (bool WriteOk, bool NoNullWrite, List<string> Notes) VerifyWritePaths()
    {
        var notes = new List<string>();
        var root = FindRepoRoot();
        if (root is null)
        {
            notes.Add("Write-path verification skipped: repo root not found.");
            return (false, false, notes);
        }

        var controller = SafeRead(Path.Combine(root, "Abhyanvaya.API", "Controllers", "SemesterController.cs"));
        var rules = SafeRead(Path.Combine(root, "Abhyanvaya.Application", "Academic", "SemesterGroupOwnershipRules.cs"));
        var createDto = SafeRead(Path.Combine(root, "Abhyanvaya.Application", "DTOs", "Semester", "CreateSemesterRequest.cs"));

        var hasRules = rules.Contains("Group is required for a Semester", StringComparison.Ordinal)
                       && controller.Contains("SemesterGroupOwnershipRules", StringComparison.Ordinal);
        var createUsesDecision = controller.Contains("GroupId = decision.AlignedGroupId", StringComparison.Ordinal);
        var noNullableGroupDto = createDto.Contains("public int GroupId", StringComparison.Ordinal)
                                 && !createDto.Contains("public int? GroupId", StringComparison.Ordinal);

        notes.Add($"Write-path: OwnershipRules={hasRules}; AlignedGroupId assignment={createUsesDecision}; DTO GroupId required={noNullableGroupDto}.");
        return (hasRules && createUsesDecision, hasRules && createUsesDecision && noNullableGroupDto, notes);
    }

    private static bool VerifyArchitectureGuardsIntact()
    {
        var root = FindRepoRoot();
        if (root is null) return false;
        var tg = SafeRead(Path.Combine(root, "Abhyanvaya.Application.UnitTests", "Scheduling", "AiSchedTg6FinalArchitectureGuardTests.cs"));
        var cap = SafeRead(Path.Combine(root, "Abhyanvaya.Application.UnitTests", "Scheduling", "AiSchedCapPrompt11EndToEndAcceptanceGuardTests.cs"));
        return tg.Contains("IgnoreQueryFilters", StringComparison.Ordinal)
               && cap.Length > 0;
    }

    private static SemesterSchemaHardeningFindingDto Finding(
        string code,
        SemesterSchemaHardeningFindingSeverity severity,
        string entity,
        int? entityId,
        int? tenantId,
        string current,
        string expected,
        string reason,
        string remediation,
        string module)
        => new()
        {
            Code = code,
            Severity = severity,
            SeverityCode = severity.ToString().ToUpperInvariant(),
            Entity = entity,
            EntityId = entityId,
            TenantId = tenantId,
            CurrentState = current,
            ExpectedState = expected,
            Reason = reason,
            RequiredRemediation = remediation,
            OwningModule = module,
            RequiresSeparateApprovedPrompt = true,
        };

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Abhyanvaya.sln"))
                || Directory.Exists(Path.Combine(dir.FullName, "Abhyanvaya.Domain")))
                return dir.FullName;
            dir = dir.Parent;
        }

        var cwd = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (cwd is not null)
        {
            if (File.Exists(Path.Combine(cwd.FullName, "Abhyanvaya.sln"))
                || Directory.Exists(Path.Combine(cwd.FullName, "Abhyanvaya.Domain")))
                return cwd.FullName;
            cwd = cwd.Parent;
        }

        return null;
    }

    private static string SafeRead(string path)
    {
        try { return File.Exists(path) ? File.ReadAllText(path) : ""; }
        catch { return ""; }
    }

    private readonly record struct IdSem(string Id, int SemesterId);
    private readonly record struct JournalSnap(int SemesterId, string DispositionCode, string? PromptCode, string Evidence, DateTime FinalizedUtc);
}
