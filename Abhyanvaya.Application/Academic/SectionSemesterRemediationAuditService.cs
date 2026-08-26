using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3G.1 —
/// Post-execution / readiness audit for controlled Section Semester remediation.
/// Zero SaveChanges. Zero Section/TG/TGS/SA/TT/Attendance/Student mutation. Zero DDL.
/// </summary>
public sealed class SectionSemesterRemediationAuditService : ISectionSemesterRemediationAuditService
{
    public const string PromptCode = "P1-4-3G.1";
    public const int ExpectedLegacySemesterId = 3;
    public const int ExpectedSemesterNumber = 3;
    public const int ExpectedFinanceTargetSemesterId = 10;
    public const int ExpectedCaTargetSemesterId = 11;

    private static readonly string ResolutionPrecedenceText =
        "1) Explicit Section.GroupId when Group exists, same tenant, and CourseId matches Group.CourseId. "
        + "2) Unanimous TeachingGroup.GroupId across linked TeachingGroups (same tenant/course). "
        + "3) Unanimous Student.GroupId via current StudentSection membership. "
        + "4) Unanimous SubjectAllocation.GroupId via TeachingGroup.SubjectAllocationId. "
        + "Never infer from Section name, student counts, or majority voting.";

    private static readonly string FutureExecutionContractText =
        "Future execution MUST: (a) single transaction boundary; (b) optimistic concurrency on Section rows; "
        + "(c) deterministic mapping only; (d) fail-closed with zero partial updates; "
        + "(e) idempotent second execution (AlreadyCorrect); (f) full rollback on failure; "
        + "(g) post-execution integrity audit. Must NOT mutate TG/TGS/membership/SA/TT/TimetableSection/"
        + "Attendance/StudentSection/Student/Semester ownership.";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SectionSemesterRemediationAuditService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<SectionSemesterRemediationAuditResultDto> BuildAuditAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var notes = new List<string>
        {
            "Prompt 3G.1 Section Semester remediation AUDIT — read-only.",
            $"PromptCode={PromptCode}. Zero SaveChanges. Zero mutations.",
            "Scope: Sections on legacy Sem 3, plus Sem III Sections already on Finance/CA targets (ALREADY_CORRECT evidence).",
            ResolutionPrecedenceText,
        };
        var warnings = new List<string>();
        var blocking = new List<string>();

