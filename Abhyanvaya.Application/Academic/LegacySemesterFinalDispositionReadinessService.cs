using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3I (Architect package 3I2) / PromptCode P1-4-3N —
/// Final disposition + schema hardening readiness gate.
/// Composes existing P1-4 audits (3D finalization, 3M schema hardening, 3H2 TG readiness).
/// Zero mutations. Zero DDL. Does not compete as a second migration engine.
/// </summary>
public sealed class LegacySemesterFinalDispositionReadinessService : ILegacySemesterFinalDispositionReadinessService
{
    public const string PromptCode = "P1-4-3N";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISemesterSchemaHardeningReadinessService _schemaHardening;
    private readonly ILegacySemesterFinalizationAuditService _finalization;
    private readonly ITeachingGroupRemediationReadinessService _tgReadiness;

    public LegacySemesterFinalDispositionReadinessService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ISemesterSchemaHardeningReadinessService schemaHardening,
        ILegacySemesterFinalizationAuditService finalization,
        ITeachingGroupRemediationReadinessService tgReadiness)
    {
        _db = db;
        _currentUser = currentUser;
        _schemaHardening = schemaHardening;
        _finalization = finalization;
        _tgReadiness = tgReadiness;
    }

    public async Task<LegacySemesterFinalDispositionReadinessResultDto> BuildAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var notes = new List<string>
        {
            "Prompt 3I FINAL DISPOSITION + SCHEMA HARDENING READINESS — AUDIT GATE ONLY.",
            $"PromptCode={PromptCode} (package 3I2; Finance Section remediation already owns P1-4-3I).",
            "Composes 3M schema-hardening readiness + 3D finalization + 3H2 TG readiness.",
            "Zero SaveChanges. Zero DDL. Zero TG/Section/Attendance/SA/TT/Semester mutation.",
            "NULL-group rows marked RETAIN_HISTORICAL still block NOT NULL while present in operational Semester table.",
        };

        var schema = await _schemaHardening.BuildAsync(cancellationToken);
        var fin = await _finalization.BuildAuditAsync(cancellationToken);
        var tg = await _tgReadiness.BuildAsync(cancellationToken);

        notes.Add($"Embedded 3M Decision={schema.DecisionCode}; NullGroup={schema.NullGroupSemesterCount}; Dup={schema.DuplicateKeyCount}.");
        notes.Add($"Embedded 3H2 TG IsHealthy={tg.IsHealthy}; AlreadyComplete=[{string.Join(",", tg.AlreadyCompleteTeachingGroupIds)}].");

        var finById = fin.LegacySemesters.ToDictionary(r => r.SemesterId);
        var downstreamBySem = schema.DownstreamLegacyReferences
            .GroupBy(d => d.SemesterId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var legacyRows = new List<FinalLegacySemesterDispositionRowDto>();
        foreach (var nullRow in schema.NullGroupSemesters.OrderBy(r => r.SemesterId))
        {
            finById.TryGetValue(nullRow.SemesterId, out var finRow);
            var refs = downstreamBySem.GetValueOrDefault(nullRow.SemesterId) ?? [];

            int Count(string entity) => refs.Where(r => r.ReferenceEntity == entity).Sum(r => r.ReferenceCount);

            var student = finRow?.StudentReferenceCount ?? Count("Student");
            var attendance = finRow?.AttendanceReferenceCount ?? Count("AttendanceSession");
            var subject = finRow?.SubjectReferenceCount ?? Count("Subject");
            var section = finRow?.SectionReferenceCount ?? Count("Section");
            var sa = finRow?.SubjectAllocationReferenceCount ?? Count("SubjectAllocation");
            var tt = finRow?.TimetableEntryReferenceCount ?? Count("TimetableEntry");
            var tgr = finRow?.TeachingGroupReferenceCount ?? Count("TeachingGroup");
            var tgs = Count("TeachingGroupSection");
            var ts = Count("TimetableSection");

            var (disp, code, reason, blocking, mutation) = MapDisposition(nullRow, finRow, student, attendance, subject, section, sa, tt, tgr, tgs, ts);

            var dependents = new List<string>();
            if (student > 0) dependents.Add($"Student:{student}");
            if (attendance > 0) dependents.Add($"Attendance:{attendance}");
            if (subject > 0) dependents.Add($"Subject:{subject}");
            if (section > 0) dependents.Add($"Section:{section}");
            if (sa > 0) dependents.Add($"SubjectAllocation:{sa}");
            if (tt > 0) dependents.Add($"TimetableEntry:{tt}");
            if (tgr > 0) dependents.Add($"TeachingGroup:{tgr}");
            if (tgs > 0) dependents.Add($"TeachingGroupSection:{tgs}");
            if (ts > 0) dependents.Add($"TimetableSection:{ts}");

            legacyRows.Add(new FinalLegacySemesterDispositionRowDto
            {
                SemesterId = nullRow.SemesterId,
                CourseId = nullRow.CourseId,
                Number = nullRow.Number,
                Name = nullRow.Name,
                CurrentGroupId = nullRow.GroupId,
                TenantId = nullRow.TenantId,
                Disposition = disp,
                DispositionCode = code,
                Reason = reason,
                BlockingDependency = blocking,
                ProposedTargetGroupId = null, // never guess
                MutationPermitted = mutation,
                StudentRefs = student,
                AttendanceRefs = attendance,
                SubjectRefs = subject,
                SectionRefs = section,
                SubjectAllocationRefs = sa,
                TimetableEntryRefs = tt,
                TeachingGroupRefs = tgr,
                TeachingGroupSectionRefs = tgs,
                TimetableSectionRefs = ts,
                DependentEntities = dependents,
            });
        }

        // Outstanding operational refs on NULL-group Semesters
        var outstanding = new List<FinalOutstandingReferenceDto>();
        foreach (var d in schema.DownstreamLegacyReferences.OrderBy(x => x.SemesterId).ThenBy(x => x.ReferenceEntity))
        {
            var classification = d.ReferenceEntity switch
            {
                "Subject" when d.ReferenceCount > 0 => "historical",
                "Student" or "AttendanceSession" or "Section" or "SubjectAllocation"
                    or "TimetableEntry" or "TeachingGroup" or "TeachingGroupSection"
                    or "TimetableSection" or "StudentSection" => "unresolved",
                _ => "blocked",
            };
            foreach (var id in d.ReferenceIds.Take(25))
            {
                if (!int.TryParse(id, out var entityId))
                    continue;
                outstanding.Add(new FinalOutstandingReferenceDto
                {
                    EntityType = d.ReferenceEntity,
                    EntityId = entityId,
                    SemesterId = d.SemesterId,
                    Classification = classification,
                    Notes = d.BlockingReason,
                });
            }
        }

        // Evidence counts
        var evidence = new FinalDispositionEvidenceCountsDto
        {
            TotalSemesters = schema.SemesterCount,
            NullGroupSemesters = schema.NullGroupSemesterCount,
            GroupSpecificSemesters = Math.Max(0, schema.SemesterCount - schema.NullGroupSemesterCount),
            DuplicateKeyGroups = schema.DuplicateKeyCount,
            OrphanedSemesterReferenceSamples = schema.StudentIntegrity.OrphanedSemester,
            CrossCourseSemesterRefs = schema.InvalidOwnershipCount,
            CrossGroupSemesterRefs = schema.StudentIntegrity.Invalid,
            CrossTenantViolations = schema.CrossTenantViolationCount,
            StudentLegacyRefs = legacyRows.Sum(r => r.StudentRefs),
            AttendanceLegacyRefs = legacyRows.Sum(r => r.AttendanceRefs),
            SubjectLegacyRefs = legacyRows.Sum(r => r.SubjectRefs),
            SectionLegacyRefs = legacyRows.Sum(r => r.SectionRefs) + schema.SectionBlockingCount,
            SubjectAllocationLegacyRefs = legacyRows.Sum(r => r.SubjectAllocationRefs),
            TimetableEntryLegacyRefs = legacyRows.Sum(r => r.TimetableEntryRefs),
            TeachingGroupLegacyRefs = Math.Max(legacyRows.Sum(r => r.TeachingGroupRefs), schema.TeachingGroupBlockingCount),
            TeachingGroupSectionLegacyRefs = legacyRows.Sum(r => r.TeachingGroupSectionRefs),
            TimetableSectionLegacyRefs = legacyRows.Sum(r => r.TimetableSectionRefs),
            StudentIntegrityViolations = schema.StudentIntegrityViolationCount,
            ActiveWildcardDependencies = schema.WildcardProductionDependencyCount,
        };

        // Flags — strict gate (NULL rows in operational table always fail NullGroupReady)
        var nullGroupReady = schema.NullGroupSemesterCount == 0;
        var uniqueKeyReady = schema.UniqueReady && schema.DuplicateKeyCount == 0 && nullGroupReady;
        var studentReady = schema.StudentIntegrityViolationCount == 0;
        var downstreamReady = schema.DownstreamLegacyReferenceCount == 0
                              && schema.SchedulingIntegrityViolationCount == 0
                              && evidence.AttendanceLegacyRefs == 0
                              && evidence.SubjectAllocationLegacyRefs == 0
                              && evidence.TimetableEntryLegacyRefs == 0
                              && evidence.SectionLegacyRefs == 0;
        // Subject historical refs count in DownstreamLegacyReferenceCount — keep strict
        var tgBoundaryReady = schema.TeachingGroupBlockingCount == 0
                              && tg.TeachingGroupLegacyReferenceCount == 0
                              && tg.TenantIsolationOk
                              && (tg.AlreadyCompleteTeachingGroupIds.Count == tg.ApprovedTeachingGroupIds.Count
                                  || tg.IsHealthy);
        var tenantReady = schema.CrossTenantViolationCount == 0 && tg.TenantIsolationOk;
        var wildcardReady = schema.WildcardProductionDependencyCount == 0;
        var writePathReady = schema.WritePathsGroupOwned && schema.NoActiveNullGroupWritePath;
        // Migration safety: write paths + guards + deterministic + rollback design possible; still false if not ready to ALTER
        var migrationSafetyReady = writePathReady
                                   && schema.ArchitectureGuardsIntact
                                   && nullGroupReady
                                   && uniqueKeyReady;

        var blockers = new List<string>();
        if (!nullGroupReady)
            blockers.Add($"NullGroupReady=FALSE: {schema.NullGroupSemesterCount} Semester row(s) still have GroupId=NULL (Ids=[{string.Join(",", schema.NullGroupSemesters.Select(s => s.SemesterId))}]). RETAIN_HISTORICAL disposition does not remove rows from operational Semester table — NOT NULL cannot apply.");
        if (!uniqueKeyReady)
            blockers.Add($"UniqueKeyReady=FALSE: DuplicateKeyCount={schema.DuplicateKeyCount}; UniqueReady={schema.UniqueReady}.");
        if (!studentReady)
            blockers.Add($"StudentIntegrityReady=FALSE: violations={schema.StudentIntegrityViolationCount}.");
        if (!downstreamReady)
            blockers.Add($"DownstreamReferenceReady=FALSE: DownstreamLegacyReferenceCount={schema.DownstreamLegacyReferenceCount}; SchedulingViol={schema.SchedulingIntegrityViolationCount}; Att={evidence.AttendanceLegacyRefs}; SA={evidence.SubjectAllocationLegacyRefs}; TT={evidence.TimetableEntryLegacyRefs}; Section={evidence.SectionLegacyRefs}; Subject={evidence.SubjectLegacyRefs}.");
        if (!tgBoundaryReady)
            blockers.Add($"TeachingGroupBoundaryReady=FALSE: TGBlocking={schema.TeachingGroupBlockingCount}; TGLegacyRefs={tg.TeachingGroupLegacyReferenceCount}; TGHealthy={tg.IsHealthy}.");
        if (!tenantReady)
            blockers.Add($"TenantIsolationReady=FALSE: CrossTenant={schema.CrossTenantViolationCount}; TGIsolation={tg.TenantIsolationStatus}.");
        if (!wildcardReady)
            blockers.Add($"WildcardDependencyReady=FALSE: ActiveProduction={schema.WildcardProductionDependencyCount}.");
        if (!writePathReady)
            blockers.Add("WritePathReady=FALSE: Semester write paths do not fully enforce Group ownership / NULL GroupId rejection.");
        if (!migrationSafetyReady)
            blockers.Add("MigrationSafetyReady=FALSE: preconditions for transactional NOT NULL + UNIQUE not met.");

        // Unresolved manual dispositions among NULL-group rows
        var unresolvedManual = legacyRows.Count(r =>
            r.Disposition is FinalLegacySemesterDisposition.ManualMappingRequired
                or FinalLegacySemesterDisposition.DuplicateReview
                or FinalLegacySemesterDisposition.BlockedByReference
                or FinalLegacySemesterDisposition.BlockedByArchitecturalBoundary);
        if (unresolvedManual > 0)
            blockers.Add($"Unresolved disposition rows requiring Architect remediation: {unresolvedManual}.");

        // RETAIN_HISTORICAL still blocks NullGroupReady — already covered

        var isReady = nullGroupReady
                      && uniqueKeyReady
                      && studentReady
                      && downstreamReady
                      && tgBoundaryReady
                      && tenantReady
                      && wildcardReady
                      && writePathReady
                      && migrationSafetyReady
                      && unresolvedManual == 0
                      && schema.ArchitectureGuardsIntact;

        var warnings = new List<string>(schema.Warnings);
        if (tg.AlreadyCompleteTeachingGroupIds.Count == tg.ApprovedTeachingGroupIds.Count
            && tg.TeachingGroupLegacyReferenceCount == 0)
        {
            warnings.Add("Teaching Group approved set is ALREADY_COMPLETE with zero legacy TG Sem-3 refs — TG boundary does not block hardening by TG residual; NULL-group Semesters still do.");
        }

        foreach (var w in schema.WildcardDependencies.Where(x => !x.BlocksHardening))
            warnings.Add($"Wildcard {w.KindCode}: {w.Path} — {w.Notes}");

        SchemaHardeningMigrationContractDto? contract = null;
        if (isReady)
        {
            contract = BuildAuthorizedMigrationContract();
        }
        else
        {
            contract = new SchemaHardeningMigrationContractDto
            {
                AuthorizedForExecution = false,
                Title = "P1-4 Prompt 3J — Schema Hardening Execution (NOT AUTHORIZED)",
                Steps =
                [
                    "BLOCKED: Do not execute ALTER/UNIQUE until IsReady=TRUE.",
                    "Clear NullGroupReady by Architect-approved historical archive model OR deterministic Group mapping (no guessing).",
                    "Clear Subject/downstream outstanding refs under approved remediation.",
                    "Resolve DUPLICATE_REVIEW / MANUAL_MAPPING_REQUIRED dispositions.",
                    "Re-run this readiness gate (PromptCode P1-4-3N) until IsReady=TRUE.",
                    "Only then authorize Prompt 3J schema hardening execution with the full 15-step contract.",
                ],
                RollbackStrategy = "N/A — no DDL authorized in this prompt.",
                FailureBehavior = "Fail closed; do not partially apply constraints.",
                Notes = "Discovery-only blocked contract. Prefer Audit→Classify→Validate over mutate-to-pass.",
            };
        }

        notes.Add($"SchemaHardeningReady={isReady}; NullGroupReady={nullGroupReady}; UniqueKeyReady={uniqueKeyReady}.");

        return new LegacySemesterFinalDispositionReadinessResultDto
        {
            GeneratedUtc = DateTime.UtcNow,
            PromptCode = PromptCode,
            TenantId = tenantId,
            IsReadOnly = true,
            NoMutationsPerformed = true,
            SaveChangesInvoked = false,
            SchemaHardeningReady = isReady,
            IsReady = isReady,
            NullGroupReady = nullGroupReady,
            UniqueKeyReady = uniqueKeyReady,
            StudentIntegrityReady = studentReady,
            DownstreamReferenceReady = downstreamReady,
            TeachingGroupBoundaryReady = tgBoundaryReady,
            TenantIsolationReady = tenantReady,
            WildcardDependencyReady = wildcardReady,
            WritePathReady = writePathReady,
            MigrationSafetyReady = migrationSafetyReady,
            EvidenceCounts = evidence,
            BlockingReasons = blockers.Distinct(StringComparer.Ordinal).ToList(),
            Warnings = warnings.Distinct(StringComparer.Ordinal).ToList(),
            LegacySemesters = legacyRows,
            DuplicateKeys = schema.DuplicateKeys,
            OutstandingReferences = outstanding,
            WildcardDependencies = schema.WildcardDependencies,
            NextMigrationContract = contract,
            Notes = notes,
            RecommendedNextPrompt = isReady
                ? "SchemaHardeningReady=TRUE. Recommend P1-4 Prompt 3J — Schema Hardening Execution (NOT NULL + filtered UNIQUE) under Chief Architect authorization."
                : "SchemaHardeningReady=FALSE. Smallest next remediation: Architect-approved historical archive / exclusion model for NULL-group Semesters 1–5 AND Subject Sem-1 historical FK policy — do not guess GroupIds; do not apply DDL.",
        };
    }

    private static (FinalLegacySemesterDisposition Disp, string Code, string Reason, string? Blocking, bool Mutation)
        MapDisposition(
            NullGroupSemesterAuditRowDto nullRow,
            LegacySemesterInventoryRowDto? fin,
            int student, int attendance, int subject, int section, int sa, int tt, int tg, int tgs, int ts)
    {
        var ops = student + attendance + section + sa + tt + tg + tgs + ts;
        // Teaching Group / architectural boundary
        if (tg > 0 || tgs > 0 || (fin?.TeachingGroupReferenceCount ?? 0) > 0)
        {
            return (FinalLegacySemesterDisposition.BlockedByArchitecturalBoundary,
                "BLOCKED_BY_ARCHITECTURAL_BOUNDARY",
                "TeachingGroup / TeachingGroupSection still references this NULL-group Semester.",
                "TeachingGroup boundary",
                false);
        }

        if (ops - subject > 0)
        {
            return (FinalLegacySemesterDisposition.BlockedByReference,
                "BLOCKED_BY_REFERENCE",
                $"Operational references remain (Student={student}, Att={attendance}, Section={section}, SA={sa}, TT={tt}, TS={ts}).",
                "Operational downstream FK",
                false);
        }

        // Prefer finalization disposition when present
        if (fin is not null)
        {
            switch (fin.Disposition)
            {
                case LegacySemesterFinalizationDisposition.DuplicateReview:
                    return (FinalLegacySemesterDisposition.DuplicateReview, "DUPLICATE_REVIEW",
                        fin.DispositionEvidence, "Duplicate Semester number on Course", false);
                case LegacySemesterFinalizationDisposition.ManualMappingRequired:
                case LegacySemesterFinalizationDisposition.SplitRequired:
                case LegacySemesterFinalizationDisposition.UnknownRequiresArchitectDecision:
                    return (FinalLegacySemesterDisposition.ManualMappingRequired, "MANUAL_MAPPING_REQUIRED",
                        fin.DispositionEvidence, "Ambiguous multi-Group Course", false);
                case LegacySemesterFinalizationDisposition.BlockedByTeachingGroupReference:
                    return (FinalLegacySemesterDisposition.BlockedByArchitecturalBoundary,
                        "BLOCKED_BY_ARCHITECTURAL_BOUNDARY", fin.DispositionEvidence, "TG residual", false);
                case LegacySemesterFinalizationDisposition.HistoricalRetain:
                    return (FinalLegacySemesterDisposition.RetainHistorical, "RETAIN_HISTORICAL",
                        subject > 0
                            ? $"{fin.DispositionEvidence} Subject historical refs={subject} still present — blocks NOT NULL until archive/exclusion."
                            : fin.DispositionEvidence,
                        subject > 0 ? "Subject historical FK in operational table" : null,
                        false);
                case LegacySemesterFinalizationDisposition.SafeSingleGroupMapping:
                case LegacySemesterFinalizationDisposition.AlreadyGroupSpecific:
                    // Still NULL in DB — cannot claim FINALIZED_GROUP_SPECIFIC until GroupId set via approved mutation prompt
                    return (FinalLegacySemesterDisposition.ManualMappingRequired, "MANUAL_MAPPING_REQUIRED",
                        "Classifier suggested mapping but Semester.GroupId remains NULL; no silent assignment.",
                        "Unapplied mapping", false);
            }
        }

        // Fallback from 3M disposition codes
        return nullRow.DispositionCode switch
        {
            "RETAIN_HISTORICAL" or "HISTORICAL_RETAIN" =>
                (FinalLegacySemesterDisposition.RetainHistorical, "RETAIN_HISTORICAL",
                    nullRow.Evidence + (subject > 0 ? $" SubjectRefs={subject}." : ""),
                    subject > 0 ? "Subject historical FK" : null, false),
            "MANUAL_MAPPING_REQUIRED" =>
                (FinalLegacySemesterDisposition.ManualMappingRequired, "MANUAL_MAPPING_REQUIRED",
                    nullRow.Evidence, "Ambiguous", false),
            "DUPLICATE_REVIEW" or "OTHER_EXPLICIT_APPROVED_STATE" when nullRow.Evidence.Contains("DUPLICATE", StringComparison.OrdinalIgnoreCase) =>
                (FinalLegacySemesterDisposition.DuplicateReview, "DUPLICATE_REVIEW", nullRow.Evidence, "Duplicate", false),
            _ when subject > 0 =>
                (FinalLegacySemesterDisposition.BlockedByReference, "BLOCKED_BY_REFERENCE",
                    $"Subject refs={subject} on NULL-group Semester.", "Subject FK", false),
            _ =>
                (FinalLegacySemesterDisposition.ManualMappingRequired, "MANUAL_MAPPING_REQUIRED",
                    string.IsNullOrWhiteSpace(nullRow.Evidence) ? "Unexplained NULL-group Semester." : nullRow.Evidence,
                    "Unclassified", false),
        };
    }

    private static SchemaHardeningMigrationContractDto BuildAuthorizedMigrationContract()
        => new()
        {
            AuthorizedForExecution = true,
            Title = "P1-4 Prompt 3J — Schema Hardening Execution",
            Steps =
            [
                "1. Pre-migration audit (re-run P1-4-3N readiness; require IsReady=TRUE).",
                "2. Single transaction boundary for all DDL assertions + alterations.",
                "3. Duplicate assertion: COUNT(*) GROUP BY TenantId,GroupId,Number HAVING COUNT>1 must be 0 (non-deleted).",
                "4. NULL assertion: COUNT(*) WHERE GroupId IS NULL (incl. soft-deleted DBA scan) must be 0.",
                "5. FK validation: every Semester.GroupId exists; CourseId==Group.CourseId.",
                "6. Tenant validation: Semester.TenantId==Group.TenantId==Course.TenantId.",
                "7. Student validation: Student.GroupId==Semester.GroupId and Course alignment.",
                "8. Downstream validation: zero operational refs to NULL-group Semesters.",
                "9. ALTER Semester.GroupId SET NOT NULL.",
                "10. CREATE UNIQUE filtered index UNIQUE(TenantId, GroupId, Number) WHERE IsDeleted=0 (or equivalent).",
                "11. Rollback strategy: reverse migration drops UNIQUE then restores nullability only if backup proves safe; prefer restore from pre-migration backup.",
                "12. Post-migration audit: re-run P1-4-3N + 3M; expect IsReady remains TRUE with NullGroup=0.",
                "13. Idempotency: second execution detects constraints already present; zero additional writes.",
                "14. Deployment verification: API health + CanManageSemesters readiness endpoint + smoke create Semester with GroupId.",
                "15. Failure behavior: abort transaction; fail closed; do not leave partial UNIQUE without NOT NULL (or vice versa) without documented recovery.",
            ],
            RollbackStrategy =
                "Take full DB backup before DDL. On failure ROLLBACK transaction. If constraints partially applied outside a transaction, restore from backup. Never silently delete Semesters to force success.",
            FailureBehavior =
                "Any assertion failure aborts before ALTER. Concurrency conflicts fail closed. No automatic merge/delete.",
            Notes = "Authorized only because Prompt 3I2 gate returned IsReady=TRUE.",
        };
}
