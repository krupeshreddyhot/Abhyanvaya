using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3D —
/// Read-only legacy Semester finalization inventory + DB hardening discovery.
/// Zero writes. Teaching Groups identify-only.
/// </summary>
public sealed class LegacySemesterFinalizationAuditService : ILegacySemesterFinalizationAuditService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILegacySemesterMigrationAuditService _prompt2B;
    private readonly ILegacySemesterMigrationDecisionPlanService _prompt3A;

    public LegacySemesterFinalizationAuditService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ILegacySemesterMigrationAuditService prompt2B,
        ILegacySemesterMigrationDecisionPlanService prompt3A)
    {
        _db = db;
        _currentUser = currentUser;
        _prompt2B = prompt2B;
        _prompt3A = prompt3A;
    }

    public async Task<LegacySemesterFinalizationAuditDto> BuildAuditAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var notes = new List<string>
        {
            "Prompt 3D is discovery/contract only — no mutations, no schema changes, no Teaching Group writes.",
        };

        var prompt2B = await _prompt2B.BuildAuditAsync(cancellationToken);
        var prompt3A = await _prompt3A.BuildDecisionPlanAsync(cancellationToken);
        var decisionBySem = prompt3A.Decisions.ToDictionary(d => d.SemesterId);
        var classBySem = prompt2B.Rows.ToDictionary(r => r.SemesterId);

        var legacy = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.GroupId == null)
            .Select(s => new
            {
                s.Id,
                s.TenantId,
                s.CourseId,
                CourseCode = s.Course != null ? s.Course.Code : "",
                CourseName = s.Course != null ? s.Course.Name : "",
                s.Number,
                s.Name,
                s.IsDeleted,
                s.CreatedDate,
                s.UpdatedDate,
            })
            .OrderBy(s => s.Id)
            .ToListAsync(cancellationToken);

        var legacyIds = legacy.Select(s => s.Id).ToList();

        var groups = await _db.Groups.AsNoTracking()
            .Where(g => g.TenantId == tenantId && !g.IsDeleted)
            .Select(g => new { g.Id, g.CourseId, g.Code, g.Name })
            .ToListAsync(cancellationToken);
        var groupsByCourse = groups.GroupBy(g => g.CourseId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var duplicateLegacyKeys = legacy
            .GroupBy(s => new { s.CourseId, s.Number })
            .Where(g => g.Count() > 1)
            .Select(g => (g.Key.CourseId, g.Key.Number))
            .ToHashSet();

        var studentCounts = await _db.Students.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && legacyIds.Contains(s.SemesterId))
            .GroupBy(s => s.SemesterId)
            .Select(g => new { SemesterId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SemesterId, x => x.Count, cancellationToken);

        var attCounts = await _db.AttendanceSessions.AsNoTracking()
            .Where(a => a.TenantId == tenantId && legacyIds.Contains(a.SemesterId))
            .GroupBy(a => a.SemesterId)
            .Select(g => new { SemesterId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SemesterId, x => x.Count, cancellationToken);

        var saCounts = await _db.SchedulingSubjectAllocations.AsNoTracking()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && legacyIds.Contains(a.SemesterId))
            .GroupBy(a => a.SemesterId)
            .Select(g => new { SemesterId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SemesterId, x => x.Count, cancellationToken);

        var ttCounts = await _db.SchedulingTimetableEntries.AsNoTracking()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted && legacyIds.Contains(e.SemesterId))
            .GroupBy(e => e.SemesterId)
            .Select(g => new { SemesterId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SemesterId, x => x.Count, cancellationToken);

        var tgCounts = await _db.SchedulingTeachingGroups.AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && legacyIds.Contains(t.SemesterId))
            .GroupBy(t => t.SemesterId)
            .Select(g => new { SemesterId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SemesterId, x => x.Count, cancellationToken);

        var subjectCounts = await _db.Subjects.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && legacyIds.Contains(s.SemesterId))
            .GroupBy(s => s.SemesterId)
            .Select(g => new { SemesterId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SemesterId, x => x.Count, cancellationToken);

        var sectionCounts = await _db.Sections.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && legacyIds.Contains(s.SemesterId))
            .GroupBy(s => s.SemesterId)
            .Select(g => new { SemesterId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SemesterId, x => x.Count, cancellationToken);

        var inventory = new List<LegacySemesterInventoryRowDto>();
        foreach (var s in legacy)
        {
            var courseGroups = groupsByCourse.GetValueOrDefault(s.CourseId) ?? [];
            var students = studentCounts.GetValueOrDefault(s.Id);
            var att = attCounts.GetValueOrDefault(s.Id);
            var sa = saCounts.GetValueOrDefault(s.Id);
            var tt = ttCounts.GetValueOrDefault(s.Id);
            var tg = tgCounts.GetValueOrDefault(s.Id);
            var subj = subjectCounts.GetValueOrDefault(s.Id);
            var sec = sectionCounts.GetValueOrDefault(s.Id);
            decisionBySem.TryGetValue(s.Id, out var d3a);
            classBySem.TryGetValue(s.Id, out var d2b);

            var (disposition, evidence) = LegacySemesterFinalizationClassifier.Classify(
                new LegacySemesterFinalizationClassifier.Input(
                    s.Id,
                    s.Number,
                    courseGroups.Count,
                    duplicateLegacyKeys.Contains((s.CourseId, s.Number)),
                    students,
                    tg,
                    att,
                    sa,
                    tt,
                    subj,
                    sec,
                    d3a?.DecisionCode));

            inventory.Add(new LegacySemesterInventoryRowDto
            {
                TenantId = s.TenantId,
                SemesterId = s.Id,
                CourseId = s.CourseId,
                CourseCode = s.CourseCode ?? "",
                CourseName = s.CourseName ?? "",
                Number = s.Number,
                Name = s.Name,
                IsDeleted = s.IsDeleted,
                CreatedDate = s.CreatedDate,
                UpdatedDate = s.UpdatedDate,
                ActiveGroupCountOnCourse = courseGroups.Count,
                GroupsOnCourse = courseGroups
                    .Select(g => new LegacyFinalizationGroupInfoDto { GroupId = g.Id, Code = g.Code, Name = g.Name })
                    .ToList(),
                StudentReferenceCount = students,
                AttendanceReferenceCount = att,
                SubjectAllocationReferenceCount = sa,
                TimetableEntryReferenceCount = tt,
                TeachingGroupReferenceCount = tg,
                SubjectReferenceCount = subj,
                SectionReferenceCount = sec,
                Prompt2BClassification = d2b?.ClassificationCode,
                Prompt3ADecision = d3a?.DecisionCode,
                Disposition = disposition,
                DispositionCode = LegacySemesterFinalizationClassifier.ToCode(disposition),
                DispositionEvidence = evidence,
            });
        }

        // --- Teaching Group residuals on any legacy NULL-group semester ---
        var tgResiduals = await BuildTeachingGroupResidualsAsync(tenantId, legacyIds, cancellationToken);

        // --- Duplicate TenantId+GroupId+Number (group-specific only) ---
        var groupSpecific = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.GroupId != null)
            .Select(s => new { s.Id, s.TenantId, GroupId = s.GroupId!.Value, s.Number })
            .ToListAsync(cancellationToken);

        var duplicates = groupSpecific
            .GroupBy(s => new { s.TenantId, s.GroupId, s.Number })
            .Where(g => g.Count() > 1)
            .Select(g => new DuplicateGroupSemesterNumberDto
            {
                TenantId = g.Key.TenantId,
                GroupId = g.Key.GroupId,
                Number = g.Key.Number,
                SemesterIds = g.Select(x => x.Id).OrderBy(id => id).ToList(),
                RemediationPlan =
                    "MANUAL: choose surviving SemesterId; remap FKs deterministically; soft-delete or archive loser; do not auto-merge.",
            })
            .ToList();

        // --- Student integrity ---
        var studentsAll = await _db.Students.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.SemesterId > 0)
            .Select(s => new { s.Id, s.CourseId, s.GroupId, s.SemesterId })
            .ToListAsync(cancellationToken);

        var semesterLookup = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Select(s => new { s.Id, s.CourseId, s.GroupId })
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var studentViolations = new List<string>();
        foreach (var st in studentsAll)
        {
            if (!semesterLookup.TryGetValue(st.SemesterId, out var sem))
            {
                studentViolations.Add($"Student {st.Id}: Semester {st.SemesterId} missing.");
                continue;
            }

            if (sem.GroupId is null || sem.GroupId.Value != st.GroupId)
                studentViolations.Add($"Student {st.Id}: Semester.GroupId={(sem.GroupId?.ToString() ?? "NULL")} != Student.GroupId={st.GroupId}.");
            if (sem.CourseId != st.CourseId)
                studentViolations.Add($"Student {st.Id}: Semester.CourseId={sem.CourseId} != Student.CourseId={st.CourseId}.");
        }

        // --- Downstream on Sem III specifically (post-3C expectation) ---
        var legacyIii = legacy.FirstOrDefault(s => s.Number == 3);
        var legacyIiiId = legacyIii?.Id ?? 0;
        var downstream = new DownstreamLegacyReferenceSummaryDto
        {
            LegacySemesterIiiId = legacyIiiId,
            Attendance = legacyIiiId == 0 ? 0 : attCounts.GetValueOrDefault(legacyIiiId),
            SubjectAllocation = legacyIiiId == 0 ? 0 : saCounts.GetValueOrDefault(legacyIiiId),
            TimetableEntry = legacyIiiId == 0 ? 0 : ttCounts.GetValueOrDefault(legacyIiiId),
            TeachingGroup = legacyIiiId == 0 ? 0 : tgCounts.GetValueOrDefault(legacyIiiId),
            Subject = legacyIiiId == 0 ? 0 : subjectCounts.GetValueOrDefault(legacyIiiId),
            Section = legacyIiiId == 0 ? 0 : sectionCounts.GetValueOrDefault(legacyIiiId),
        };

        // Course↔Group ownership sample
        var courseIds = await _db.Courses.AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);
        var courseIdSet = courseIds.ToHashSet();
        var courseGroupOk = groups.All(g => courseIdSet.Contains(g.CourseId));

        var studentsOnAnyLegacy = inventory.Sum(i => i.StudentReferenceCount);
        var attOnAnyLegacy = inventory.Sum(i => i.AttendanceReferenceCount);
        var saOnAnyLegacy = inventory.Sum(i => i.SubjectAllocationReferenceCount);
        var ttOnAnyLegacy = inventory.Sum(i => i.TimetableEntryReferenceCount);
        var tgOnAnyLegacy = inventory.Sum(i => i.TeachingGroupReferenceCount);
        var subjOnAnyLegacy = inventory.Sum(i => i.SubjectReferenceCount);
        var secOnAnyLegacy = inventory.Sum(i => i.SectionReferenceCount);

        var blocking = new List<string>();
        if (inventory.Count > 0)
            blocking.Add($"{inventory.Count} NULL-group Semester row(s) remain.");
        if (tgOnAnyLegacy > 0)
            blocking.Add($"{tgOnAnyLegacy} TeachingGroup reference(s) on legacy Semesters (separate TG prompt required).");
        if (attOnAnyLegacy > 0)
            blocking.Add($"{attOnAnyLegacy} AttendanceSession reference(s) on legacy NULL-group Semesters.");
        if (saOnAnyLegacy > 0)
            blocking.Add($"{saOnAnyLegacy} SubjectAllocation reference(s) on legacy NULL-group Semesters.");
        if (ttOnAnyLegacy > 0)
            blocking.Add($"{ttOnAnyLegacy} TimetableEntry reference(s) on legacy NULL-group Semesters.");
        if (subjOnAnyLegacy > 0)
            blocking.Add($"{subjOnAnyLegacy} Subject reference(s) on legacy NULL-group Semesters.");
        if (secOnAnyLegacy > 0)
            blocking.Add($"{secOnAnyLegacy} Section reference(s) on legacy NULL-group Semesters.");
        if (studentsOnAnyLegacy > 0)
            blocking.Add($"{studentsOnAnyLegacy} Student reference(s) on legacy NULL-group Semesters.");
        if (duplicates.Count > 0)
            blocking.Add($"{duplicates.Count} duplicate TenantId+GroupId+Number key(s).");
        if (studentViolations.Count > 0)
            blocking.Add($"{studentViolations.Count} Student Semester integrity violation(s).");
        if (!courseGroupOk)
            blocking.Add("Group.CourseId inconsistency detected.");
        blocking.Add("NULL-group wildcard dependencies still present in AcademicTree / filterSemestersForScope / UI (must deprecate before NOT NULL).");
        blocking.Add("Rollback strategy documented in Prompt 3D contract — operational backup/journal not yet executed.");

        // Write-path GroupId required is already true (P1-4 2A / 3B-A) — not a blocker.
        var hardening = new DatabaseHardeningPreconditionDto
        {
            ZeroNullGroupSemesters = inventory.Count == 0,
            AllLegacyHaveExplicitDisposition = inventory.All(i =>
                i.Disposition != LegacySemesterFinalizationDisposition.UnknownRequiresArchitectDecision),
            ZeroTeachingGroupOnLegacy = tgOnAnyLegacy == 0,
            ZeroAttendanceOnLegacyNull = attOnAnyLegacy == 0,
            ZeroSaOnLegacyNull = saOnAnyLegacy == 0,
            ZeroTtOnLegacyNull = ttOnAnyLegacy == 0,
            ZeroStudentOnLegacyNull = studentsOnAnyLegacy == 0,
            ZeroDuplicateGroupNumber = duplicates.Count == 0,
            CourseGroupOwnershipConsistent = courseGroupOk,
            StudentIntegrityClean = studentViolations.Count == 0,
            WritePathsRequireGroupId = true,
            WildcardDependenciesDeprecated = false,
            RollbackStrategyDocumented = true,
            BlockingReasons = blocking,
        };

        notes.Add($"Legacy NULL-group inventory={inventory.Count}; TG residuals={tgResiduals.Count}; duplicates={duplicates.Count}.");
        notes.Add($"Sem III downstream: Att={downstream.Attendance}, SA={downstream.SubjectAllocation}, TT={downstream.TimetableEntry}, TG={downstream.TeachingGroup}, Subj={downstream.Subject}, Sec={downstream.Section}.");

        return new LegacySemesterFinalizationAuditDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = tenantId,
            IsReadOnly = true,
            NoMutationsPerformed = true,
            Summary = new LegacySemesterFinalizationSummaryDto
            {
                LegacyNullGroupCount = inventory.Count,
                TeachingGroupResidualCount = tgResiduals.Count,
                DuplicateGroupNumberKeys = duplicates.Count,
                StudentIntegrityViolations = studentViolations.Count,
                AttendanceLegacyRefs = downstream.Attendance,
                SubjectAllocationLegacyRefs = downstream.SubjectAllocation,
                TimetableEntryLegacyRefs = downstream.TimetableEntry,
                NotNullReady = hardening.NotNullMayProceed,
                UniqueConstraintReady = hardening.UniqueMayProceed,
            },
            LegacySemesters = inventory,
            TeachingGroupResiduals = tgResiduals,
            DuplicateGroupSemesterNumbers = duplicates,
            NullWildcardDependencies = BuildWildcardCatalog(),
            StudentIntegrity = new StudentSemesterIntegritySummaryDto
            {
                StudentsChecked = studentsAll.Count,
                Violations = studentViolations.Count,
                SampleViolationMessages = studentViolations.Take(20).ToList(),
            },
            DownstreamLegacyReferences = downstream,
            HardeningPreconditions = hardening,
            Notes = notes,
        };
    }

    private async Task<IReadOnlyList<TeachingGroupResidualReferenceDto>> BuildTeachingGroupResidualsAsync(
        int tenantId,
        IReadOnlyList<int> legacyIds,
        CancellationToken ct)
    {
        if (legacyIds.Count == 0)
            return [];

        var tgs = await _db.SchedulingTeachingGroups.AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && legacyIds.Contains(t.SemesterId))
            .Select(t => new
            {
                t.Id,
                t.Code,
                t.Name,
                t.TenantId,
                t.CourseId,
                t.GroupId,
                t.SemesterId,
            })
            .ToListAsync(ct);

        var tgIds = tgs.Select(t => t.Id).ToList();
        var sectionCounts = tgIds.Count == 0
            ? new Dictionary<int, int>()
            : await _db.SchedulingTeachingGroupSections.AsNoTracking()
                .Where(s => s.TenantId == tenantId && tgIds.Contains(s.TeachingGroupId))
                .GroupBy(s => s.TeachingGroupId)
                .Select(g => new { TeachingGroupId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.TeachingGroupId, x => x.Count, ct);

        var ttByTg = tgIds.Count == 0
            ? new Dictionary<int, int>()
            : await _db.SchedulingTimetableEntries.AsNoTracking()
                .Where(e => e.TenantId == tenantId && !e.IsDeleted && e.TeachingGroupId != null && tgIds.Contains(e.TeachingGroupId.Value))
                .GroupBy(e => e.TeachingGroupId!.Value)
                .Select(g => new { TeachingGroupId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.TeachingGroupId, x => x.Count, ct);

        var semesters = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && legacyIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Number, s.CourseId })
            .ToDictionaryAsync(s => s.Id, ct);

        var result = new List<TeachingGroupResidualReferenceDto>();
        foreach (var t in tgs)
        {
            semesters.TryGetValue(t.SemesterId, out var legacySem);
            var number = legacySem?.Number ?? 0;

            var candidates = await _db.Semesters.AsNoTracking()
                .Where(s =>
                    s.TenantId == tenantId
                    && !s.IsDeleted
                    && s.CourseId == t.CourseId
                    && s.GroupId == t.GroupId
                    && s.Number == number)
                .Select(s => s.Id)
                .ToListAsync(ct);

            int? candidateId = candidates.Count == 1 ? candidates[0] : null;
            var deterministic = candidateId is not null;

            TeachingGroupResidualRecommendation rec;
            string evidence;
            if (!deterministic)
            {
                rec = TeachingGroupResidualRecommendation.Blocked;
                evidence = candidates.Count == 0
                    ? "No deterministic Group-specific target Semester for TG GroupId+Number."
                    : $"Multiple ({candidates.Count}) target Semesters; fail closed.";
            }
            else
            {
                // Deterministic target exists, but TG mutation is out of scope for 3D/3C —
                // safe only under a separately approved TG remediation prompt.
                rec = TeachingGroupResidualRecommendation.SafeForSeparateTgRemediation;
                evidence =
                    $"Deterministic candidate SemesterId={candidateId}; TG mutation NOT performed in Prompt 3D. Requires separate approved TG remediation prompt. TeachingGroupSection ownership and projector remain frozen.";
            }

            result.Add(new TeachingGroupResidualReferenceDto
            {
                TeachingGroupId = t.Id,
                Code = t.Code,
                Name = t.Name,
                TenantId = t.TenantId,
                CourseId = t.CourseId,
                GroupId = t.GroupId,
                LegacySemesterId = t.SemesterId,
                LegacySemesterNumber = number,
                TeachingGroupSectionCount = sectionCounts.GetValueOrDefault(t.Id),
                TimetableEntryCountUsingTg = ttByTg.GetValueOrDefault(t.Id),
                CandidateTargetSemesterId = candidateId,
                CandidateIsDeterministic = deterministic,
                Recommendation = rec,
                RecommendationCode = rec switch
                {
                    TeachingGroupResidualRecommendation.SafeForSeparateTgRemediation => "SAFE_FOR_SEPARATE_TG_REMEDIATION",
                    TeachingGroupResidualRecommendation.RequiresManualReview => "REQUIRES_MANUAL_REVIEW",
                    TeachingGroupResidualRecommendation.Blocked => "BLOCKED",
                    _ => rec.ToString().ToUpperInvariant(),
                },
                Evidence = evidence,
                NoMutationPerformed = true,
            });
        }

        return result;
    }

    private static IReadOnlyList<NullWildcardDependencyDto> BuildWildcardCatalog()
        =>
        [
            Dep("AcademicTreeService", "Abhyanvaya.Application/Academic/AcademicTreeService.cs",
                NullWildcardDependencyAction.SafeToDeprecate,
                "P1-4-3L/3I3: Group-specific only (s.GroupId == g.Id); NULL-group wildcard retired."),
            Dep("filterSemestersForScope", "abhyanvaya-ui/src/services/setupService.ts",
                NullWildcardDependencyAction.SafeToDeprecate,
                "P1-4-3L/3I3: Group-specific only; excludes NULL-group and IsHistoricalArchive."),
            Dep("academicCascade", "abhyanvaya-ui/src/utils/academicCascade.ts",
                NullWildcardDependencyAction.SafeToDeprecate,
                "Delegates to filterSemestersForScope (no NULL-group wildcard)."),
            Dep("SemestersPage", "abhyanvaya-ui/src/pages/setup/SemestersPage.tsx",
                NullWildcardDependencyAction.HistoricalReadOnly,
                "Displays Legacy / Historical chip for null GroupId; writes already require Group (historical list only)."),
            Dep("MasterController semesters/full", "Abhyanvaya.API/Controllers/MasterController.cs",
                NullWildcardDependencyAction.HistoricalReadOnly,
                "Exposes IsLegacyCourseWide = GroupId == null for historical readability only."),
            Dep("SemesterController GetAll", "Abhyanvaya.API/Controllers/SemesterController.cs",
                NullWildcardDependencyAction.HistoricalReadOnly,
                "Lists legacy NULL-group rows for transition readability; operational selectors exclude them."),
            Dep("SubjectAllocationPage", "abhyanvaya-ui/src/pages/setup/scheduling/SubjectAllocationPage.tsx",
                NullWildcardDependencyAction.SafeToDeprecate,
                "Null-group labeled as legacy historical — not for new allocations; scope via filterSemestersForScope."),
            Dep("SubjectsPage", "abhyanvaya-ui/src/pages/setup/SubjectsPage.tsx",
                NullWildcardDependencyAction.SafeToDeprecate,
                "P1-4-3I3: uses filterSemestersForScope; excludes NULL-group wildcards."),
            Dep("AttendanceMarking", "abhyanvaya-ui/src/pages/AttendanceMarking.tsx",
                NullWildcardDependencyAction.SafeToDeprecate,
                "Uses filterSemestersForScope (Group-specific only)."),
            Dep("ElectiveGroupsPage", "abhyanvaya-ui/src/pages/setup/ElectiveGroupsPage.tsx",
                NullWildcardDependencyAction.SafeToDeprecate,
                "P1-4-3I3: semester filter requires groupId != null && groupId === selection."),
            Dep("schedulingFormUtils", "abhyanvaya-ui/src/pages/setup/scheduling/schedulingFormUtils.ts",
                NullWildcardDependencyAction.SafeToDeprecate,
                "Group-specific only; no NULL-group wildcard; no silent course-wide fallback."),
            Dep("StudentsPage filter", "abhyanvaya-ui/src/pages/StudentsPage.tsx",
                NullWildcardDependencyAction.SafeToDeprecate,
                "Filter Group-specific only; write form rejects null-group Semester."),
            Dep("Student write-path", "StudentSemesterOwnershipRules / StudentController",
                NullWildcardDependencyAction.SafeToDeprecate,
                "Already rejects null-group Semester assignment."),
            Dep("Semester write-path", "SemesterGroupOwnershipRules / SemesterController",
                NullWildcardDependencyAction.SafeToDeprecate,
                "Already requires GroupId on create/update."),
        ];

    private static NullWildcardDependencyDto Dep(
        string path, string location, NullWildcardDependencyAction action, string notes)
        => new()
        {
            Path = path,
            Location = location,
            Action = action,
            ActionCode = action.ToString().ToUpperInvariant(),
            Notes = notes,
        };
}