        var legacy = await _db.Semesters.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.Id == ExpectedLegacySemesterId, cancellationToken);

        if (legacy is null)
            blocking.Add("Legacy Semester Id=3 not found for tenant.");
        else if (legacy.Number != ExpectedSemesterNumber)
            blocking.Add($"Legacy Semester Id=3 Number={legacy.Number}; expected {ExpectedSemesterNumber}.");

        var (financeOk, financeNotes, financeTarget) = await ValidateTargetAsync(
            tenantId, ExpectedFinanceTargetSemesterId, "Finance", cancellationToken);
        var (caOk, caNotes, caTarget) = await ValidateTargetAsync(
            tenantId, ExpectedCaTargetSemesterId, "CA", cancellationToken);

        if (!financeOk) blocking.Add(financeNotes);
        if (!caOk) blocking.Add(caNotes);

        int? financeGroupId = financeTarget?.GroupId;
        int? caGroupId = caTarget?.GroupId;
        int? financeCourseId = financeTarget?.CourseId;
        int? caCourseId = caTarget?.CourseId;

        if (financeOk && caOk && financeGroupId == caGroupId)
            blocking.Add("Finance and CA target Semesters resolve to the same GroupId; fail closed.");

        // Discovery: legacy Sem 3 + already on targets for Sem III course path
        var candidateQuery = _db.Sections.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted
                        && (s.SemesterId == ExpectedLegacySemesterId
                            || s.SemesterId == ExpectedFinanceTargetSemesterId
                            || s.SemesterId == ExpectedCaTargetSemesterId));

        var sections = await candidateQuery.OrderBy(s => s.Id).ToListAsync(cancellationToken);

        // Restrict ALREADY_CORRECT candidates to matching Course when targets known
        if (financeCourseId is int fc && caCourseId is int cc)
        {
            sections = sections
                .Where(s => s.SemesterId == ExpectedLegacySemesterId
                            || (s.SemesterId == ExpectedFinanceTargetSemesterId && s.CourseId == fc)
                            || (s.SemesterId == ExpectedCaTargetSemesterId && s.CourseId == cc))
                .ToList();
        }

        var sectionIds = sections.Select(s => s.Id).ToList();
        var courseIds = sections.Select(s => s.CourseId).Distinct().ToList();
        var groupIds = sections.Select(s => s.GroupId).Distinct().ToList();

        var courses = await _db.Courses.AsNoTracking()
            .Where(c => !c.IsDeleted && courseIds.Contains(c.Id))
            .Select(c => new { c.Id, c.TenantId })
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var groups = await _db.Groups.AsNoTracking()
            .Where(g => !g.IsDeleted && groupIds.Contains(g.Id))
            .Select(g => new { g.Id, g.TenantId, g.CourseId })
            .ToDictionaryAsync(g => g.Id, cancellationToken);

        var semesterIdsForMeta = sections.Select(s => s.SemesterId)
            .Concat([ExpectedLegacySemesterId, ExpectedFinanceTargetSemesterId, ExpectedCaTargetSemesterId])
            .Distinct()
            .ToList();

        var semesterMeta = await _db.Semesters.AsNoTracking()
            .Where(s => !s.IsDeleted && semesterIdsForMeta.Contains(s.Id))
            .Select(s => new { s.Id, s.Number, s.TenantId, s.CourseId, s.GroupId })
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var tgsLinks = sectionIds.Count == 0
            ? []
            : await _db.SchedulingTeachingGroupSections.AsNoTracking()
                .Where(x => !x.IsDeleted && sectionIds.Contains(x.SectionId))
                .Select(x => new { x.Id, x.TeachingGroupId, x.SectionId })
                .ToListAsync(cancellationToken);

        var tgIds = tgsLinks.Select(x => x.TeachingGroupId).Distinct().ToList();
        var teachingGroups = tgIds.Count == 0
            ? new Dictionary<int, TgSnap>()
            : (await _db.SchedulingTeachingGroups.AsNoTracking()
                .Where(t => !t.IsDeleted && tgIds.Contains(t.Id))
                .Select(t => new { t.Id, t.TenantId, t.CourseId, t.GroupId, t.SemesterId, t.SubjectAllocationId })
                .ToListAsync(cancellationToken))
            .ToDictionary(t => t.Id, t => new TgSnap(t.Id, t.TenantId, t.CourseId, t.GroupId, t.SemesterId, t.SubjectAllocationId));

        var studentSectionCounts = sectionIds.Count == 0
            ? new Dictionary<int, int>()
            : await _db.StudentSections.AsNoTracking()
                .Where(ss => !ss.IsDeleted && sectionIds.Contains(ss.SectionId))
                .GroupBy(ss => ss.SectionId)
                .Select(g => new { SectionId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SectionId, x => x.Count, cancellationToken);

        var studentGroupEvidence = sectionIds.Count == 0
            ? new Dictionary<int, List<int>>()
            : await LoadStudentGroupEvidenceAsync(sectionIds, cancellationToken);

        var ttSectionCounts = sectionIds.Count == 0
            ? new Dictionary<int, int>()
            : await _db.TimetableSections.AsNoTracking()
                .Where(ts => !ts.IsDeleted && sectionIds.Contains(ts.SectionId))
                .GroupBy(ts => ts.SectionId)
                .Select(g => new { SectionId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SectionId, x => x.Count, cancellationToken);

        var attendanceSectionCounts = sectionIds.Count == 0
            ? new Dictionary<int, int>()
            : await _db.AttendanceSessionSections.AsNoTracking()
                .Where(a => sectionIds.Contains(a.SectionId))
                .GroupBy(a => a.SectionId)
                .Select(g => new { SectionId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SectionId, x => x.Count, cancellationToken);

        var saIds = teachingGroups.Values.Select(t => t.SubjectAllocationId).Distinct().ToList();
        var saById = saIds.Count == 0
            ? new Dictionary<int, SaSnap>()
            : (await _db.SchedulingSubjectAllocations.AsNoTracking()
                .Where(a => !a.IsDeleted && saIds.Contains(a.Id))
                .Select(a => new { a.Id, a.TenantId, a.GroupId, a.CourseId, a.SemesterId })
                .ToListAsync(cancellationToken))
            .ToDictionary(a => a.Id, a => new SaSnap(a.Id, a.TenantId, a.GroupId, a.CourseId, a.SemesterId));

        var ttEntryCountsBySection = new Dictionary<int, int>();
        foreach (var sid in sectionIds)
        {
            // TimetableEntry has no SectionId; count via TimetableSection → TimetableEntryId when present
            ttEntryCountsBySection[sid] = 0;
        }

        if (sectionIds.Count > 0)
        {
            var entryLinks = await _db.TimetableSections.AsNoTracking()
                .Where(ts => !ts.IsDeleted && sectionIds.Contains(ts.SectionId) && ts.TimetableEntryId != null)
                .Select(ts => new { ts.SectionId, ts.TimetableEntryId })
                .ToListAsync(cancellationToken);
            foreach (var g in entryLinks.GroupBy(x => x.SectionId))
                ttEntryCountsBySection[g.Key] = g.Select(x => x.TimetableEntryId!.Value).Distinct().Count();
        }

        var sectionRows = new List<SectionSemesterAuditSectionRowDto>();
        var tgsRows = new List<SectionSemesterAuditTgsRowDto>();

        foreach (var section in sections)
        {
            var blockers = new List<string>();
            var isLegacy = section.SemesterId == ExpectedLegacySemesterId;

            // Tenant / course / group validity
            if (section.TenantId != tenantId)
                blockers.Add("Section tenant mismatch with ambient tenant.");

            courses.TryGetValue(section.CourseId, out var course);
            if (course is null)
                blockers.Add($"Course Id={section.CourseId} missing.");
            else if (course.TenantId != section.TenantId)
                blockers.Add("Cross-tenant Section→Course relationship.");

            groups.TryGetValue(section.GroupId, out var group);
            if (group is null)
                blockers.Add($"Group Id={section.GroupId} missing.");
            else if (group.TenantId != section.TenantId)
                blockers.Add("Cross-tenant Section→Group relationship.");
            else if (group.CourseId != section.CourseId)
                blockers.Add($"Section.CourseId={section.CourseId} != Group.CourseId={group.CourseId}.");

            semesterMeta.TryGetValue(section.SemesterId, out var currentSem);
            if (currentSem is null && isLegacy)
                blockers.Add($"Current Semester Id={section.SemesterId} missing.");
            else if (currentSem is not null && currentSem.TenantId != section.TenantId)
                blockers.Add("Cross-tenant Section→Semester relationship.");

            var linkedTgs = tgsLinks.Where(x => x.SectionId == section.Id).ToList();
            var linkedTgSnaps = linkedTgs
                .Select(x => teachingGroups.TryGetValue(x.TeachingGroupId, out var tg) ? tg : null)
                .Where(x => x is not null)
                .Cast<TgSnap>()
                .ToList();

            // Resolution
            var (resolvedGroupId, resolutionReason, deterministic) = ResolveGroup(
                section,
                group?.CourseId,
                linkedTgSnaps,
                studentGroupEvidence.TryGetValue(section.Id, out var sg) ? sg : [],
                linkedTgSnaps
                    .Select(t => saById.TryGetValue(t.SubjectAllocationId, out var sa) ? sa : null)
                    .Where(x => x is not null)
                    .Cast<SaSnap>()
                    .ToList());

            int? targetSemesterId = null;
            var classification = SectionSemesterAuditClassification.ManualMappingRequired;
            var classificationCode = "MANUAL_MAPPING_REQUIRED";
            var isDeterministic = deterministic;
            var confidence = deterministic ? "High" : "Low";

            if (blockers.Any(b => b.Contains("Cross-tenant", StringComparison.Ordinal)
                                  || b.Contains("missing", StringComparison.OrdinalIgnoreCase)
                                  || b.Contains("!=", StringComparison.Ordinal)))
            {
                classification = SectionSemesterAuditClassification.InvalidReference;
                classificationCode = "INVALID_REFERENCE";
                isDeterministic = false;
                confidence = "None";
            }
            else if (!isLegacy
                     && financeOk
                     && section.SemesterId == ExpectedFinanceTargetSemesterId
                     && financeGroupId is int fg
                     && section.GroupId == fg
                     && financeCourseId is int fCourse
                     && section.CourseId == fCourse)
            {
                classification = SectionSemesterAuditClassification.AlreadyCorrect;
                classificationCode = "ALREADY_CORRECT";
                targetSemesterId = ExpectedFinanceTargetSemesterId;
                resolvedGroupId = section.GroupId;
                resolutionReason = "Already on Finance target Semester with matching Group/Course.";
                isDeterministic = true;
                confidence = "High";
            }
            else if (!isLegacy
                     && caOk
                     && section.SemesterId == ExpectedCaTargetSemesterId
                     && caGroupId is int cg
                     && section.GroupId == cg
                     && caCourseId is int cCourse
                     && section.CourseId == cCourse)
            {
                classification = SectionSemesterAuditClassification.AlreadyCorrect;
                classificationCode = "ALREADY_CORRECT";
                targetSemesterId = ExpectedCaTargetSemesterId;
                resolvedGroupId = section.GroupId;
                resolutionReason = "Already on CA target Semester with matching Group/Course.";
                isDeterministic = true;
                confidence = "High";
            }
            else if (!isLegacy)
            {
                // On target id but Course/Group mismatch
                classification = SectionSemesterAuditClassification.Blocked;
                classificationCode = "BLOCKED";
                blockers.Add("Section on target Semester Id but Course/Group does not match validated target ownership.");
                isDeterministic = false;
                confidence = "None";
            }
            else if (!financeOk || !caOk)
            {
                classification = SectionSemesterAuditClassification.Blocked;
                classificationCode = "BLOCKED";
                blockers.Add("Target Semester validation failed; remediation blocked.");
                isDeterministic = false;
                confidence = "None";
            }
            else if (resolvedGroupId is null || !deterministic)
            {
                classification = SectionSemesterAuditClassification.ManualMappingRequired;
                classificationCode = "MANUAL_MAPPING_REQUIRED";
                resolutionReason = string.IsNullOrWhiteSpace(resolutionReason)
                    ? "Unable to deterministically resolve ownership Group."
                    : resolutionReason;
                isDeterministic = false;
                confidence = "Low";
            }
            else if (financeGroupId is int fgid && resolvedGroupId == fgid
                     && financeCourseId is int fcid && section.CourseId == fcid)
            {
                classification = SectionSemesterAuditClassification.SafeForFinance;
                classificationCode = "SAFE_FOR_FINANCE";
                targetSemesterId = ExpectedFinanceTargetSemesterId;
                resolutionReason = resolutionReason;
                confidence = "High";
            }
            else if (caGroupId is int cgid && resolvedGroupId == cgid
                     && caCourseId is int ccid && section.CourseId == ccid)
            {
                classification = SectionSemesterAuditClassification.SafeForCa;
                classificationCode = "SAFE_FOR_CA";
                targetSemesterId = ExpectedCaTargetSemesterId;
                resolutionReason = resolutionReason;
                confidence = "High";
            }
            else
            {
                classification = SectionSemesterAuditClassification.ManualMappingRequired;
                classificationCode = "MANUAL_MAPPING_REQUIRED";
                resolutionReason =
                    $"Resolved GroupId={resolvedGroupId} is neither Finance GroupId={financeGroupId} nor CA GroupId={caGroupId} for Course.";
                isDeterministic = false;
                confidence = "Low";
                targetSemesterId = null;
            }

            // TGS compatibility
            var incompatibleTgs = false;
            foreach (var link in linkedTgs)
            {
                teachingGroups.TryGetValue(link.TeachingGroupId, out var tg);
                var (compat, compatCode, compatNotes) = EvaluateTgsCompatibility(
                    section, tg, targetSemesterId, tenantId);
                if (compat == TeachingGroupSectionCompatibilityStatus.Incompatible
                    || compat == TeachingGroupSectionCompatibilityStatus.CrossTenant)
                {
                    incompatibleTgs = true;
                    blockers.Add(compatNotes);
                }
                else if (compat == TeachingGroupSectionCompatibilityStatus.InterimLegacyTgAllowed)
                {
                    warnings.Add($"Section {section.Id}: {compatNotes}");
                }

                tgsRows.Add(new SectionSemesterAuditTgsRowDto
                {
                    TeachingGroupSectionId = link.Id,
                    TeachingGroupId = link.TeachingGroupId,
                    SectionId = section.Id,
                    TeachingGroupSemesterId = tg?.SemesterId,
                    SectionSemesterId = section.SemesterId,
                    TeachingGroupGroupId = tg?.GroupId,
                    ResolvedTargetSemesterId = targetSemesterId,
                    Compatibility = compat,
                    CompatibilityCode = compatCode,
                    Notes = compatNotes,
                });
            }

            if (incompatibleTgs
                && classification is SectionSemesterAuditClassification.SafeForFinance
                    or SectionSemesterAuditClassification.SafeForCa)
            {
                classification = SectionSemesterAuditClassification.Blocked;
                classificationCode = "BLOCKED";
                isDeterministic = false;
                confidence = "None";
            }

            if (classification == SectionSemesterAuditClassification.InvalidReference
                || blockers.Count > 0 && classification != SectionSemesterAuditClassification.AlreadyCorrect
                    && classification is not SectionSemesterAuditClassification.SafeForFinance
                    and not SectionSemesterAuditClassification.SafeForCa)
            {
                // Keep INVALID if already set; otherwise elevate to BLOCKED when blockers exist on legacy path
                if (classification != SectionSemesterAuditClassification.InvalidReference
                    && classification != SectionSemesterAuditClassification.ManualMappingRequired
                    && isLegacy
                    && blockers.Count > 0)
                {
                    classification = SectionSemesterAuditClassification.Blocked;
                    classificationCode = "BLOCKED";
                }
            }

            // SA count via linked TGs (indirect)
            var saCount = linkedTgSnaps
                .Select(t => t.SubjectAllocationId)
                .Distinct()
                .Count(id => saById.ContainsKey(id));

            sectionRows.Add(new SectionSemesterAuditSectionRowDto
            {
                SectionId = section.Id,
                SectionCode = section.SectionCode,
                SectionName = section.SectionName,
                TenantId = section.TenantId,
                CourseId = section.CourseId,
                CurrentSemesterId = section.SemesterId,
                CurrentSemesterNumber = currentSem?.Number,
                CurrentGroupId = section.GroupId,
                ResolvedGroupId = resolvedGroupId,
                TargetSemesterId = targetSemesterId,
                Classification = classification,
                ClassificationCode = classificationCode,
                ResolutionReason = resolutionReason,
                IsDeterministic = isDeterministic,
                Confidence = confidence,
                TeachingGroupSectionCount = linkedTgs.Count,
                StudentSectionCount = studentSectionCounts.GetValueOrDefault(section.Id),
                SubjectAllocationCount = saCount,
                TimetableEntryCount = ttEntryCountsBySection.GetValueOrDefault(section.Id),
                TimetableSectionCount = ttSectionCounts.GetValueOrDefault(section.Id),
                AttendanceSessionSectionCount = attendanceSectionCounts.GetValueOrDefault(section.Id),
                BlockingReasons = blockers,
            });
        }

        var legacyRows = sectionRows.Where(r => r.CurrentSemesterId == ExpectedLegacySemesterId).ToList();
        var safeFinance = legacyRows.Count(r => r.Classification == SectionSemesterAuditClassification.SafeForFinance);
        var safeCa = legacyRows.Count(r => r.Classification == SectionSemesterAuditClassification.SafeForCa);
        var already = sectionRows.Count(r => r.Classification == SectionSemesterAuditClassification.AlreadyCorrect);
        var manual = legacyRows.Count(r => r.Classification == SectionSemesterAuditClassification.ManualMappingRequired);
        var blocked = sectionRows.Count(r => r.Classification == SectionSemesterAuditClassification.Blocked);
        var invalid = sectionRows.Count(r => r.Classification == SectionSemesterAuditClassification.InvalidReference);
        var tgsDepCount = tgsRows.Count;

        // Readiness: every remaining legacy Section must be SAFE_FOR_* (deterministic), targets valid, no blockers/invalid/manual
        var legacyUnresolved = legacyRows.Any(r =>
            r.Classification is not SectionSemesterAuditClassification.SafeForFinance
                and not SectionSemesterAuditClassification.SafeForCa);

        if (legacyUnresolved)
            blocking.Add("One or more legacy Sem-3 Sections are not deterministically SAFE_FOR_FINANCE/SAFE_FOR_CA.");
        if (invalid > 0)
            blocking.Add($"InvalidReference count={invalid}.");
        if (blocked > 0)
            blocking.Add($"Blocked count={blocked}.");
        if (manual > 0)
            blocking.Add($"ManualMappingRequired count={manual}.");

        // Cross-tenant findings already in per-row blockers
        if (sectionRows.Any(r => r.BlockingReasons.Any(b => b.Contains("Cross-tenant", StringComparison.Ordinal))))
            blocking.Add("Cross-tenant relationship detected.");

        var ready = financeOk && caOk
                    && legacy is not null
                    && !legacyUnresolved
                    && invalid == 0
                    && blocked == 0
                    && manual == 0
                    && blocking.Count == 0;

        // Deduplicate blocking reasons
        blocking = blocking.Distinct(StringComparer.Ordinal).ToList();

        // Recompute ready after dedupe — if only "legacy unresolved" etc.
        ready = financeOk && caOk
                && legacy is not null
                && !legacyUnresolved
                && invalid == 0
                && blocked == 0
                && manual == 0;

        if (!ready && blocking.Count == 0)
            blocking.Add("NOT_READY: see classification counts.");

        notes.Add($"Legacy Sem-3 Sections={legacyRows.Count}; AlreadyCorrect={already}; TGS deps={tgsDepCount}.");
        notes.Add($"Readiness={(ready ? "READY" : "NOT_READY")}.");

        return new SectionSemesterRemediationAuditResultDto
        {
            GeneratedUtc = DateTime.UtcNow,
            PromptCode = PromptCode,
            TenantId = tenantId,
            IsReadOnly = true,
            NoMutationsPerformed = true,
            SaveChangesInvoked = false,
            LegacySemesterId = ExpectedLegacySemesterId,
            FinanceTargetSemesterId = ExpectedFinanceTargetSemesterId,
            CaTargetSemesterId = ExpectedCaTargetSemesterId,
            FinanceTargetValid = financeOk,
            CaTargetValid = caOk,
            FinanceTargetValidationNotes = financeNotes,
            CaTargetValidationNotes = caNotes,
            TotalLegacySections = legacyRows.Count,
            SafeFinanceCount = safeFinance,
            SafeCaCount = safeCa,
            AlreadyCorrectCount = already,
            ManualMappingCount = manual,
            BlockedCount = blocked,
            InvalidCount = invalid,
            TeachingGroupSectionDependencyCount = tgsDepCount,
            Readiness = ready ? SectionSemesterAuditReadiness.Ready : SectionSemesterAuditReadiness.NotReady,
            ReadinessCode = ready ? "READY" : "NOT_READY",
            IsReady = ready,
            BlockingReasons = blocking,
            Warnings = warnings.Distinct(StringComparer.Ordinal).ToList(),
            Notes = notes,
            Sections = sectionRows,
            TeachingGroupSections = tgsRows,
            ResolutionPrecedence = ResolutionPrecedenceText,
            FutureExecutionContract = FutureExecutionContractText,
            RecommendedNextPrompt = ready
                ? (legacyRows.Count == 0
                    ? "No remaining Sem-3 Sections. Controlled Section remediation is complete for this tenant; next Architect prompt may address remaining schema-hardening NO_GO blockers (NULL-group Semesters / Subject historical), not Section remap."
                    : "Chief Architect may authorize a controlled Section.SemesterId execution prompt (Finance→10 / CA→11) under the documented transaction/idempotency contract. Do not mutate TG/TGS in that prompt.")
                : "Do NOT execute Section remediation. Resolve BlockingReasons / MANUAL_MAPPING / INVALID / target validation first under Architect approval.",
        };
    }

    private async Task<(bool Ok, string Notes, TargetSnap? Target)> ValidateTargetAsync(
        int tenantId,
        int semesterId,
        string label,
        CancellationToken ct)
    {
        var sem = await _db.Semesters.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.Id == semesterId, ct);
        if (sem is null)
            return (false, $"{label} target Semester Id={semesterId} not found.", null);
        if (sem.GroupId is null)
            return (false, $"{label} target Semester Id={semesterId} has NULL GroupId.", null);
        if (sem.Number != ExpectedSemesterNumber)
            return (false, $"{label} target Semester Id={semesterId} Number={sem.Number}; expected {ExpectedSemesterNumber}.", null);
        if (sem.TenantId != tenantId)
            return (false, $"{label} target Semester Id={semesterId} cross-tenant.", null);

        var group = await _db.Groups.AsNoTracking()
            .FirstOrDefaultAsync(g => !g.IsDeleted && g.Id == sem.GroupId.Value, ct);
        if (group is null)
            return (false, $"{label} target Group Id={sem.GroupId} missing.", null);
        if (group.TenantId != tenantId)
            return (false, $"{label} target Group Id={sem.GroupId} cross-tenant.", null);
        if (group.CourseId != sem.CourseId)
            return (false, $"{label} Semester.CourseId={sem.CourseId} != Group.CourseId={group.CourseId}.", null);

        var course = await _db.Courses.AsNoTracking()
            .FirstOrDefaultAsync(c => !c.IsDeleted && c.Id == sem.CourseId, ct);
        if (course is null)
            return (false, $"{label} target Course Id={sem.CourseId} missing.", null);
        if (course.TenantId != tenantId)
            return (false, $"{label} target Course Id={sem.CourseId} cross-tenant.", null);

        return (true,
            $"{label} target Sem={semesterId} GroupId={sem.GroupId} CourseId={sem.CourseId} valid.",
            new TargetSnap(sem.Id, sem.CourseId, sem.GroupId.Value, sem.Number, sem.TenantId));
    }

    private static (int? GroupId, string Reason, bool Deterministic) ResolveGroup(
        Domain.Entities.Academic.Section section,
        int? sectionGroupCourseId,
        IReadOnlyList<TgSnap> tgs,
        IReadOnlyList<int> studentGroupIds,
        IReadOnlyList<SaSnap> allocations)
    {
        // 1) Explicit Section.GroupId
        if (section.GroupId > 0 && sectionGroupCourseId is int gc && gc == section.CourseId)
        {
            return (section.GroupId,
                $"Resolved via explicit Section.GroupId={section.GroupId} (Course-aligned).",
                true);
        }

        if (section.GroupId > 0 && sectionGroupCourseId is null)
            return (null, $"Section.GroupId={section.GroupId} present but Group missing/invalid.", false);

        if (section.GroupId > 0 && sectionGroupCourseId != section.CourseId)
            return (null, "Section.GroupId Course mismatch; fail closed.", false);

        // 2) TeachingGroup unanimous
        var tgGroups = tgs.Select(t => t.GroupId).Distinct().ToList();
        if (tgGroups.Count == 1)
            return (tgGroups[0], $"Resolved via unanimous TeachingGroup.GroupId={tgGroups[0]}.", true);
        if (tgGroups.Count > 1)
            return (null, $"Ambiguous TeachingGroup.GroupId set=[{string.Join(",", tgGroups)}].", false);

        // 3) StudentSection unanimous
        var stuGroups = studentGroupIds.Distinct().ToList();
        if (stuGroups.Count == 1)
            return (stuGroups[0], $"Resolved via unanimous Student.GroupId={stuGroups[0]} via StudentSection.", true);
        if (stuGroups.Count > 1)
            return (null, $"Ambiguous Student.GroupId set=[{string.Join(",", stuGroups)}].", false);

        // 4) SubjectAllocation unanimous
        var saGroups = allocations.Select(a => a.GroupId).Distinct().ToList();
        if (saGroups.Count == 1)
            return (saGroups[0], $"Resolved via unanimous SubjectAllocation.GroupId={saGroups[0]}.", true);
        if (saGroups.Count > 1)
            return (null, $"Ambiguous SubjectAllocation.GroupId set=[{string.Join(",", saGroups)}].", false);

        return (null, "No authoritative Group evidence.", false);
    }

    private static (TeachingGroupSectionCompatibilityStatus Status, string Code, string Notes)
        EvaluateTgsCompatibility(
            Domain.Entities.Academic.Section section,
            TgSnap? tg,
            int? targetSemesterId,
            int ambientTenantId)
    {
        if (tg is null)
            return (TeachingGroupSectionCompatibilityStatus.MissingTeachingGroup, "MISSING_TEACHING_GROUP",
                "TeachingGroupSection references missing/deleted TeachingGroup.");

        if (tg.TenantId != section.TenantId || tg.TenantId != ambientTenantId)
            return (TeachingGroupSectionCompatibilityStatus.CrossTenant, "CROSS_TENANT",
                $"TeachingGroup {tg.Id} tenant mismatch vs Section {section.Id}.");

        if (tg.GroupId != section.GroupId)
            return (TeachingGroupSectionCompatibilityStatus.Incompatible, "INCOMPATIBLE",
                $"TeachingGroup {tg.Id} GroupId={tg.GroupId} != Section.GroupId={section.GroupId}; would remain unsafe after Section remap.");

        if (targetSemesterId is int target)
        {
            if (tg.SemesterId == target)
                return (TeachingGroupSectionCompatibilityStatus.Compatible, "COMPATIBLE",
                    $"TeachingGroup {tg.Id} already on target Sem {target}.");

            if (tg.SemesterId == ExpectedLegacySemesterId)
                return (TeachingGroupSectionCompatibilityStatus.InterimLegacyTgAllowed, "INTERIM_LEGACY_TG_ALLOWED",
                    $"TeachingGroup {tg.Id} still on legacy Sem 3; Architect sequence allows Section-first then TG remap (do not detach TGS).");

            if (tg.SemesterId == ExpectedFinanceTargetSemesterId || tg.SemesterId == ExpectedCaTargetSemesterId)
            {
                if (tg.SemesterId != target)
                    return (TeachingGroupSectionCompatibilityStatus.Incompatible, "INCOMPATIBLE",
                        $"TeachingGroup {tg.Id} on Sem {tg.SemesterId} conflicts with Section target Sem {target}.");
            }

            return (TeachingGroupSectionCompatibilityStatus.Incompatible, "INCOMPATIBLE",
                $"TeachingGroup {tg.Id} SemesterId={tg.SemesterId} incompatible with target Sem {target}.");
        }

        return (TeachingGroupSectionCompatibilityStatus.Compatible, "COMPATIBLE",
            "No target Semester resolved yet; TGS reported for evidence only.");
    }

    private async Task<Dictionary<int, List<int>>> LoadStudentGroupEvidenceAsync(
        List<int> sectionIds,
        CancellationToken ct)
    {
        var rows = await (
            from ss in _db.StudentSections.AsNoTracking()
            where !ss.IsDeleted && sectionIds.Contains(ss.SectionId)
            join st in _db.Students.AsNoTracking() on ss.StudentId equals st.Id
            where !st.IsDeleted
            select new { ss.SectionId, st.GroupId }
        ).ToListAsync(ct);

        return rows
            .GroupBy(x => x.SectionId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.GroupId).ToList());
    }

    private sealed record TargetSnap(int Id, int CourseId, int GroupId, int Number, int TenantId);
    private sealed record TgSnap(int Id, int TenantId, int CourseId, int GroupId, int SemesterId, int SubjectAllocationId);
    private sealed record SaSnap(int Id, int TenantId, int GroupId, int CourseId, int SemesterId);
}
