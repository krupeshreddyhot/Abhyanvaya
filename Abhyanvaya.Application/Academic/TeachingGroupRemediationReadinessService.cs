using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3H (TG readiness) / PromptCode P1-4-3H2 —
/// Post-Section-remediation integrity audit for controlled Prompt 3F re-execution eligibility.
/// Zero mutations. Does not call Prompt 3F Execute. Does not modify TeachingGroupSemesterRemediationService.
/// </summary>
public sealed class TeachingGroupRemediationReadinessService : ITeachingGroupRemediationReadinessService
{
    public const string PromptCode = "P1-4-3H2";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ITeachingGroupSemesterRemediationService _prompt3F;

    public TeachingGroupRemediationReadinessService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ITeachingGroupSemesterRemediationService prompt3F)
    {
        _db = db;
        _currentUser = currentUser;
        _prompt3F = prompt3F;
    }

    public async Task<TeachingGroupRemediationReadinessResultDto> BuildAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var notes = new List<string>
        {
            "Prompt 3H TG remediation readiness — AUDIT ONLY.",
            $"PromptCode={PromptCode}. Zero SaveChanges. Zero Prompt 3F Execute.",
            $"Approved TG set from Prompt 3F: [{string.Join(",", TeachingGroupSemesterRemediationService.ApprovedTeachingGroupIds)}].",
            "Does not mutate TeachingGroup / TeachingGroupSection / TimetableSection / Section / Semester.",
        };
        var findings = new List<TgRemediationFindingDto>();

        var legacyId = TeachingGroupSemesterRemediationService.ExpectedLegacySemesterId;
        var targetId = TeachingGroupSemesterRemediationService.ExpectedTargetSemesterId;
        var expectedNumber = TeachingGroupSemesterRemediationService.ExpectedSemesterNumber;
        var approved = TeachingGroupSemesterRemediationService.ApprovedTeachingGroupIds.ToList();

        // --- D. Target / legacy Semester ownership ---
        var legacy = await _db.Semesters.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.Id == legacyId, cancellationToken);
        var target = await _db.Semesters.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.Id == targetId, cancellationToken);

        var legacyValid = legacy is not null
                          && legacy.GroupId is null
                          && legacy.Number == expectedNumber
                          && legacy.TenantId == tenantId;
        if (legacy is null)
            findings.Add(Finding("LEGACY_SEMESTER_MISSING", TgRemediationFindingSeverity.Critical,
                "Semester", legacyId, null, targetId, null, null,
                "Legacy Semester Id=3 not found.", "BLOCKED"));
        else if (!legacyValid)
            findings.Add(Finding("LEGACY_SEMESTER_BASELINE", TgRemediationFindingSeverity.Critical,
                "Semester", legacyId, legacy.Id, targetId, legacy.GroupId, null,
                $"Legacy Sem 3 baseline mismatch GroupId={legacy.GroupId} Number={legacy.Number}.", "BLOCKED"));

        var targetValid = false;
        var courseGroupAligned = false;
        var noDup = false;
        int? targetGroupId = null;
        int? targetCourseId = null;
        var targetNotes = "";

        if (target is null)
        {
            findings.Add(Finding("TARGET_SEMESTER_MISSING", TgRemediationFindingSeverity.Critical,
                "Semester", targetId, null, targetId, null, null,
                "Target Semester Id=11 not found.", "BLOCKED"));
            targetNotes = "Target missing.";
        }
        else
        {
            targetGroupId = target.GroupId;
            targetCourseId = target.CourseId;
            if (target.GroupId is null)
            {
                findings.Add(Finding("TARGET_NULL_GROUP", TgRemediationFindingSeverity.Critical,
                    "Semester", target.Id, target.Id, targetId, null, null,
                    "Target Semester 11 has NULL GroupId.", "BLOCKED"));
            }
            else if (target.Number != expectedNumber)
            {
                findings.Add(Finding("TARGET_NUMBER_MISMATCH", TgRemediationFindingSeverity.Critical,
                    "Semester", target.Id, target.Id, targetId, target.GroupId, target.GroupId,
                    $"Target Number={target.Number} expected {expectedNumber}.", "BLOCKED"));
            }
            else
            {
                var group = await _db.Groups.AsNoTracking()
                    .FirstOrDefaultAsync(g => !g.IsDeleted && g.Id == target.GroupId.Value, cancellationToken);
                var course = await _db.Courses.AsNoTracking()
                    .FirstOrDefaultAsync(c => !c.IsDeleted && c.Id == target.CourseId, cancellationToken);

                if (group is null || course is null)
                {
                    findings.Add(Finding("TARGET_COURSE_GROUP_MISSING", TgRemediationFindingSeverity.Critical,
                        "Semester", target.Id, target.Id, targetId, target.GroupId, target.GroupId,
                        "Target Course or Group missing.", "BLOCKED"));
                }
                else if (group.TenantId != tenantId || course.TenantId != tenantId || target.TenantId != tenantId)
                {
                    findings.Add(Finding("CROSS_TENANT_TARGET", TgRemediationFindingSeverity.Critical,
                        "Semester", target.Id, target.Id, targetId, target.GroupId, target.GroupId,
                        "Target Semester/Group/Course cross-tenant.", "BLOCKED"));
                }
                else if (group.CourseId != target.CourseId)
                {
                    findings.Add(Finding("TARGET_COURSE_GROUP_MISMATCH", TgRemediationFindingSeverity.Critical,
                        "Semester", target.Id, target.Id, targetId, target.GroupId, target.GroupId,
                        $"Semester.CourseId={target.CourseId} != Group.CourseId={group.CourseId}.", "BLOCKED"));
                }
                else
                {
                    courseGroupAligned = true;
                    var dupCount = await _db.Semesters.AsNoTracking()
                        .CountAsync(s => s.TenantId == tenantId && !s.IsDeleted
                                         && s.GroupId == target.GroupId && s.Number == target.Number, cancellationToken);
                    noDup = dupCount == 1;
                    if (!noDup)
                        findings.Add(Finding("TARGET_DUPLICATE_GROUP_NUMBER", TgRemediationFindingSeverity.Critical,
                            "Semester", target.Id, target.Id, targetId, target.GroupId, target.GroupId,
                            $"Duplicate Group+Number Semesters count={dupCount}.", "BLOCKED"));
                    else
                    {
                        targetValid = true;
                        targetNotes = $"Target Sem={targetId} GroupId={target.GroupId} CourseId={target.CourseId} valid.";
                    }
                }
            }
        }

        var targetValidation = new TgRemediationTargetSemesterValidationDto
        {
            LegacySemesterId = legacyId,
            TargetSemesterId = targetId,
            LegacyValid = legacyValid,
            TargetValid = targetValid,
            TargetGroupId = targetGroupId,
            TargetCourseId = targetCourseId,
            CourseGroupAligned = courseGroupAligned,
            NoDuplicateGroupNumber = noDup,
            Notes = targetNotes,
        };

        // --- A. Section integrity (legacy Sem 3) ---
        var legacySections = await _db.Sections.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.SemesterId == legacyId)
            .OrderBy(s => s.Id)
            .ToListAsync(cancellationToken);

        var legacySectionRows = new List<TgRemediationSectionLegacyRowDto>();
        foreach (var s in legacySections)
        {
            var compatible = targetValid
                             && targetCourseId is int tc
                             && targetGroupId is int tg
                             && s.CourseId == tc
                             && s.GroupId == tg;
            var notesSec = compatible
                ? "On legacy Sem 3 but Course/Group match CA target — still blocks TG remap until Section remapped."
                : "On legacy Sem 3; Course/Group may be Finance or other — out of Prompt 3F CA TG scope unless mapped separately.";

            if (s.CourseId != (targetCourseId ?? -1) && targetValid)
                findings.Add(Finding("SECTION_LEGACY_COURSE", TgRemediationFindingSeverity.Warning,
                    "Section", s.Id, s.SemesterId, targetId, s.GroupId, targetGroupId,
                    $"Section CourseId={s.CourseId} vs target CourseId={targetCourseId}.", "MANUAL_REVIEW_REQUIRED"));

            findings.Add(Finding("SECTION_LEGACY_SEMESTER", TgRemediationFindingSeverity.Error,
                "Section", s.Id, s.SemesterId, targetId, s.GroupId, targetGroupId,
                $"Section still references legacy Sem {legacyId}.", "BLOCKED"));

            legacySectionRows.Add(new TgRemediationSectionLegacyRowDto
            {
                SectionId = s.Id,
                SectionCode = s.SectionCode,
                CourseId = s.CourseId,
                GroupId = s.GroupId,
                SemesterId = s.SemesterId,
                CompatibleWithCaTarget = compatible,
                Notes = notesSec,
            });
        }

        // --- C. Downstream regression (Attendance / SA / TT must not be on Sem 3) ---
        var studentLegacy = await _db.Students.AsNoTracking()
            .CountAsync(s => !s.IsDeleted && s.SemesterId == legacyId, cancellationToken);
        var attendanceLegacy = await _db.AttendanceSessions.AsNoTracking()
            .CountAsync(a => a.SemesterId == legacyId, cancellationToken);
        var saLegacy = await _db.SchedulingSubjectAllocations.AsNoTracking()
            .CountAsync(a => !a.IsDeleted && a.SemesterId == legacyId, cancellationToken);
        var ttLegacy = await _db.SchedulingTimetableEntries.AsNoTracking()
            .CountAsync(e => !e.IsDeleted && e.SemesterId == legacyId, cancellationToken);
        var subjectLegacy = await _db.Subjects.AsNoTracking()
            .CountAsync(s => !s.IsDeleted && s.SemesterId == legacyId, cancellationToken);

        var regression = attendanceLegacy > 0 || saLegacy > 0 || ttLegacy > 0;
        if (regression)
        {
            findings.Add(Finding("DOWNSTREAM_REGRESSION_ATT_SA_TT", TgRemediationFindingSeverity.Critical,
                "Downstream", null, legacyId, targetId, null, targetGroupId,
                $"Regressed legacy Sem-3 refs: Attendance={attendanceLegacy}, SA={saLegacy}, TT={ttLegacy}.",
                "BLOCKED"));
        }

        if (studentLegacy > 0)
            findings.Add(Finding("STUDENT_LEGACY_SEM3", TgRemediationFindingSeverity.Error,
                "Student", null, legacyId, targetId, null, targetGroupId,
                $"{studentLegacy} Student(s) still on legacy Sem 3.", "MANUAL_REVIEW_REQUIRED"));

        if (subjectLegacy > 0)
            findings.Add(Finding("SUBJECT_LEGACY_SEM3", TgRemediationFindingSeverity.Warning,
                "Subject", null, legacyId, targetId, null, targetGroupId,
                $"{subjectLegacy} Subject(s) on legacy Sem 3 (may be deferred historical).", "DEFERRED"));

        var tgLegacyCount = await _db.SchedulingTeachingGroups.AsNoTracking()
            .CountAsync(t => !t.IsDeleted && t.SemesterId == legacyId, cancellationToken);

        var downstream = new TgRemediationDownstreamRegressionDto
        {
            StudentLegacySem3Count = studentLegacy,
            AttendanceLegacySem3Count = attendanceLegacy,
            SubjectAllocationLegacySem3Count = saLegacy,
            TimetableEntryLegacySem3Count = ttLegacy,
            SubjectLegacySem3Count = subjectLegacy,
            AttendanceSaTtRegressionDetected = regression,
            Notes = regression
                ? "CRITICAL: Prompt 3C remediation appears regressed for Attendance/SA/TT."
                : "Attendance/SA/TT show no Sem-3 regression.",
        };

        // --- B. Teaching Group readiness via Prompt 3F read-only preview (no Execute) ---
        var preview = await _prompt3F.PreviewAsync(cancellationToken);
        notes.Add($"Prompt 3F Preview ExecutionStatus={preview.ExecutionStatus}; IsReadOnly={preview.IsReadOnly}.");

        if (!preview.IsReadOnly)
            findings.Add(Finding("PROMPT3F_PREVIEW_NOT_READONLY", TgRemediationFindingSeverity.Critical,
                "TeachingGroupRemediation", null, null, targetId, null, targetGroupId,
                "Prompt 3F Preview reported IsReadOnly=false; fail closed.", "BLOCKED"));

        var tgRows = new List<TgRemediationTeachingGroupRowDto>();
        var readyIds = new List<int>();
        var blockedIds = new List<int>();
        var alreadyIds = new List<int>();
        var manualIds = new List<int>();

        // Tenant isolation across approved TGs
        var tenantIsolationOk = true;
        foreach (var tgId in approved)
        {
            var tg = await _db.SchedulingTeachingGroups.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tgId && !t.IsDeleted, cancellationToken);
            if (tg is null)
            {
                blockedIds.Add(tgId);
                findings.Add(Finding("TG_MISSING", TgRemediationFindingSeverity.Critical,
                    "TeachingGroup", tgId, null, targetId, null, targetGroupId,
                    $"Approved TeachingGroup Id={tgId} not found.", "BLOCKED"));
                tgRows.Add(new TgRemediationTeachingGroupRowDto
                {
                    TeachingGroupId = tgId,
                    Readiness = TgRemediationReadinessStatus.Blocked,
                    ReadinessCode = "BLOCKED",
                    Reason = "TeachingGroup not found.",
                    TargetSemesterId = targetId,
                });
                continue;
            }

            if (tg.TenantId != tenantId)
            {
                tenantIsolationOk = false;
                findings.Add(Finding("CROSS_TENANT_TG", TgRemediationFindingSeverity.Critical,
                    "TeachingGroup", tg.Id, tg.SemesterId, targetId, tg.GroupId, targetGroupId,
                    "TeachingGroup tenant mismatch.", "BLOCKED"));
            }

            var item = preview.Items.FirstOrDefault(i => i.TeachingGroupId == tgId);
            var (status, code) = MapPreviewStatus(item, preview.ExecutionStatus);

            // Elevate: if any linked Section still on legacy Sem 3 → BLOCKED for 3F
            var tgsLinks = await _db.SchedulingTeachingGroupSections.AsNoTracking()
                .Where(x => !x.IsDeleted && x.TeachingGroupId == tgId)
                .Select(x => x.SectionId)
                .ToListAsync(cancellationToken);
            var linkedSections = tgsLinks.Count == 0
                ? []
                : await _db.Sections.AsNoTracking()
                    .Where(s => !s.IsDeleted && tgsLinks.Contains(s.Id))
                    .Select(s => new { s.Id, s.SemesterId, s.CourseId, s.GroupId, s.TenantId })
                    .ToListAsync(cancellationToken);

            var compatible = 0;
            var incompatible = 0;
            foreach (var sec in linkedSections)
            {
                if (sec.TenantId != tg.TenantId)
                {
                    tenantIsolationOk = false;
                    incompatible++;
                    findings.Add(Finding("CROSS_TENANT_TGS_SECTION", TgRemediationFindingSeverity.Critical,
                        "Section", sec.Id, sec.SemesterId, targetId, sec.GroupId, tg.GroupId,
                        $"Section tenant != TeachingGroup {tgId} tenant.", "BLOCKED"));
                    continue;
                }

                var ok = targetValid
                         && targetCourseId is int tCourse
                         && targetGroupId is int tGroup
                         && sec.CourseId == tCourse
                         && sec.GroupId == tGroup
                         && sec.SemesterId == targetId;
                if (ok) compatible++;
                else
                {
                    incompatible++;
                    findings.Add(Finding("TG_SECTION_INCOMPATIBLE", TgRemediationFindingSeverity.Error,
                        "Section", sec.Id, sec.SemesterId, targetId, sec.GroupId, targetGroupId,
                        $"Section {sec.Id} incompatible with TG {tgId} target Sem {targetId} (Sec Sem={sec.SemesterId} Course={sec.CourseId} Group={sec.GroupId}).",
                        status == TgRemediationReadinessStatus.AlreadyComplete ? "MANUAL_REVIEW_REQUIRED" : "BLOCKED"));
                }
            }

            // If preview said Ready but sections incompatible, force BLOCKED
            if (status == TgRemediationReadinessStatus.ReadyFor3FReexecution && incompatible > 0)
            {
                status = TgRemediationReadinessStatus.Blocked;
                code = "BLOCKED";
            }

            // Ambiguous: never mark ready if target invalid
            if (status == TgRemediationReadinessStatus.ReadyFor3FReexecution && !targetValid)
            {
                status = TgRemediationReadinessStatus.Blocked;
                code = "BLOCKED";
            }

            switch (status)
            {
                case TgRemediationReadinessStatus.ReadyFor3FReexecution:
                    readyIds.Add(tgId);
                    break;
                case TgRemediationReadinessStatus.AlreadyComplete:
                    alreadyIds.Add(tgId);
                    break;
                case TgRemediationReadinessStatus.ManualReviewRequired:
                    manualIds.Add(tgId);
                    break;
                default:
                    blockedIds.Add(tgId);
                    break;
            }

            findings.Add(Finding("TG_READINESS",
                status is TgRemediationReadinessStatus.Blocked or TgRemediationReadinessStatus.ManualReviewRequired
                    ? TgRemediationFindingSeverity.Error
                    : TgRemediationFindingSeverity.Info,
                "TeachingGroup", tgId, tg.SemesterId, targetId, tg.GroupId, targetGroupId,
                item?.Reason ?? code,
                code));

            tgRows.Add(new TgRemediationTeachingGroupRowDto
            {
                TeachingGroupId = tg.Id,
                Code = tg.Code,
                Name = tg.Name,
                CourseId = tg.CourseId,
                GroupId = tg.GroupId,
                CurrentSemesterId = tg.SemesterId,
                TargetSemesterId = targetId,
                Readiness = status,
                ReadinessCode = code,
                Reason = item?.Reason ?? code,
                TeachingGroupSectionCount = linkedSections.Count,
                CompatibleSectionCount = compatible,
                IncompatibleSectionCount = incompatible,
                LinkedSectionIds = linkedSections.Select(s => s.Id).OrderBy(x => x).ToList(),
            });
        }

        // Unexpected TG on legacy Sem 3 outside approved set
        var unexpectedLegacyTg = await _db.SchedulingTeachingGroups.AsNoTracking()
            .Where(t => !t.IsDeleted && t.SemesterId == legacyId && !approved.Contains(t.Id))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);
        foreach (var id in unexpectedLegacyTg)
            findings.Add(Finding("UNEXPECTED_LEGACY_TG", TgRemediationFindingSeverity.Critical,
                "TeachingGroup", id, legacyId, targetId, null, targetGroupId,
                $"TeachingGroup {id} on legacy Sem 3 outside approved 3F set.", "BLOCKED"));

        var critical = findings.Count(f => f.Severity == TgRemediationFindingSeverity.Critical);
        var errors = findings.Count(f => f.Severity == TgRemediationFindingSeverity.Error);
        var warnings = findings.Count(f => f.Severity == TgRemediationFindingSeverity.Warning);

        // Can re-execute 3F only when: target valid, no Att/SA/TT regression, no legacy Sections linked to approved TGs blocking,
        // and every approved TG is READY (pending remap) OR all ALREADY_COMPLETE with zero READY needed.
        // Architect: CanReExecute when there is work (READY) and nothing BLOCKED/MANUAL for approved set.
        var anyBlockedOrManual = blockedIds.Count > 0 || manualIds.Count > 0 || unexpectedLegacyTg.Count > 0;
        var canReExecute = targetValid
                           && legacyValid
                           && !regression
                           && tenantIsolationOk
                           && !anyBlockedOrManual
                           && readyIds.Count > 0
                           && critical == 0;

        // Healthy: no critical/error (warnings allowed for deferred Subject), TGs already complete or ready set clean
        var isHealthy = critical == 0
                        && errors == 0
                        && !regression
                        && tenantIsolationOk
                        && targetValid
                        && legacySections.Count == 0
                        && (alreadyIds.Count == approved.Count || (readyIds.Count > 0 && !anyBlockedOrManual));

        // If all already complete and no legacy sections — healthy, canReExecute false (nothing to do)
        if (alreadyIds.Count == approved.Count && legacySections.Count == 0 && !regression && critical == 0 && errors == 0)
        {
            isHealthy = true;
            canReExecute = false;
            notes.Add("All approved Teaching Groups ALREADY_COMPLETE; 3F re-execution not required.");
        }

        var tenantStatus = tenantIsolationOk ? "PASS" : "FAIL_CLOSED";

        notes.Add($"IsHealthy={isHealthy}; CanReExecute={canReExecute}; Ready=[{string.Join(",", readyIds)}]; Already=[{string.Join(",", alreadyIds)}]; Blocked=[{string.Join(",", blockedIds)}]; Manual=[{string.Join(",", manualIds)}].");

        return new TeachingGroupRemediationReadinessResultDto
        {
            GeneratedUtc = DateTime.UtcNow,
            PromptCode = PromptCode,
            TenantId = tenantId,
            IsReadOnly = true,
            NoMutationsPerformed = true,
            SaveChangesInvoked = false,
            Prompt3FExecuteInvoked = false,
            IsHealthy = isHealthy,
            CanReExecuteTeachingGroupRemediation = canReExecute,
            CriticalCount = critical,
            ErrorCount = errors,
            WarningCount = warnings,
            ApprovedTeachingGroupIds = approved,
            ReadyTeachingGroupIds = readyIds,
            BlockedTeachingGroupIds = blockedIds.Distinct().ToList(),
            AlreadyCompleteTeachingGroupIds = alreadyIds,
            ManualReviewTeachingGroupIds = manualIds,
            SectionLegacyReferenceCount = legacySections.Count,
            TeachingGroupLegacyReferenceCount = tgLegacyCount,
            TargetSemesterValidation = targetValidation,
            TenantIsolationStatus = tenantStatus,
            TenantIsolationOk = tenantIsolationOk,
            DownstreamRegression = downstream,
            Findings = findings,
            TeachingGroups = tgRows,
            LegacySections = legacySectionRows,
            Notes = notes,
            RecommendedNextPrompt = canReExecute
                ? "Chief Architect may authorize controlled Prompt 3F re-execution (TeachingGroup.SemesterId only for approved TG IDs). Do not auto-start."
                : alreadyIds.Count == approved.Count && legacySections.Count == 0
                    ? "Teaching Group Sem-3 remediation already complete for approved set. Next: schema-hardening / remaining NULL-group Semester disposition under separate Architect prompt — do not re-execute 3F."
                    : "Do NOT execute Prompt 3F. Resolve Blocking/Manual findings (Section Sem-3, SA/TT/Attendance regression, target ownership, unexpected legacy TGs) first.",
        };
    }

    private static (TgRemediationReadinessStatus Status, string Code) MapPreviewStatus(
        TeachingGroupSemesterRemediationItemDto? item,
        string previewExecutionStatus)
    {
        if (item is null)
            return (TgRemediationReadinessStatus.Blocked, "BLOCKED");

        return item.StatusKind switch
        {
            TeachingGroupSemesterRemediationStatus.Ready =>
                (TgRemediationReadinessStatus.ReadyFor3FReexecution, "READY_FOR_3F_REEXECUTION"),
            TeachingGroupSemesterRemediationStatus.AlreadyComplete =>
                (TgRemediationReadinessStatus.AlreadyComplete, "ALREADY_COMPLETE"),
            TeachingGroupSemesterRemediationStatus.ManualReviewRequired =>
                (TgRemediationReadinessStatus.ManualReviewRequired, "MANUAL_REVIEW_REQUIRED"),
            _ => (TgRemediationReadinessStatus.Blocked, "BLOCKED"),
        };
    }

    private static TgRemediationFindingDto Finding(
        string code,
        TgRemediationFindingSeverity severity,
        string entityType,
        int? entityId,
        int? currentSem,
        int? targetSem,
        int? currentGroup,
        int? targetGroup,
        string reason,
        string remediationStatus)
        => new()
        {
            Code = code,
            Severity = severity,
            SeverityCode = severity.ToString().ToUpperInvariant(),
            EntityType = entityType,
            EntityId = entityId,
            CurrentSemesterId = currentSem,
            TargetSemesterId = targetSem,
            CurrentGroupId = currentGroup,
            TargetGroupId = targetGroup,
            Reason = reason,
            RemediationStatus = remediationStatus,
        };
}
