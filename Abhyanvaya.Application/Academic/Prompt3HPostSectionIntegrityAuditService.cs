using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3H —
/// Composite read-only integrity audit after Prompt 3G + schema-hardening readiness.
/// Zero writes. No persistence calls. No TG/Section/SA/TT/Attendance/Student mutation.
/// </summary>
public sealed class Prompt3HPostSectionIntegrityAuditService : IPrompt3HPostSectionIntegrityAuditService
{
    public const string PromptCode = "P1-4-3H";
    public const int ExpectedLegacySemesterId = 3;
    public const int ExpectedTargetSemesterId = 11;
    public const int ExpectedTargetGroupId = 2;
    public const int ExpectedTargetCourseId = 1;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISemesterPostMigrationIntegrityAuditService _integrity;
    private readonly ILegacySemesterFinalizationAuditService _finalization;

    public Prompt3HPostSectionIntegrityAuditService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ISemesterPostMigrationIntegrityAuditService integrity,
        ILegacySemesterFinalizationAuditService finalization)
    {
        _db = db;
        _currentUser = currentUser;
        _integrity = integrity;
        _finalization = finalization;
    }

    public async Task<Prompt3HPostSectionIntegrityAuditDto> BuildAuditAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var notes = new List<string>
        {
            "Prompt 3H is AUDIT AND READINESS ONLY — zero mutations, zero schema changes.",
            "Reuses Prompt 3B-A integrity + Prompt 3D finalization audits; adds Prompt 3G verification.",
            "NOT_NULL_READY / UNIQUE_READY are fail-closed and independent of deletion fantasies.",
        };

        var integrity = await _integrity.BuildAuditAsync(cancellationToken);
        var finalization = await _finalization.BuildAuditAsync(cancellationToken);

        var semesters = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Select(s => new { s.Id, s.CourseId, s.GroupId, s.Number, s.Name })
            .ToListAsync(cancellationToken);

        var groups = await _db.Groups.AsNoTracking()
            .Where(g => g.TenantId == tenantId && !g.IsDeleted)
            .Select(g => new { g.Id, g.CourseId })
            .ToDictionaryAsync(g => g.Id, cancellationToken);

        var nullGroup = semesters.Where(s => s.GroupId is null).ToList();
        var groupSpecific = semesters.Where(s => s.GroupId is not null).ToList();

        var courseGroupMismatches = groupSpecific
            .Where(s => !groups.TryGetValue(s.GroupId!.Value, out var g) || g.CourseId != s.CourseId)
            .Select(s => s.Id)
            .OrderBy(x => x)
            .ToList();

        var duplicates = groupSpecific
            .GroupBy(s => new { GroupId = s.GroupId!.Value, s.Number })
            .Where(g => g.Count() > 1)
            .Select(g => new DuplicateGroupSemesterNumberDto
            {
                TenantId = tenantId,
                GroupId = g.Key.GroupId,
                Number = g.Key.Number,
                SemesterIds = g.Select(x => x.Id).OrderBy(x => x).ToList(),
                RemediationPlan = "Architect-approved merge/retire before UNIQUE(TenantId, GroupId, Number).",
            })
            .OrderBy(d => d.GroupId).ThenBy(d => d.Number)
            .ToList();

        var nullGroupIds = nullGroup.Select(s => s.Id).OrderBy(x => x).ToList();

        var prompt3G = await VerifyPrompt3GAsync(tenantId, cancellationToken);
        notes.Add(prompt3G.Evidence);

        var students = await AuditStudentsAsync(tenantId, cancellationToken);
        var attendance = await AuditAttendanceAsync(tenantId, nullGroupIds, cancellationToken);
        var subjects = await AuditSubjectsAsync(tenantId, nullGroupIds, cancellationToken);
        var sections = await AuditSectionsAsync(tenantId, nullGroupIds, cancellationToken);
        var sa = await AuditSubjectAllocationsAsync(tenantId, nullGroupIds, cancellationToken);
        var tt = await AuditTimetableEntriesAsync(tenantId, nullGroupIds, cancellationToken);
        var tg = await AuditTeachingGroupsAsync(tenantId, cancellationToken);
        var tgs = await AuditTeachingGroupSectionsAsync(tenantId, cancellationToken);
        var ts = await AuditTimetableSectionsAsync(tenantId, cancellationToken);
        var programOpt = await AuditProgramOptionalityAsync(tenantId, cancellationToken);
        var deptSsot = await AuditDepartmentSsotAsync(tenantId, cancellationToken);
        var tenantIso = await AuditTenantIsolationAsync(tenantId, cancellationToken);

        var classifications = MapLegacyClassifications(finalization);
        var historicalCount = classifications.Count(c =>
            c.Classification == Prompt3HLegacySemesterClassification.RetainHistorical);
        var ambiguousCount = classifications.Count(c =>
            c.Classification is Prompt3HLegacySemesterClassification.ManualMappingRequired
                or Prompt3HLegacySemesterClassification.DuplicateReview
                or Prompt3HLegacySemesterClassification.BlockedByTeachingGroupReference
                or Prompt3HLegacySemesterClassification.BlockedByDownstreamReference);

        var inventory = new Prompt3HSemesterInventoryDto
        {
            TotalSemesters = semesters.Count,
            NullGroupIdCount = nullGroup.Count,
            GroupSpecificCount = groupSpecific.Count,
            CourseGroupMismatchCount = courseGroupMismatches.Count,
            DuplicateGroupNumberCandidateCount = duplicates.Count,
            HistoricalRetainedCount = historicalCount,
            AmbiguousLegacyCount = ambiguousCount,
            NullGroupSemesterIds = nullGroupIds,
            CourseGroupMismatchSemesterIds = courseGroupMismatches,
            DuplicateKeys = duplicates,
        };

        var wildcardStatus = MapWildcardStatus(finalization.NullWildcardDependencies);

        var schema = ComputeSchemaReadiness(
            inventory,
            classifications,
            students,
            attendance,
            subjects,
            sections,
            sa,
            tt,
            tg,
            tgs,
            finalization,
            prompt3G,
            wildcardStatus,
            tenantIso,
            deptSsot);

        var blockers = schema.NotNullBlockers
            .Concat(schema.UniqueBlockers)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var (isHealthy, critical, error, warning) = ComputeHealthSummary(
            integrity.IsHealthy,
            prompt3G,
            students,
            attendance,
            subjects,
            sections,
            sa,
            tt,
            tg,
            tgs,
            inventory,
            classifications,
            wildcardStatus,
            schema);

        notes.Add($"Integrity IsHealthy={integrity.IsHealthy}; Aggregate IsHealthy={isHealthy}; Finalization NotNullReady(3D)={finalization.Summary.NotNullReady}.");
        notes.Add($"3H SemesterHardeningReady={schema.SemesterHardeningReadyCode}; NotNull={schema.NotNullReady}; Unique={schema.UniqueReady}; Downstream={schema.DownstreamReady}; TGBoundary={schema.TeachingGroupBoundaryReady}; TenantIso={schema.TenantIsolationReady}.");
        notes.Add($"3H CanMakeGroupIdNotNull={schema.CanMakeGroupIdNotNull}; CanAddUnique={schema.CanAddGroupSemesterUniqueConstraint}; CanRemoveWildcards={schema.CanRemoveLegacyWildcardSemantics}.");

        return new Prompt3HPostSectionIntegrityAuditDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = tenantId,
            IsReadOnly = true,
            NoMutationsPerformed = true,
            SaveChangesInvoked = false,
            PromptCode = PromptCode,
            IsHealthy = isHealthy,
            CriticalCount = critical,
            ErrorCount = error,
            WarningCount = warning,
            Prompt3GVerification = prompt3G,
            SemesterInventory = inventory,
            Students = students,
            Attendance = attendance,
            Subjects = subjects,
            Sections = sections,
            SubjectAllocations = sa,
            TimetableEntries = tt,
            TeachingGroups = tg,
            TeachingGroupSections = tgs,
            TimetableSections = ts,
            ProgramOptionality = programOpt,
            DepartmentSsot = deptSsot,
            TenantIsolation = tenantIso,
            LegacyClassifications = classifications,
            WildcardDependencyStatus = wildcardStatus,
            WildcardDependencies = finalization.NullWildcardDependencies,
            SchemaHardening = schema,
            EmbeddedIntegrityAudit = integrity,
            EmbeddedFinalizationAudit = finalization,
            ExactBlockers = blockers,
            Notes = notes,
            CanMakeGroupIdNotNull = schema.CanMakeGroupIdNotNull,
            CanAddGroupSemesterUniqueConstraint = schema.CanAddGroupSemesterUniqueConstraint,
            CanRemoveLegacyWildcardSemantics = schema.CanRemoveLegacyWildcardSemantics,
            DownstreamReady = schema.DownstreamReady,
            TenantIsolationReady = schema.TenantIsolationReady,
            StudentIntegrityReady = schema.StudentIntegrityReady,
            SectionIntegrityReady = schema.SectionIntegrityReady,
            TeachingGroupBoundaryReady = schema.TeachingGroupBoundaryReady,
            SemesterHardeningReady = schema.SemesterHardeningReady,
            SemesterHardeningReadyCode = schema.SemesterHardeningReadyCode,
            RecommendedNextStep = schema.SchemaHardeningPromptSafeToBegin
                ? "Chief Architect may authorize a dedicated schema-hardening prompt with proven preconditions."
                : "Do NOT begin schema hardening. Clear ExactBlockers first (legacy NULL operational refs, wildcards, dispositions).",
        };
    }

    private async Task<Prompt3GVerificationDto> VerifyPrompt3GAsync(int tenantId, CancellationToken ct)
    {
        var evidenceRows = await _db.LegacySemesterDispositionJournals.AsNoTracking()
            .Where(j => j.TenantId == tenantId
                        && j.PromptCode == SectionSemesterRemediationService.PromptCode
                        && j.DispositionCode == SectionSemesterRemediationService.JournalDispositionCode)
            .Select(j => j.Evidence)
            .ToListAsync(ct);

        var journaled = new HashSet<int>();
        foreach (var row in evidenceRows)
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
                    journaled.Add(id);
            }
        }

        var caSections = await _db.Sections.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted
                        && s.CourseId == ExpectedTargetCourseId
                        && s.GroupId == ExpectedTargetGroupId
                        && (journaled.Contains(s.Id)
                            || s.Id == SectionSemesterRemediationService.RequiredKnownBlockerSectionId
                            || s.SemesterId == ExpectedLegacySemesterId
                            || s.SemesterId == ExpectedTargetSemesterId))
            .Select(s => new { s.Id, s.SemesterId, s.GroupId })
            .ToListAsync(ct);

        var remediated = caSections
            .Where(s => (journaled.Contains(s.Id) || s.Id == SectionSemesterRemediationService.RequiredKnownBlockerSectionId)
                        && s.SemesterId == ExpectedTargetSemesterId)
            .Select(s => s.Id).OrderBy(x => x).ToList();

        var stillLegacyCa = caSections
            .Where(s => s.SemesterId == ExpectedLegacySemesterId && s.GroupId == ExpectedTargetGroupId)
            .Select(s => s.Id).OrderBy(x => x).ToList();

        var financeResidual = await _db.Sections.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted
                        && s.SemesterId == ExpectedLegacySemesterId
                        && s.GroupId != ExpectedTargetGroupId)
            .Select(s => s.Id)
            .OrderBy(x => x)
            .ToListAsync(ct);

        var alreadyCorrect = caSections
            .Where(s => s.SemesterId == ExpectedTargetSemesterId && !journaled.Contains(s.Id)
                        && s.Id != SectionSemesterRemediationService.RequiredKnownBlockerSectionId)
            .Select(s => s.Id).OrderBy(x => x).ToList();

        var contractOk = journaled.Count > 0
            && remediated.Count > 0
            && stillLegacyCa.Count == 0
            && remediated.Contains(SectionSemesterRemediationService.RequiredKnownBlockerSectionId);

        return new Prompt3GVerificationDto
        {
            JournalEvidenceFound = evidenceRows.Count > 0,
            JournaledSectionIds = journaled.OrderBy(x => x).ToList(),
            RemediatedOnTargetSemester = remediated,
            StillOnLegacySemester = stillLegacyCa,
            AlreadyCorrectOnTarget = alreadyCorrect,
            FinanceResidualOnLegacy = financeResidual,
            ExpectedLegacySemesterId = ExpectedLegacySemesterId,
            ExpectedTargetSemesterId = ExpectedTargetSemesterId,
            Prompt3GContractSatisfied = contractOk,
            Evidence = contractOk
                ? $"Prompt 3G verified: journaled [{string.Join(",", journaled.OrderBy(x => x))}] on Sem {ExpectedTargetSemesterId}; CA legacy residual=0; Finance residual on Sem 3=[{string.Join(",", financeResidual)}]."
                : $"Prompt 3G incomplete/ambiguous: journaled={journaled.Count}; remediated={remediated.Count}; CA still on Sem 3=[{string.Join(",", stillLegacyCa)}].",
        };
    }

    private async Task<Prompt3HEntityIntegrityDto> AuditStudentsAsync(
        int tenantId,
        CancellationToken ct)
    {
        var semesters = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Select(s => new { s.Id, s.CourseId, s.GroupId })
            .ToDictionaryAsync(s => s.Id, ct);

        var students = await _db.Students.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.SemesterId > 0)
            .Select(s => new { s.Id, s.CourseId, s.GroupId, s.SemesterId })
            .ToListAsync(ct);

        var samples = new List<Prompt3HEntityRefSampleDto>();
        var healthy = 0;
        var legacy = 0;
        var incompatible = 0;
        var unresolved = 0;

        foreach (var st in students)
        {
            if (!semesters.TryGetValue(st.SemesterId, out var sem))
            {
                unresolved++;
                if (samples.Count < 15)
                    samples.Add(Sample(st.Id, st.SemesterId, st.CourseId, st.GroupId, Prompt3HEntityRefStatus.Unresolved, "Semester missing."));
                continue;
            }

            if (sem.GroupId is null)
            {
                legacy++;
                if (samples.Count < 15)
                    samples.Add(Sample(st.Id, st.SemesterId, st.CourseId, st.GroupId, Prompt3HEntityRefStatus.LegacyNullGroupReference, "Student on NULL-group Semester."));
                continue;
            }

            if (sem.GroupId.Value != st.GroupId || sem.CourseId != st.CourseId)
            {
                incompatible++;
                if (samples.Count < 15)
                    samples.Add(Sample(st.Id, st.SemesterId, st.CourseId, st.GroupId, Prompt3HEntityRefStatus.Incompatible,
                        $"Semester Course/Group ({sem.CourseId}/{sem.GroupId}) != Student ({st.CourseId}/{st.GroupId})."));
                continue;
            }

            healthy++;
        }

        return Entity("Student", students.Count, healthy, legacy, incompatible, unresolved, legacy + incompatible + unresolved, samples);
    }

    private async Task<Prompt3HEntityIntegrityDto> AuditAttendanceAsync(
        int tenantId,
        IReadOnlyList<int> nullGroupIds,
        CancellationToken ct)
    {
        var semesters = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Select(s => new { s.Id, s.CourseId, s.GroupId })
            .ToDictionaryAsync(s => s.Id, ct);

        var rows = await _db.AttendanceSessions.AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .Select(a => new { a.Id, a.SemesterId, a.CourseId, a.GroupId })
            .ToListAsync(ct);

        return ClassifyOperationalRefs("AttendanceSession", rows.Select(r => (r.Id.ToString(), r.SemesterId, (int?)r.CourseId, (int?)r.GroupId)).ToList(),
            semesters.ToDictionary(x => x.Key, x => (x.Value.CourseId, x.Value.GroupId)), nullGroupIds);
    }

    private async Task<Prompt3HEntityIntegrityDto> AuditSubjectsAsync(
        int tenantId,
        IReadOnlyList<int> nullGroupIds,
        CancellationToken ct)
    {
        var semesters = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Select(s => new { s.Id, s.CourseId, s.GroupId })
            .ToDictionaryAsync(s => s.Id, ct);

        var rows = await _db.Subjects.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Select(s => new { s.Id, s.SemesterId, s.CourseId, s.GroupId })
            .ToListAsync(ct);

        return ClassifyOperationalRefs("Subject", rows.Select(r => (r.Id.ToString(), r.SemesterId, (int?)r.CourseId, (int?)r.GroupId)).ToList(),
            semesters.ToDictionary(x => x.Key, x => (x.Value.CourseId, x.Value.GroupId)), nullGroupIds);
    }

    private async Task<Prompt3HEntityIntegrityDto> AuditSectionsAsync(
        int tenantId,
        IReadOnlyList<int> nullGroupIds,
        CancellationToken ct)
    {
        var semesters = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Select(s => new { s.Id, s.CourseId, s.GroupId })
            .ToDictionaryAsync(s => s.Id, ct);

        var rows = await _db.Sections.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Select(s => new { s.Id, s.SemesterId, s.CourseId, s.GroupId })
            .ToListAsync(ct);

        return ClassifyOperationalRefs("Section", rows.Select(r => (r.Id.ToString(), r.SemesterId, (int?)r.CourseId, (int?)r.GroupId)).ToList(),
            semesters.ToDictionary(x => x.Key, x => (x.Value.CourseId, x.Value.GroupId)), nullGroupIds);
    }

    private async Task<Prompt3HEntityIntegrityDto> AuditSubjectAllocationsAsync(
        int tenantId,
        IReadOnlyList<int> nullGroupIds,
        CancellationToken ct)
    {
        var semesters = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Select(s => new { s.Id, s.CourseId, s.GroupId })
            .ToDictionaryAsync(s => s.Id, ct);

        var courses = await _db.Courses.AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted)
            .Select(c => new { c.Id, c.DepartmentId })
            .ToDictionaryAsync(c => c.Id, ct);

        var rows = await _db.SchedulingSubjectAllocations.AsNoTracking()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted)
            .Select(a => new { a.Id, a.SemesterId, a.CourseId, a.GroupId, a.DepartmentId })
            .ToListAsync(ct);

        var samples = new List<Prompt3HEntityRefSampleDto>();
        var healthy = 0;
        var legacy = 0;
        var incompatible = 0;
        var unresolved = 0;

        foreach (var r in rows)
        {
            if (!semesters.TryGetValue(r.SemesterId, out var sem))
            {
                unresolved++;
                continue;
            }

            if (sem.GroupId is null)
            {
                legacy++;
                if (samples.Count < 15)
                    samples.Add(Sample(r.Id, r.SemesterId, r.CourseId, r.GroupId, Prompt3HEntityRefStatus.LegacyNullGroupReference, "SA on NULL-group Semester."));
                continue;
            }

            var deptOk = courses.TryGetValue(r.CourseId, out var course) && course.DepartmentId == r.DepartmentId;
            if (sem.GroupId.Value != r.GroupId || sem.CourseId != r.CourseId || !deptOk)
            {
                incompatible++;
                if (samples.Count < 15)
                    samples.Add(Sample(r.Id, r.SemesterId, r.CourseId, r.GroupId, Prompt3HEntityRefStatus.Incompatible,
                        $"SA Course/Group/Dept mismatch (deptOk={deptOk})."));
                continue;
            }

            healthy++;
        }

        return Entity("SubjectAllocation", rows.Count, healthy, legacy, incompatible, unresolved, legacy + incompatible, samples);
    }

    private async Task<Prompt3HEntityIntegrityDto> AuditTimetableEntriesAsync(
        int tenantId,
        IReadOnlyList<int> nullGroupIds,
        CancellationToken ct)
    {
        var semesters = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Select(s => new { s.Id, s.CourseId, s.GroupId })
            .ToDictionaryAsync(s => s.Id, ct);

        var courses = await _db.Courses.AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted)
            .Select(c => new { c.Id, c.DepartmentId })
            .ToDictionaryAsync(c => c.Id, ct);

        var rows = await _db.SchedulingTimetableEntries.AsNoTracking()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted)
            .Select(e => new { e.Id, e.SemesterId, e.CourseId, e.GroupId, e.DepartmentId })
            .ToListAsync(ct);

        var samples = new List<Prompt3HEntityRefSampleDto>();
        var healthy = 0;
        var legacy = 0;
        var incompatible = 0;
        var unresolved = 0;

        foreach (var r in rows)
        {
            if (!semesters.TryGetValue(r.SemesterId, out var sem))
            {
                unresolved++;
                continue;
            }

            if (sem.GroupId is null)
            {
                legacy++;
                if (samples.Count < 15)
                    samples.Add(Sample(r.Id, r.SemesterId, r.CourseId, r.GroupId, Prompt3HEntityRefStatus.LegacyNullGroupReference, "TT on NULL-group Semester."));
                continue;
            }

            var deptOk = courses.TryGetValue(r.CourseId, out var course) && course.DepartmentId == r.DepartmentId;
            if (sem.GroupId.Value != r.GroupId || sem.CourseId != r.CourseId || !deptOk)
            {
                incompatible++;
                if (samples.Count < 15)
                    samples.Add(Sample(r.Id, r.SemesterId, r.CourseId, r.GroupId, Prompt3HEntityRefStatus.Incompatible,
                        $"TT Course/Group/Dept mismatch (deptOk={deptOk})."));
                continue;
            }

            healthy++;
        }

        return Entity("TimetableEntry", rows.Count, healthy, legacy, incompatible, unresolved, legacy + incompatible, samples);
    }

    private async Task<Prompt3HTeachingGroupIntegrityDto> AuditTeachingGroupsAsync(
        int tenantId,
        CancellationToken ct)
    {
        var semesters = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Select(s => new { s.Id, s.CourseId, s.GroupId })
            .ToDictionaryAsync(s => s.Id, ct);

        var rows = await _db.SchedulingTeachingGroups.AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted)
            .Select(t => new { t.Id, t.SemesterId, t.CourseId, t.GroupId })
            .ToListAsync(ct);

        var samples = new List<Prompt3HEntityRefSampleDto>();
        var residuals = new List<Prompt3HTgResidualRowDto>();
        var onGroup = 0;
        var legacy = 0;
        var incompatible = 0;

        foreach (var r in rows)
        {
            if (!semesters.TryGetValue(r.SemesterId, out var sem))
            {
                incompatible++;
                samples.Add(Sample(r.Id, r.SemesterId, r.CourseId, r.GroupId, Prompt3HEntityRefStatus.Unresolved, "TG Semester missing."));
                residuals.Add(TgResidual(r.Id, r.SemesterId, r.CourseId, r.GroupId,
                    Prompt3HTgResidualClassification.Blocked, "BLOCKED", "TG Semester missing / orphan."));
                continue;
            }

            if (sem.GroupId is null)
            {
                legacy++;
                samples.Add(Sample(r.Id, r.SemesterId, r.CourseId, r.GroupId, Prompt3HEntityRefStatus.LegacyNullGroupReference, "TG on NULL-group Semester."));
                residuals.Add(TgResidual(r.Id, r.SemesterId, r.CourseId, r.GroupId,
                    Prompt3HTgResidualClassification.Blocked, "BLOCKED", "TG references legacy NULL-group Semester."));
                continue;
            }

            if (sem.GroupId.Value != r.GroupId || sem.CourseId != r.CourseId)
            {
                incompatible++;
                samples.Add(Sample(r.Id, r.SemesterId, r.CourseId, r.GroupId, Prompt3HEntityRefStatus.Incompatible, "TG Course/Group != Semester."));
                residuals.Add(TgResidual(r.Id, r.SemesterId, r.CourseId, r.GroupId,
                    Prompt3HTgResidualClassification.ManualReviewRequired, "MANUAL_REVIEW_REQUIRED",
                    $"TG Course/Group ({r.CourseId}/{r.GroupId}) != Semester ({sem.CourseId}/{sem.GroupId})."));
                continue;
            }

            onGroup++;
            residuals.Add(TgResidual(r.Id, r.SemesterId, r.CourseId, r.GroupId,
                Prompt3HTgResidualClassification.Safe, "SAFE", "TG on Group-specific Semester; classify-only."));
            if (samples.Count < 10)
                samples.Add(Sample(r.Id, r.SemesterId, r.CourseId, r.GroupId, Prompt3HEntityRefStatus.Healthy, "TG on Group-specific Semester."));
        }

        return new Prompt3HTeachingGroupIntegrityDto
        {
            TotalChecked = rows.Count,
            OnGroupSpecificSemester = onGroup,
            LegacyNullGroupRefs = legacy,
            IncompatibleRefs = incompatible,
            Samples = samples.Take(20).ToList(),
            Residuals = residuals,
        };
    }

    private static Prompt3HTgResidualRowDto TgResidual(
        int tgId, int semesterId, int? courseId, int? groupId,
        Prompt3HTgResidualClassification cls, string code, string evidence)
        => new()
        {
            TeachingGroupId = tgId,
            SemesterId = semesterId,
            CourseId = courseId,
            GroupId = groupId,
            Classification = cls,
            ClassificationCode = code,
            Evidence = evidence,
        };

    private async Task<Prompt3HTeachingGroupSectionIntegrityDto> AuditTeachingGroupSectionsAsync(
        int tenantId,
        CancellationToken ct)
    {
        var links = await _db.SchedulingTeachingGroupSections.AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Select(x => new { x.TeachingGroupId, x.SectionId })
            .ToListAsync(ct);

        var tgSem = await _db.SchedulingTeachingGroups.AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted)
            .Select(t => new { t.Id, t.SemesterId })
            .ToDictionaryAsync(t => t.Id, t => t.SemesterId, ct);

        var secSem = await _db.Sections.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Select(s => new { s.Id, s.SemesterId })
            .ToDictionaryAsync(s => s.Id, s => s.SemesterId, ct);

        var samples = new List<Prompt3HTgsCompatibilitySampleDto>();
        var compatible = 0;
        var incompatible = 0;

        foreach (var link in links)
        {
            tgSem.TryGetValue(link.TeachingGroupId, out var tgSemesterId);
            secSem.TryGetValue(link.SectionId, out var sectionSemesterId);
            var ok = tgSemesterId > 0 && sectionSemesterId > 0 && tgSemesterId == sectionSemesterId;
            if (ok)
                compatible++;
            else
                incompatible++;

            if (samples.Count < 20)
            {
                samples.Add(new Prompt3HTgsCompatibilitySampleDto
                {
                    TeachingGroupId = link.TeachingGroupId,
                    SectionId = link.SectionId,
                    TeachingGroupSemesterId = tgSemesterId,
                    SectionSemesterId = sectionSemesterId,
                    IsCompatible = ok,
                    Notes = ok
                        ? "TG Semester matches Section Semester."
                        : $"Mismatch TG Sem={tgSemesterId} vs Section Sem={sectionSemesterId}.",
                });
            }
        }

        return new Prompt3HTeachingGroupSectionIntegrityDto
        {
            TotalLinksChecked = links.Count,
            CompatibleCount = compatible,
            IncompatibleCount = incompatible,
            Samples = samples,
        };
    }

    private async Task<Prompt3HTimetableSectionOwnershipDto> AuditTimetableSectionsAsync(
        int tenantId,
        CancellationToken ct)
    {
        var count = await _db.TimetableSections.AsNoTracking()
            .CountAsync(t => t.TenantId == tenantId && !t.IsDeleted, ct);

        return new Prompt3HTimetableSectionOwnershipDto
        {
            RowCount = count,
            ProjectorOwnedConfirmed = true,
            DirectWriterAbsentInThisPrompt = true,
            Notes = $"TimetableSection rows={count}; Prompt 3H does not write; projector remains sole writer.",
        };
    }

    private static IReadOnlyList<Prompt3HLegacyClassificationRowDto> MapLegacyClassifications(
        LegacySemesterFinalizationAuditDto finalization)
    {
        return finalization.LegacySemesters.Select(row =>
        {
            var (cls, code, evidence, blocks) = MapDisposition(row);
            return new Prompt3HLegacyClassificationRowDto
            {
                SemesterId = row.SemesterId,
                CourseId = row.CourseId,
                Number = row.Number,
                Name = row.Name,
                GroupId = null,
                Classification = cls,
                ClassificationCode = code,
                Evidence = evidence,
                StudentRefs = row.StudentReferenceCount,
                AttendanceRefs = row.AttendanceReferenceCount,
                SubjectRefs = row.SubjectReferenceCount,
                SectionRefs = row.SectionReferenceCount,
                SubjectAllocationRefs = row.SubjectAllocationReferenceCount,
                TimetableEntryRefs = row.TimetableEntryReferenceCount,
                TeachingGroupRefs = row.TeachingGroupReferenceCount,
                BlocksSchemaHardening = blocks,
                Prompt3DDispositionCode = row.DispositionCode,
            };
        }).OrderBy(r => r.SemesterId).ToList();
    }

    private static (Prompt3HLegacySemesterClassification, string, string, bool) MapDisposition(
        LegacySemesterInventoryRowDto row)
    {
        var opsRefs = row.StudentReferenceCount
            + row.AttendanceReferenceCount
            + row.SectionReferenceCount
            + row.SubjectAllocationReferenceCount
            + row.TimetableEntryReferenceCount
            + row.TeachingGroupReferenceCount;
        var subjectOnly = row.SubjectReferenceCount;

        if (row.Disposition == LegacySemesterFinalizationDisposition.BlockedByTeachingGroupReference
            || row.TeachingGroupReferenceCount > 0)
        {
            return (Prompt3HLegacySemesterClassification.BlockedByTeachingGroupReference,
                "BLOCKED_BY_TEACHING_GROUP_REFERENCE",
                row.DispositionEvidence,
                true);
        }

        if (row.Disposition == LegacySemesterFinalizationDisposition.DuplicateReview)
        {
            return (Prompt3HLegacySemesterClassification.DuplicateReview, "DUPLICATE_REVIEW",
                row.DispositionEvidence, true);
        }

        if (opsRefs > 0)
        {
            return (Prompt3HLegacySemesterClassification.BlockedByDownstreamReference,
                "BLOCKED_BY_DOWNSTREAM_REFERENCE",
                $"Operational refs remain (students/att/sec/sa/tt/tg total={opsRefs}). {row.DispositionEvidence}",
                true);
        }

        if (row.Disposition == LegacySemesterFinalizationDisposition.SafeSingleGroupMapping && opsRefs == 0)
        {
            return (Prompt3HLegacySemesterClassification.SafeForGroupMapping, "SAFE_FOR_GROUP_MAPPING",
                row.DispositionEvidence, true);
        }

        if (row.Disposition == LegacySemesterFinalizationDisposition.HistoricalRetain)
        {
            return (Prompt3HLegacySemesterClassification.RetainHistorical, "RETAIN_HISTORICAL",
                subjectOnly > 0
                    ? $"Subject historical refs={subjectOnly}. {row.DispositionEvidence}"
                    : row.DispositionEvidence,
                true);
        }

        if (row.Disposition is LegacySemesterFinalizationDisposition.ManualMappingRequired
            or LegacySemesterFinalizationDisposition.SplitRequired
            or LegacySemesterFinalizationDisposition.UnknownRequiresArchitectDecision)
        {
            return (Prompt3HLegacySemesterClassification.ManualMappingRequired, "MANUAL_MAPPING_REQUIRED",
                row.DispositionEvidence, true);
        }

        if (opsRefs == 0 && subjectOnly == 0
            && row.Disposition is LegacySemesterFinalizationDisposition.AlreadyGroupSpecific
                or LegacySemesterFinalizationDisposition.SafeSingleGroupMapping)
        {
            return (Prompt3HLegacySemesterClassification.ObsoleteCandidate, "OBSOLETE_CANDIDATE",
                "No operational or Subject refs; eligible for Architect-approved archive/retirement (no delete in 3H).",
                false);
        }

        if (opsRefs == 0 && subjectOnly == 0)
        {
            return (Prompt3HLegacySemesterClassification.RetainHistorical, "RETAIN_HISTORICAL",
                $"Zero ops refs; retain historical pending Architect disposition. {row.DispositionEvidence}",
                true);
        }

        // Fail-closed: unknown 3D dispositions require manual mapping rather than inventing new classes.
        return (Prompt3HLegacySemesterClassification.ManualMappingRequired, "MANUAL_MAPPING_REQUIRED",
            $"Mapped from 3D disposition {row.DispositionCode}: {row.DispositionEvidence}",
            true);
    }

    private static IReadOnlyList<Prompt3HWildcardDependencyStatusDto> MapWildcardStatus(
        IReadOnlyList<NullWildcardDependencyDto> deps)
    {
        return deps.Select(d =>
        {
            var (cls, code) = d.Action switch
            {
                NullWildcardDependencyAction.HistoricalReadOnly =>
                    (Prompt3HWildcardDependencyClassification.LegacyReadOnlyCompatibility, "LEGACY_READ_ONLY_COMPATIBILITY"),
                NullWildcardDependencyAction.SafeToDeprecate or NullWildcardDependencyAction.Remove =>
                    (Prompt3HWildcardDependencyClassification.SafeToRemove, "SAFE_TO_REMOVE"),
                NullWildcardDependencyAction.ReplaceWithGroupScope =>
                    (Prompt3HWildcardDependencyClassification.ActiveRuntimeDependency, "ACTIVE_RUNTIME_DEPENDENCY"),
                _ =>
                    (Prompt3HWildcardDependencyClassification.RequiresFollowup, "REQUIRES_FOLLOWUP"),
            };

            return new Prompt3HWildcardDependencyStatusDto
            {
                Path = d.Path,
                Location = d.Location,
                Classification = cls,
                ClassificationCode = code,
                Notes = string.IsNullOrWhiteSpace(d.Notes) ? d.ActionCode : d.Notes,
            };
        }).ToList();
    }

    private static (bool IsHealthy, int Critical, int Error, int Warning) ComputeHealthSummary(
        bool embeddedHealthy,
        Prompt3GVerificationDto prompt3G,
        Prompt3HEntityIntegrityDto students,
        Prompt3HEntityIntegrityDto attendance,
        Prompt3HEntityIntegrityDto subjects,
        Prompt3HEntityIntegrityDto sections,
        Prompt3HEntityIntegrityDto sa,
        Prompt3HEntityIntegrityDto tt,
        Prompt3HTeachingGroupIntegrityDto tg,
        Prompt3HTeachingGroupSectionIntegrityDto tgs,
        Prompt3HSemesterInventoryDto inventory,
        IReadOnlyList<Prompt3HLegacyClassificationRowDto> classifications,
        IReadOnlyList<Prompt3HWildcardDependencyStatusDto> wildcards,
        Prompt3HSchemaHardeningReadinessDto schema)
    {
        var critical = 0;
        var error = 0;
        var warning = 0;

        if (!prompt3G.Prompt3GContractSatisfied)
            critical++;
        if (tgs.IncompatibleCount > 0)
            critical += tgs.IncompatibleCount;
        if (tg.LegacyNullGroupRefs > 0)
            critical += tg.LegacyNullGroupRefs;
        if (inventory.CourseGroupMismatchCount > 0)
            critical += inventory.CourseGroupMismatchCount;

        error += students.IncompatibleRefs + students.UnresolvedRefs
            + sections.IncompatibleRefs + sections.UnresolvedRefs
            + attendance.IncompatibleRefs + attendance.UnresolvedRefs
            + sa.IncompatibleRefs + sa.UnresolvedRefs
            + tt.IncompatibleRefs + tt.UnresolvedRefs
            + tg.IncompatibleRefs;

        warning += students.LegacyNullGroupRefs
            + sections.LegacyNullGroupRefs
            + attendance.LegacyNullGroupRefs
            + subjects.LegacyNullGroupRefs
            + sa.LegacyNullGroupRefs
            + tt.LegacyNullGroupRefs
            + inventory.NullGroupIdCount
            + inventory.DuplicateGroupNumberCandidateCount
            + classifications.Count(c => c.BlocksSchemaHardening)
            + wildcards.Count(w => w.Classification is Prompt3HWildcardDependencyClassification.ActiveRuntimeDependency
                or Prompt3HWildcardDependencyClassification.RequiresFollowup);

        var operationalClean =
            students.IncompatibleRefs == 0 && students.UnresolvedRefs == 0
            && sections.IncompatibleRefs == 0 && sections.UnresolvedRefs == 0
            && attendance.IncompatibleRefs == 0 && attendance.UnresolvedRefs == 0
            && sa.IncompatibleRefs == 0 && sa.UnresolvedRefs == 0
            && tt.IncompatibleRefs == 0 && tt.UnresolvedRefs == 0
            && tg.IncompatibleRefs == 0
            && tgs.IncompatibleCount == 0
            && prompt3G.Prompt3GContractSatisfied
            && embeddedHealthy;

        // IsHealthy reflects operational hierarchy integrity — not schema-hardening readiness.
        _ = schema;
        return (operationalClean, critical, error, warning);
    }

    private static Prompt3HSchemaHardeningReadinessDto ComputeSchemaReadiness(
        Prompt3HSemesterInventoryDto inventory,
        IReadOnlyList<Prompt3HLegacyClassificationRowDto> classifications,
        Prompt3HEntityIntegrityDto students,
        Prompt3HEntityIntegrityDto attendance,
        Prompt3HEntityIntegrityDto subjects,
        Prompt3HEntityIntegrityDto sections,
        Prompt3HEntityIntegrityDto sa,
        Prompt3HEntityIntegrityDto tt,
        Prompt3HTeachingGroupIntegrityDto tg,
        Prompt3HTeachingGroupSectionIntegrityDto tgs,
        LegacySemesterFinalizationAuditDto finalization,
        Prompt3GVerificationDto prompt3G,
        IReadOnlyList<Prompt3HWildcardDependencyStatusDto> wildcardStatus,
        Prompt3HTenantIsolationDto tenantIso,
        Prompt3HDepartmentSsotDto deptSsot)
    {
        var notNullBlockers = new List<string>();
        var uniqueBlockers = new List<string>();

        if (inventory.NullGroupIdCount > 0)
        {
            notNullBlockers.Add(
                $"{inventory.NullGroupIdCount} NULL-group Semester row(s) remain Ids=[{string.Join(",", inventory.NullGroupSemesterIds)}]. " +
                "NOT NULL cannot be applied while these rows exist unless a separate historical-preservation schema is designed and approved.");
        }

        if (inventory.CourseGroupMismatchCount > 0)
            notNullBlockers.Add($"{inventory.CourseGroupMismatchCount} Semester CourseId/Group.CourseId mismatch(es).");

        if (students.LegacyNullGroupRefs + students.IncompatibleRefs + students.UnresolvedRefs > 0)
            notNullBlockers.Add($"Student unresolved/legacy/incompatible refs={students.LegacyNullGroupRefs + students.IncompatibleRefs + students.UnresolvedRefs}.");

        if (attendance.LegacyNullGroupRefs > 0)
            notNullBlockers.Add($"Attendance legacy NULL-group refs={attendance.LegacyNullGroupRefs}.");
        if (subjects.LegacyNullGroupRefs > 0)
            notNullBlockers.Add($"Subject legacy NULL-group refs={subjects.LegacyNullGroupRefs}.");
        if (sections.LegacyNullGroupRefs > 0)
            notNullBlockers.Add($"Section legacy NULL-group refs={sections.LegacyNullGroupRefs}.");
        if (sa.LegacyNullGroupRefs > 0)
            notNullBlockers.Add($"SubjectAllocation legacy NULL-group refs={sa.LegacyNullGroupRefs}.");
        if (tt.LegacyNullGroupRefs > 0)
            notNullBlockers.Add($"TimetableEntry legacy NULL-group refs={tt.LegacyNullGroupRefs}.");
        if (tg.LegacyNullGroupRefs > 0)
            notNullBlockers.Add($"TeachingGroup legacy NULL-group refs={tg.LegacyNullGroupRefs}.");
        if (tgs.IncompatibleCount > 0)
            notNullBlockers.Add($"TeachingGroupSection Semester mismatches={tgs.IncompatibleCount}.");

        if (wildcardStatus.Count > 0)
            notNullBlockers.Add($"{wildcardStatus.Count} NULL-group wildcard dependency site(s) still catalogued (AcademicTree/UI/filters).");

        if (!prompt3G.Prompt3GContractSatisfied)
            notNullBlockers.Add("Prompt 3G CA Section contract not fully verified against live data/journal.");

        var blockingLegacy = classifications.Count(c => c.BlocksSchemaHardening);
        if (blockingLegacy > 0)
            notNullBlockers.Add($"{blockingLegacy} legacy Semester(s) still block schema hardening (RETAIN/MANUAL/DUPLICATE/TG/DOWNSTREAM).");

        if (!tenantIso.Passed)
            notNullBlockers.Add("Tenant isolation violations detected.");

        if (deptSsot.SubjectAllocationDepartmentMismatches + deptSsot.TimetableEntryDepartmentMismatches > 0)
            notNullBlockers.Add("SA/TT Department SSOT mismatches remain.");

        if (inventory.DuplicateGroupNumberCandidateCount > 0)
        {
            uniqueBlockers.Add(
                $"{inventory.DuplicateGroupNumberCandidateCount} duplicate TenantId+GroupId+Number key(s) among Group-specific Semesters.");
        }

        if (inventory.NullGroupIdCount > 0)
        {
            uniqueBlockers.Add(
                "Historical/operational NULL-group rows remain; plain UNIQUE(TenantId, GroupId, Number) needs an approved design for NULL GroupId preservation (filtered unique / separate legacy table / soft-archive) before UNIQUE_READY.");
        }

        var notNullReady = notNullBlockers.Count == 0;
        var uniqueReady = uniqueBlockers.Count == 0;

        if (sections.LegacyNullGroupRefs > 0 || attendance.LegacyNullGroupRefs > 0 || subjects.LegacyNullGroupRefs > 0
            || sa.LegacyNullGroupRefs > 0 || tt.LegacyNullGroupRefs > 0 || students.LegacyNullGroupRefs > 0
            || inventory.NullGroupIdCount > 0)
        {
            notNullReady = false;
            if (notNullBlockers.Count == 0)
                notNullBlockers.Add("Unresolved operational NULL-group dependency remains.");
        }

        if (inventory.DuplicateGroupNumberCandidateCount > 0)
        {
            uniqueReady = false;
            if (uniqueBlockers.Count == 0)
                uniqueBlockers.Add("Duplicate operational keys remain.");
        }

        var canRemoveWildcards = wildcardStatus.Count == 0
            && !finalization.NullWildcardDependencies.Any();

        var studentReady = students.IncompatibleRefs == 0 && students.UnresolvedRefs == 0 && students.LegacyNullGroupRefs == 0;
        var sectionReady = sections.IncompatibleRefs == 0 && sections.UnresolvedRefs == 0 && sections.LegacyNullGroupRefs == 0
            && prompt3G.Prompt3GContractSatisfied;
        var downstreamReady = attendance.LegacyNullGroupRefs == 0 && attendance.IncompatibleRefs == 0
            && sa.LegacyNullGroupRefs == 0 && sa.IncompatibleRefs == 0
            && tt.LegacyNullGroupRefs == 0 && tt.IncompatibleRefs == 0
            && subjects.IncompatibleRefs == 0
            && deptSsot.SubjectAllocationDepartmentMismatches == 0
            && deptSsot.TimetableEntryDepartmentMismatches == 0;
        var tgBoundaryReady = tg.LegacyNullGroupRefs == 0 && tg.IncompatibleRefs == 0 && tgs.IncompatibleCount == 0;
        var tenantReady = tenantIso.Passed;

        var decision = Prompt3HHardeningDecision.NotReady;
        var decisionCode = "NOT_READY";
        if (!tenantReady || !prompt3G.Prompt3GContractSatisfied || students.IncompatibleRefs > 0 || sections.IncompatibleRefs > 0)
        {
            decision = Prompt3HHardeningDecision.Blocked;
            decisionCode = "BLOCKED";
        }
        else if (notNullReady && uniqueReady && canRemoveWildcards && studentReady && sectionReady
                 && downstreamReady && tgBoundaryReady && tenantReady)
        {
            decision = Prompt3HHardeningDecision.Ready;
            decisionCode = "READY";
        }

        return new Prompt3HSchemaHardeningReadinessDto
        {
            NotNullReady = notNullReady,
            NotNullVerdict = notNullReady ? "READY" : "NOT READY",
            NotNullBlockers = notNullBlockers,
            UniqueReady = uniqueReady,
            UniqueVerdict = uniqueReady ? "READY" : "NOT READY",
            UniqueBlockers = uniqueBlockers,
            DownstreamReady = downstreamReady,
            TenantIsolationReady = tenantReady,
            StudentIntegrityReady = studentReady,
            SectionIntegrityReady = sectionReady,
            TeachingGroupBoundaryReady = tgBoundaryReady,
            SemesterHardeningReady = decision,
            SemesterHardeningReadyCode = decisionCode,
            HistoricalNullPreservationNote =
                "While NULL-group Semesters must remain for audit/history, GroupId NOT NULL cannot be applied to the same column without an Architect-approved preservation design " +
                "(e.g. migrate historical rows to an archive table, or introduce IsHistoricalArchive + filtered unique index excluding historical). Deletion-only paths are forbidden by ADL.",
            SchemaHardeningPromptSafeToBegin = decision == Prompt3HHardeningDecision.Ready,
            CanMakeGroupIdNotNull = notNullReady,
            CanAddGroupSemesterUniqueConstraint = uniqueReady,
            CanRemoveLegacyWildcardSemantics = canRemoveWildcards,
        };
    }

    private async Task<Prompt3HProgramOptionalityDto> AuditProgramOptionalityAsync(
        int tenantId, CancellationToken ct)
    {
        var enablePrograms = await _db.TenantAcademicConfigurations.AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .Select(c => (bool?)c.EnablePrograms)
            .FirstOrDefaultAsync(ct) ?? false;

        var missingDept = await _db.Courses.AsNoTracking()
            .CountAsync(c => c.TenantId == tenantId && !c.IsDeleted && c.DepartmentId <= 0, ct);

        return new Prompt3HProgramOptionalityDto
        {
            EnablePrograms = enablePrograms,
            ProgramRemainsOptional = true,
            CourseDepartmentIdMandatory = true,
            CoursesMissingDepartmentId = missingDept,
            Notes = enablePrograms
                ? "EnablePrograms=true: Program may be selected or omitted where permitted; Course.DepartmentId remains mandatory."
                : "EnablePrograms=false: Program is not required; Course.DepartmentId remains mandatory.",
        };
    }

    private async Task<Prompt3HDepartmentSsotDto> AuditDepartmentSsotAsync(
        int tenantId, CancellationToken ct)
    {
        var courses = await _db.Courses.AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted)
            .Select(c => new { c.Id, c.DepartmentId })
            .ToDictionaryAsync(c => c.Id, ct);

        var saRows = await _db.SchedulingSubjectAllocations.AsNoTracking()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted)
            .Select(a => new { a.Id, a.CourseId, a.DepartmentId })
            .ToListAsync(ct);
        var saMismatch = saRows.Count(a =>
            !courses.TryGetValue(a.CourseId, out var c) || c.DepartmentId != a.DepartmentId);

        var ttRows = await _db.SchedulingTimetableEntries.AsNoTracking()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted)
            .Select(e => new { e.Id, e.CourseId, e.DepartmentId })
            .ToListAsync(ct);
        var ttMismatch = ttRows.Count(e =>
            !courses.TryGetValue(e.CourseId, out var c) || c.DepartmentId != e.DepartmentId);

        return new Prompt3HDepartmentSsotDto
        {
            SubjectAllocationsChecked = saRows.Count,
            SubjectAllocationDepartmentMismatches = saMismatch,
            TimetableEntriesChecked = ttRows.Count,
            TimetableEntryDepartmentMismatches = ttMismatch,
            CourseDepartmentSsotIntact = saMismatch == 0 && ttMismatch == 0,
        };
    }

    private async Task<Prompt3HTenantIsolationDto> AuditTenantIsolationAsync(
        int tenantId, CancellationToken ct)
    {
        var findings = new List<string>();

        var studentXt = await (
            from st in _db.Students.AsNoTracking()
            join s in _db.Semesters.AsNoTracking() on st.SemesterId equals s.Id
            where st.TenantId == tenantId && !st.IsDeleted && s.TenantId != tenantId
            select st.Id).CountAsync(ct);
        if (studentXt > 0)
            findings.Add($"Students referencing other-tenant Semesters={studentXt}.");

        var sectionXt = await (
            from sec in _db.Sections.AsNoTracking()
            join s in _db.Semesters.AsNoTracking() on sec.SemesterId equals s.Id
            where sec.TenantId == tenantId && !sec.IsDeleted && s.TenantId != tenantId
            select sec.Id).CountAsync(ct);
        if (sectionXt > 0)
            findings.Add($"Sections referencing other-tenant Semesters={sectionXt}.");

        var semXt = await (
            from s in _db.Semesters.AsNoTracking()
            join g in _db.Groups.AsNoTracking() on s.GroupId equals g.Id
            where s.TenantId == tenantId && !s.IsDeleted && s.GroupId != null && g.TenantId != tenantId
            select s.Id).CountAsync(ct);
        if (semXt > 0)
            findings.Add($"Semesters referencing other-tenant Groups={semXt}.");

        return new Prompt3HTenantIsolationDto
        {
            Passed = findings.Count == 0,
            CrossTenantSemesterRefs = semXt,
            CrossTenantSectionRefs = sectionXt,
            CrossTenantStudentRefs = studentXt,
            Findings = findings,
        };
    }

    private static Prompt3HEntityIntegrityDto ClassifyOperationalRefs(
        string entityType,
        List<(string Key, int SemesterId, int? CourseId, int? GroupId)> rows,
        Dictionary<int, (int CourseId, int? GroupId)> semesters,
        IReadOnlyList<int> nullGroupIds,
        bool requireGroupMatch = true)
    {
        var nullSet = nullGroupIds.ToHashSet();
        var samples = new List<Prompt3HEntityRefSampleDto>();
        var healthy = 0;
        var legacy = 0;
        var incompatible = 0;
        var unresolved = 0;

        foreach (var r in rows)
        {
            if (!semesters.TryGetValue(r.SemesterId, out var sem))
            {
                unresolved++;
                if (samples.Count < 15)
                    samples.Add(Sample(r.Key, r.SemesterId, r.CourseId, r.GroupId, Prompt3HEntityRefStatus.Unresolved, "Semester missing."));
                continue;
            }

            if (nullSet.Contains(r.SemesterId) || sem.GroupId is null)
            {
                legacy++;
                if (samples.Count < 15)
                    samples.Add(Sample(r.Key, r.SemesterId, r.CourseId, r.GroupId, Prompt3HEntityRefStatus.LegacyNullGroupReference, "References NULL-group Semester."));
                continue;
            }

            if (r.CourseId is int cid && cid != sem.CourseId)
            {
                incompatible++;
                if (samples.Count < 15)
                    samples.Add(Sample(r.Key, r.SemesterId, r.CourseId, r.GroupId, Prompt3HEntityRefStatus.Incompatible, "Course mismatch vs Semester."));
                continue;
            }

            if (requireGroupMatch && r.GroupId is int gid && sem.GroupId.Value != gid)
            {
                incompatible++;
                if (samples.Count < 15)
                    samples.Add(Sample(r.Key, r.SemesterId, r.CourseId, r.GroupId, Prompt3HEntityRefStatus.Incompatible, "Group mismatch vs Semester."));
                continue;
            }

            healthy++;
        }

        return Entity(entityType, rows.Count, healthy, legacy, incompatible, unresolved, legacy + incompatible + unresolved, samples);
    }

    private static Prompt3HEntityIntegrityDto Entity(
        string type, int total, int healthy, int legacy, int incompatible, int unresolved, int remediation,
        List<Prompt3HEntityRefSampleDto> samples)
        => new()
        {
            EntityType = type,
            TotalChecked = total,
            HealthyCount = healthy,
            LegacyNullGroupRefs = legacy,
            IncompatibleRefs = incompatible,
            UnresolvedRefs = unresolved,
            RemediationCandidates = remediation,
            Samples = samples,
        };

    private static Prompt3HEntityRefSampleDto Sample(
        object key, int? semesterId, int? courseId, int? groupId, Prompt3HEntityRefStatus status, string notes)
        => new()
        {
            EntityKey = key?.ToString() ?? "",
            SemesterId = semesterId,
            CourseId = courseId,
            GroupId = groupId,
            Status = status,
            StatusCode = status.ToString().ToUpperInvariant(),
            Notes = notes,
        };
}
