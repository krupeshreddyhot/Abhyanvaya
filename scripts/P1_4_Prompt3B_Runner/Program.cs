using System.Text.Json;
using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Local runner for P1-4 Prompt 3B / 3B-A / 3C / 3D / 3E.
/// Args: --preflight | --execute | --integrity | --remediate-preview | --remediate-execute
///       | --finalization | --finalization-preview | --finalization-execute
/// </summary>
static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var mode = args.FirstOrDefault()?.Trim().ToLowerInvariant() ?? "--preflight";
        static string FindApiPath()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "Abhyanvaya.API");
                if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "appsettings.json")))
                    return candidate;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate Abhyanvaya.API with appsettings.json.");
        }

        var apiPath = FindApiPath();

        var config = new ConfigurationBuilder()
            .SetBasePath(apiPath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cs = config.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(cs))
        {
            Console.Error.WriteLine("ABORT: DefaultConnection missing.");
            return 2;
        }

        var user = new FixedUser { TenantId = 1, UserId = 1, Role = "Admin" };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(cs)
            .Options;
        await using var db = new ApplicationDbContext(options, user, LoggerFactory.Create(b => b.AddConsole()).CreateLogger<ApplicationDbContext>());
        await db.Database.MigrateAsync();

        var planSvc = new LegacySemesterMigrationDecisionPlanService(db, user);
        var integrity = new SemesterPostMigrationIntegrityAuditService(db, user, planSvc);
        var remediate = new LegacySemesterDownstreamRemediationService(
            db, user, integrity, LoggerFactory.Create(b => b.AddConsole()).CreateLogger<LegacySemesterDownstreamRemediationService>());

        if (mode is "--remediate-preview" or "--remediate-audit")
        {
            var report = await remediate.PreviewAsync();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                report.ExecutionStatus,
                report.LegacySemesterId,
                report.Summary,
                ByType = report.Items.GroupBy(i => i.EntityType).Select(g => new
                {
                    Entity = g.Key,
                    Ready = g.Count(x => x.Status == DownstreamRemediationStatus.Ready),
                    Deferred = g.Count(x => x.Status == DownstreamRemediationStatus.DeferredByArchitectureBoundary),
                    Manual = g.Count(x => x.Status == DownstreamRemediationStatus.ManualReviewRequired),
                }),
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        if (mode is "--tg-remediate-preview")
        {
            var finAudit = new LegacySemesterFinalizationAuditService(db, user, new LegacySemesterMigrationAuditService(db, user), planSvc);
            var projector = new Abhyanvaya.Application.Scheduling.TimetableSectionProjector(db, user);
            var svc = new TeachingGroupSemesterRemediationService(
                db, user, projector, integrity, finAudit,
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<TeachingGroupSemesterRemediationService>());
            var report = await svc.PreviewAsync();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                report.ExecutionStatus,
                report.IsReadOnly,
                report.ExecutionSafe,
                report.ChangedCount,
                report.AlreadyCompleteCount,
                report.ManualReviewCount,
                report.BlockedCount,
                report.ApprovedTeachingGroupIds,
                Items = report.Items.Select(i => new
                {
                    i.TeachingGroupId,
                    i.Code,
                    i.CurrentSemesterId,
                    i.TargetSemesterId,
                    i.StatusCode,
                    i.Reason,
                    i.SubjectAllocationConsistent,
                    i.TeachingGroupSectionCount,
                    i.TimetableEntryCount,
                    Sections = i.SectionChecks.Select(s => new
                    {
                        s.SectionId,
                        s.SectionSemesterId,
                        s.IsCompatible,
                        s.Notes,
                    }),
                }),
                report.AbortReason,
                report.Notes,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        if (mode is "--teaching-group-remediation-readiness" or "--tg-remediation-readiness")
        {
            var finAuditR = new LegacySemesterFinalizationAuditService(db, user, new LegacySemesterMigrationAuditService(db, user), planSvc);
            var projectorR = new Abhyanvaya.Application.Scheduling.TimetableSectionProjector(db, user);
            var tgSvcR = new TeachingGroupSemesterRemediationService(
                db, user, projectorR, integrity, finAuditR,
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<TeachingGroupSemesterRemediationService>());
            var readiness = new TeachingGroupRemediationReadinessService(db, user, tgSvcR);
            var report = await readiness.BuildAsync();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                report.PromptCode,
                report.IsReadOnly,
                report.NoMutationsPerformed,
                report.SaveChangesInvoked,
                report.Prompt3FExecuteInvoked,
                report.IsHealthy,
                report.CanReExecuteTeachingGroupRemediation,
                report.CriticalCount,
                report.ErrorCount,
                report.WarningCount,
                report.ApprovedTeachingGroupIds,
                report.ReadyTeachingGroupIds,
                report.BlockedTeachingGroupIds,
                report.AlreadyCompleteTeachingGroupIds,
                report.ManualReviewTeachingGroupIds,
                report.SectionLegacyReferenceCount,
                report.TeachingGroupLegacyReferenceCount,
                report.TargetSemesterValidation,
                report.TenantIsolationStatus,
                report.DownstreamRegression,
                TeachingGroups = report.TeachingGroups.Select(t => new
                {
                    t.TeachingGroupId,
                    t.CurrentSemesterId,
                    t.ReadinessCode,
                    t.CompatibleSectionCount,
                    t.IncompatibleSectionCount,
                    t.LinkedSectionIds,
                    t.Reason,
                }),
                LegacySections = report.LegacySections,
                Findings = report.Findings.Select(f => new { f.Code, f.SeverityCode, f.EntityType, f.EntityId, f.Reason, f.RemediationStatus }),
                report.RecommendedNextPrompt,
                report.Notes,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return report.IsHealthy || report.CanReExecuteTeachingGroupRemediation ? 0 : 15;
        }

        if (mode is "--section-remediate-preview")
        {
            var sectionSvc = new SectionSemesterRemediationService(
                db, user,
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<SectionSemesterRemediationService>());
            var report = await sectionSvc.PreviewAsync();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                report.ExecutionStatus,
                report.IsReadOnly,
                report.ExecutionSafe,
                report.LegacySemesterId,
                report.TargetSemesterId,
                report.TargetCourseId,
                report.TargetGroupId,
                report.EligibleCount,
                report.BlockedCount,
                report.ManualReviewCount,
                report.AlreadyCompleteCount,
                report.ApprovedSectionIds,
                Items = report.Items.Select(i => new
                {
                    i.SectionId,
                    i.SectionCode,
                    i.SectionName,
                    i.CourseId,
                    i.GroupId,
                    i.CurrentSemesterId,
                    i.TargetSemesterId,
                    i.StatusCode,
                    i.Reason,
                    i.InApprovedSet,
                    i.ReferencingTeachingGroupIds,
                    i.TeachingGroupSectionLinkCount,
                    i.CurrentStudentSectionCount,
                }),
                report.AbortReason,
                report.Notes,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        if (mode is "--section-semester-remediation-audit" or "--section-remediation-audit")
        {
            var auditSvc = new SectionSemesterRemediationAuditService(db, user);
            var report = await auditSvc.BuildAuditAsync();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                report.PromptCode,
                report.IsReadOnly,
                report.NoMutationsPerformed,
                report.SaveChangesInvoked,
                report.ReadinessCode,
                report.IsReady,
                report.TotalLegacySections,
                report.SafeFinanceCount,
                report.SafeCaCount,
                report.AlreadyCorrectCount,
                report.ManualMappingCount,
                report.BlockedCount,
                report.InvalidCount,
                report.TeachingGroupSectionDependencyCount,
                report.FinanceTargetValid,
                report.CaTargetValid,
                report.FinanceTargetValidationNotes,
                report.CaTargetValidationNotes,
                Sections = report.Sections.Select(s => new
                {
                    s.SectionId,
                    s.SectionCode,
                    s.CurrentSemesterId,
                    s.CurrentGroupId,
                    s.ResolvedGroupId,
                    s.TargetSemesterId,
                    s.ClassificationCode,
                    s.IsDeterministic,
                    s.TeachingGroupSectionCount,
                    s.StudentSectionCount,
                    s.TimetableSectionCount,
                    s.ResolutionReason,
                    s.BlockingReasons,
                }),
                Tgs = report.TeachingGroupSections.Select(t => new
                {
                    t.TeachingGroupSectionId,
                    t.TeachingGroupId,
                    t.SectionId,
                    t.TeachingGroupSemesterId,
                    t.SectionSemesterId,
                    t.CompatibilityCode,
                    t.Notes,
                }),
                report.BlockingReasons,
                report.Warnings,
                report.RecommendedNextPrompt,
                report.Notes,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return report.IsReady ? 0 : 14;
        }

        if (mode is "--subject-catalog-remediate-preview")
        {
            var subjSvc = new SubjectCatalogSemesterRemediationService(
                db, user,
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<SubjectCatalogSemesterRemediationService>());
            var report = await subjSvc.PreviewAsync();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                report.ExecutionStatus,
                report.IsReadOnly,
                report.ExecutionSafe,
                report.SafeToRemapCount,
                report.ManualMappingCount,
                report.BlockedCount,
                report.HistoricalRetainCount,
                report.AlreadyCorrectCount,
                report.AlreadyCompleteCount,
                Items = report.Items.Select(i => new
                {
                    i.SubjectId,
                    i.TenantSubjectId,
                    i.CourseId,
                    i.GroupId,
                    i.CurrentSemesterId,
                    i.CurrentSemesterNumber,
                    i.CurrentSemesterIsNullGroup,
                    i.TargetSemesterId,
                    i.CandidateTargetSemesterIds,
                    i.StatusCode,
                    i.Reason,
                    i.ReferencingTeachingGroupIds,
                    i.SubjectAllocationCount,
                }),
                report.AbortReason,
                report.Notes,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        if (mode is "--subject-catalog-remediate-execute")
        {
            var subjExec = new SubjectCatalogSemesterRemediationService(
                db, user,
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<SubjectCatalogSemesterRemediationService>());
            var report = await subjExec.ExecuteAsync();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                report.ExecutionStatus,
                report.RolledBack,
                report.TransactionCommitted,
                report.ChangedCount,
                report.SafeToRemapCount,
                report.AlreadyCompleteCount,
                report.ManualMappingCount,
                report.BlockedCount,
                report.HistoricalRetainCount,
                report.AffectedSubjectIds,
                report.CorrelationId,
                report.TeachingGroupsUnchanged,
                report.SubjectAllocationsUnchanged,
                report.AbortReason,
                Items = report.Items.Where(i => i.MutationAllowed || i.StatusCode is "ALREADY_COMPLETE" or "BLOCKED" or "MANUAL_MAPPING_REQUIRED" or "HISTORICAL_RETAIN")
                    .Select(i => new
                    {
                        i.SubjectId,
                        i.CurrentSemesterId,
                        i.TargetSemesterId,
                        i.StatusCode,
                        i.Reason,
                    }),
                report.Notes,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return string.Equals(report.ExecutionStatus, "Aborted", StringComparison.Ordinal) ? 12 : 0;
        }

        if (mode is "--finance-section-remediate-preview")
        {
            var financeSvc = new FinanceSectionSemesterRemediationService(
                db, user,
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<FinanceSectionSemesterRemediationService>());
            var report = await financeSvc.PreviewAsync();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                report.ExecutionStatus,
                report.IsReadOnly,
                report.ExecutionSafe,
                report.LegacySemesterId,
                report.TargetSemesterId,
                report.TargetCourseId,
                report.TargetFinanceGroupId,
                report.EligibleCount,
                report.BlockedCount,
                report.ManualReviewCount,
                report.NotInScopeCount,
                report.AlreadyCompleteCount,
                report.ApprovedSectionIds,
                Items = report.Items.Select(i => new
                {
                    i.SectionId,
                    i.SectionCode,
                    i.GroupId,
                    i.CurrentSemesterId,
                    i.TargetSemesterId,
                    i.StatusCode,
                    i.Reason,
                    i.ReferencingTeachingGroupIds,
                }),
                report.AbortReason,
                report.Notes,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        if (mode is "--finance-section-remediate-execute")
        {
            var financeExec = new FinanceSectionSemesterRemediationService(
                db, user,
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<FinanceSectionSemesterRemediationService>());
            var report = await financeExec.ExecuteAsync();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                report.ExecutionStatus,
                report.RolledBack,
                report.TransactionCommitted,
                report.ChangedCount,
                report.AlreadyCompleteCount,
                report.EligibleCount,
                report.BlockedCount,
                report.NotInScopeCount,
                report.ApprovedSectionIds,
                report.AffectedSectionIds,
                report.TeachingGroupsUnchanged,
                report.TeachingGroupSectionsUnchanged,
                report.StudentsUnchanged,
                report.AbortReason,
                Items = report.Items.Select(i => new
                {
                    i.SectionId,
                    i.CurrentSemesterId,
                    i.TargetSemesterId,
                    i.StatusCode,
                    i.Reason,
                }),
                report.Notes,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return string.Equals(report.ExecutionStatus, "Aborted", StringComparison.Ordinal) ? 12 : 0;
        }

        if (mode is "--prompt3h-audit" or "--post-section-integrity")
        {
            var finAudit3h = new LegacySemesterFinalizationAuditService(db, user, new LegacySemesterMigrationAuditService(db, user), planSvc);
            var svc3h = new Prompt3HPostSectionIntegrityAuditService(db, user, integrity, finAudit3h);
            var report = await svc3h.BuildAuditAsync();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                report.IsReadOnly,
                report.NoMutationsPerformed,
                report.SaveChangesInvoked,
                report.PromptCode,
                report.IsHealthy,
                report.CriticalCount,
                report.ErrorCount,
                report.WarningCount,
                Prompt3G = report.Prompt3GVerification,
                Semester = report.SemesterInventory,
                Students = new { report.Students.TotalChecked, report.Students.HealthyCount, report.Students.LegacyNullGroupRefs, report.Students.IncompatibleRefs },
                Attendance = new { report.Attendance.TotalChecked, report.Attendance.LegacyNullGroupRefs, report.Attendance.IncompatibleRefs },
                Subjects = new { report.Subjects.TotalChecked, report.Subjects.LegacyNullGroupRefs, report.Subjects.IncompatibleRefs },
                Sections = new { report.Sections.TotalChecked, report.Sections.HealthyCount, report.Sections.LegacyNullGroupRefs, report.Sections.IncompatibleRefs },
                SA = new { report.SubjectAllocations.TotalChecked, report.SubjectAllocations.LegacyNullGroupRefs, report.SubjectAllocations.IncompatibleRefs },
                TT = new { report.TimetableEntries.TotalChecked, report.TimetableEntries.LegacyNullGroupRefs, report.TimetableEntries.IncompatibleRefs },
                TG = new { report.TeachingGroups.TotalChecked, report.TeachingGroups.OnGroupSpecificSemester, report.TeachingGroups.LegacyNullGroupRefs },
                TGS = new { report.TeachingGroupSections.TotalLinksChecked, report.TeachingGroupSections.CompatibleCount, report.TeachingGroupSections.IncompatibleCount },
                TimetableSection = report.TimetableSections,
                Legacy = report.LegacyClassifications.Select(c => new { c.SemesterId, c.ClassificationCode, c.SectionRefs, c.SubjectRefs, c.TeachingGroupRefs, c.BlocksSchemaHardening, c.Evidence }),
                Wildcards = report.WildcardDependencyStatus.Select(w => new { w.Path, w.ClassificationCode }),
                WildcardCount = report.WildcardDependencies.Count,
                Schema = new
                {
                    report.SchemaHardening.NotNullReady,
                    report.SchemaHardening.NotNullVerdict,
                    report.SchemaHardening.NotNullBlockers,
                    report.SchemaHardening.UniqueReady,
                    report.SchemaHardening.UniqueVerdict,
                    report.SchemaHardening.UniqueBlockers,
                    report.DownstreamReady,
                    report.TenantIsolationReady,
                    report.StudentIntegrityReady,
                    report.SectionIntegrityReady,
                    report.TeachingGroupBoundaryReady,
                    report.SemesterHardeningReadyCode,
                    report.CanMakeGroupIdNotNull,
                    report.CanAddGroupSemesterUniqueConstraint,
                    report.CanRemoveLegacyWildcardSemantics,
                    report.SchemaHardening.SchemaHardeningPromptSafeToBegin,
                },
                Program = report.ProgramOptionality,
                DepartmentSsot = report.DepartmentSsot,
                TenantIsolation = report.TenantIsolation,
                TgResiduals = report.TeachingGroups.Residuals.Select(r => new { r.TeachingGroupId, r.ClassificationCode, r.Evidence }),
                report.ExactBlockers,
                report.RecommendedNextStep,
                report.Notes,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        if (mode is "--wildcard-retire-preview" or "--legacy-wildcard-preview")
        {
            var finAuditW = new LegacySemesterFinalizationAuditService(db, user, new LegacySemesterMigrationAuditService(db, user), planSvc);
            var svc3hW = new Prompt3HPostSectionIntegrityAuditService(db, user, integrity, finAuditW);
            var svcW = new LegacySemesterWildcardRetirementService(
                db, user, finAuditW, svc3hW,
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<LegacySemesterWildcardRetirementService>());
            var report = await svcW.PreviewAsync();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                report.IsReadOnly,
                report.PromptCode,
                report.ExecutionSafe,
                report.OperationalWildcardRetiredInCode,
                report.LegacySemesterCount,
                report.RetainedCount,
                report.ManualCount,
                report.BlockedCount,
                report.DuplicateReviewCount,
                report.ReadyForRetirementCount,
                report.ActiveOperationalDependencyCount,
                Items = report.Items.Select(i => new { i.SemesterId, i.DispositionCode, i.DependencyCount, i.CanExecute, i.Reason }),
                Wildcards = report.WildcardSites.Select(w => new { w.Path, w.ClassificationCode }),
                report.CanMakeGroupIdNotNull,
                report.CanAddGroupSemesterUniqueConstraint,
                report.CanRemoveLegacyWildcardSemantics,
                report.BlockingReasons,
                report.Notes,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        if (mode is "--legacy-wildcard-retirement-readiness" or "--wildcard-retirement-readiness")
        {
            var finAuditR = new LegacySemesterFinalizationAuditService(db, user, new LegacySemesterMigrationAuditService(db, user), planSvc);
            var svc3hR = new Prompt3HPostSectionIntegrityAuditService(db, user, integrity, finAuditR);
            var svcR = new LegacySemesterWildcardRetirementService(
                db, user, finAuditR, svc3hR,
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<LegacySemesterWildcardRetirementService>());
            var report = await svcR.BuildReadinessAsync();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                report.PromptCode,
                report.IsReadOnly,
                report.SaveChangesInvoked,
                report.LegacyNullGroupCount,
                report.ActiveLegacyWildcardCount,
                report.HistoricalOnlyCount,
                report.ManualMappingRequiredCount,
                report.DuplicateReviewCount,
                report.DownstreamReferenceCount,
                report.WildcardQueryDependencyCount,
                report.TenantIsolationPassed,
                report.OperationalSemesterResolutionPassed,
                report.HistoricalRetentionPassed,
                report.NewNullGroupWritePathBlocked,
                report.WildcardRetirementReady,
                report.CanMakeGroupIdNotNull,
                report.CanAddGroupSemesterUniqueConstraint,
                DispositionMatrix = report.DispositionMatrix.Select(i => new { i.SemesterId, i.Number, i.DispositionCode, i.SubjectRefs }),
                Semester1 = report.Semester1ManualMappingPreview is null ? null : new
                {
                    report.Semester1ManualMappingPreview.SemesterId,
                    report.Semester1ManualMappingPreview.SubjectReferenceCount,
                    report.Semester1ManualMappingPreview.DeterministicMappingProven,
                    report.Semester1ManualMappingPreview.ReasonMappingNotSafe,
                },
                Duplicates = report.DuplicateReviewPreviews.Select(d => new
                {
                    d.SemesterId, d.Number, d.SafeToRetainHistorically, d.DeterministicMappingProven, d.Evidence,
                }),
                report.Blockers,
                report.Warnings,
                report.RecommendedNextPrompt,
                report.Notes,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        if (mode is "--wildcard-retire-execute" or "--legacy-wildcard-execute")
        {
            var finAuditWx = new LegacySemesterFinalizationAuditService(db, user, new LegacySemesterMigrationAuditService(db, user), planSvc);
            var svc3hWx = new Prompt3HPostSectionIntegrityAuditService(db, user, integrity, finAuditWx);
            var svcWx = new LegacySemesterWildcardRetirementService(
                db, user, finAuditWx, svc3hWx,
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<LegacySemesterWildcardRetirementService>());
            var report = await svcWx.ExecuteAsync();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                report.ExecutionStatus,
                report.RolledBack,
                report.TransactionCommitted,
                report.ChangedCount,
                report.AlreadyCompleteCount,
                report.RetainedCount,
                report.BlockedCount,
                report.ManualCount,
                report.DuplicateReviewCount,
                report.AffectedSemesterIds,
                report.CanMakeGroupIdNotNull,
                report.CanAddGroupSemesterUniqueConstraint,
                report.CanRemoveLegacyWildcardSemantics,
                report.AbortReason,
                report.Notes,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return string.Equals(report.ExecutionStatus, "Aborted", StringComparison.Ordinal) ? 12 : 0;
        }

        if (mode is "--schema-hardening-readiness" or "--hardening-go-nogo")
        {
            var finAuditH = new LegacySemesterFinalizationAuditService(db, user, new LegacySemesterMigrationAuditService(db, user), planSvc);
            var svcH = new SemesterSchemaHardeningReadinessService(db, user, finAuditH);
            var report = await svcH.BuildAsync();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                report.PromptCode,
                report.IsReadOnly,
                report.NoMutationsPerformed,
                report.SaveChangesInvoked,
                report.DecisionCode,
                report.ReadinessCodes,
                report.IsReady,
                report.TenantCount,
                report.SemesterCount,
                report.NullGroupSemesterCount,
                report.DuplicateGroupSemesterCount,
                report.SemesterIntegrityErrorCount,
                report.StudentIntegrityErrorCount,
                report.AttendanceIntegrityErrorCount,
                report.SectionIntegrityErrorCount,
                report.SubjectAllocationIntegrityErrorCount,
                report.TimetableIntegrityErrorCount,
                report.TeachingGroupIntegrityErrorCount,
                report.DownstreamLegacyReferenceCount,
                report.WildcardConsumerCount,
                report.WildcardConsumerClosureStatus,
                report.ActiveWritePathViolationCount,
                report.CrossTenantViolationCount,
                report.ManualReviewCount,
                report.NotNullReady,
                report.UniqueReady,
                report.WritePathsGroupOwned,
                report.NoActiveNullGroupWritePath,
                report.ArchitectureGuardsIntact,
                report.ConstraintSimulationSummary,
                report.EvidenceSummary,
                NullGroup = report.NullGroupSemesters.Select(n => new { n.SemesterId, n.DispositionCode, n.DownstreamReferenceCount }),
                Wildcards = report.WildcardDependencies.Select(w => new { w.Path, w.KindCode, w.BlocksHardening, w.ClosureStatus }),
                Blockers = report.BlockingFindings.Select(f => new { f.Code, f.Entity, f.EntityId, f.Reason, f.RequiredRemediation }),
                report.Warnings,
                report.RecommendedNextPrompt,
                report.Notes,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return report.IsReady ? 0 : 13;
        }

        if (mode is "--legacy-final-disposition-readiness" or "--final-disposition-readiness" or "--prompt3i2-readiness")
        {
            var finAuditN = new LegacySemesterFinalizationAuditService(db, user, new LegacySemesterMigrationAuditService(db, user), planSvc);
            var schemaN = new SemesterSchemaHardeningReadinessService(db, user, finAuditN);
            var projectorN = new Abhyanvaya.Application.Scheduling.TimetableSectionProjector(db, user);
            var tg3fN = new TeachingGroupSemesterRemediationService(
                db, user, projectorN, integrity, finAuditN,
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<TeachingGroupSemesterRemediationService>());
            var tgReadyN = new TeachingGroupRemediationReadinessService(db, user, tg3fN);
            var svcN = new LegacySemesterFinalDispositionReadinessService(db, user, schemaN, finAuditN, tgReadyN);
            var report = await svcN.BuildAsync();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                report.PromptCode,
                report.IsReadOnly,
                report.NoMutationsPerformed,
                report.SaveChangesInvoked,
                report.SchemaHardeningReady,
                report.IsReady,
                report.NullGroupReady,
                report.UniqueKeyReady,
                report.StudentIntegrityReady,
                report.DownstreamReferenceReady,
                report.TeachingGroupBoundaryReady,
                report.TenantIsolationReady,
                report.WildcardDependencyReady,
                report.WritePathReady,
                report.MigrationSafetyReady,
                report.EvidenceCounts,
                Legacy = report.LegacySemesters.Select(l => new
                {
                    l.SemesterId,
                    l.Number,
                    l.CourseId,
                    l.DispositionCode,
                    l.MutationPermitted,
                    l.SubjectRefs,
                    l.DependentEntities,
                    l.Reason,
                    l.BlockingDependency,
                }),
                DuplicateKeys = report.DuplicateKeys,
                Outstanding = report.OutstandingReferences.Take(40),
                Wildcards = report.WildcardDependencies.Select(w => new { w.Path, w.KindCode, w.BlocksHardening }),
                report.BlockingReasons,
                report.Warnings,
                MigrationContract = report.NextMigrationContract is null ? null : new
                {
                    report.NextMigrationContract.AuthorizedForExecution,
                    report.NextMigrationContract.Title,
                    report.NextMigrationContract.Steps,
                    report.NextMigrationContract.Notes,
                },
                report.RecommendedNextPrompt,
                report.Notes,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return report.IsReady ? 0 : 16;
        }

        if (mode is "--legacy-historical-disposition-preview" or "--historical-disposition-preview" or "--prompt3ja-preview")
        {
            var finAuditJa = new LegacySemesterFinalizationAuditService(db, user, new LegacySemesterMigrationAuditService(db, user), planSvc);
            var histSvc = new LegacySemesterHistoricalDispositionService(
                db, user, finAuditJa,
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<LegacySemesterHistoricalDispositionService>());
            var preview = await histSvc.PreviewAsync();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                preview.PromptCode,
                preview.IsReadOnly,
                preview.NoMutationsPerformed,
                preview.SchemaHardeningReady,
                preview.Prompt3JAuthorized,
                preview.LegacyNullGroupCount,
                preview.HistoricalArchiveCount,
                preview.EligibleForHistoricalArchiveCount,
                preview.ManualMappingRequiredCount,
                preview.DuplicateReviewCount,
                preview.PendingReviewCount,
                Candidates = preview.Candidates.Select(c => new
                {
                    c.SemesterId,
                    c.Number,
                    c.RecommendedDisposition,
                    c.EligibleForHistoricalArchive,
                    c.OperationalRefTotal,
                    c.SubjectRefs,
                    c.AllowedDispositions,
                    c.Reason,
                }),
                Matrix = preview.DependencyMatrix.Select(m => new
                {
                    m.Entity,
                    m.SemesterFk,
                    m.CanReferenceArchivedSemester,
                    m.MustRemapBeforeArchival,
                }),
                preview.Notes,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        if (mode is "--historical-disposition-audit" or "--prompt3ka-audit")
        {
            var finAuditKa = new LegacySemesterFinalizationAuditService(db, user, new LegacySemesterMigrationAuditService(db, user), planSvc);
            var svcKa = new HistoricalSemesterDispositionAuditService(db, user, finAuditKa);
            var report = await svcKa.BuildAuditAsync();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                report.PromptCode,
                report.IsReadOnly,
                report.SaveChangesInvoked,
                report.ExistingArchivePatternFound,
                report.ExistingArchivePatternName,
                report.CompetingLifecycleAvoided,
                report.SchemaHardeningDeferred,
                report.TenantIsolationPassed,
                report.ActiveOperationalCount,
                report.HistoricalRetainCount,
                report.ManualMappingRequiredCount,
                report.DuplicateReviewCount,
                report.BlockedByReferenceCount,
                report.ArchiveEligibleCount,
                report.ArchivedCount,
                report.LegacyNullGroupCount,
                Items = report.Items.Select(i => new
                {
                    i.SemesterId,
                    i.SemesterNumber,
                    i.GroupId,
                    i.Classification,
                    i.IsOperational,
                    i.IsHistorical,
                    i.IsArchiveEligible,
                    i.RecommendedAction,
                    i.DownstreamReferenceSummary.OperationalRefTotal,
                }),
                report.Blockers,
                report.Warnings,
                report.RecommendedNextPrompt,
                report.Notes,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        if (mode is "--legacy-historical-disposition-execute" or "--prompt3ja-execute")
        {
            // Explicit Items JSON as second arg, e.g.:
            // --legacy-historical-disposition-execute "{\"items\":[{\"semesterId\":2,\"disposition\":\"HISTORICAL_ARCHIVE\"}]}"
            var payload = args.ElementAtOrDefault(1);
            if (string.IsNullOrWhiteSpace(payload))
            {
                Console.Error.WriteLine("ABORT: explicit disposition JSON payload required (no archive-all).");
                return 17;
            }

            var request = JsonSerializer.Deserialize<LegacySemesterHistoricalDispositionExecuteRequest>(
                payload,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (request is null || request.Items.Count == 0)
            {
                Console.Error.WriteLine("ABORT: Items required.");
                return 17;
            }

            var finAuditJa = new LegacySemesterFinalizationAuditService(db, user, new LegacySemesterMigrationAuditService(db, user), planSvc);
            var histSvc = new LegacySemesterHistoricalDispositionService(
                db, user, finAuditJa,
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<LegacySemesterHistoricalDispositionService>());
            var histResult = await histSvc.ExecuteAsync(request);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                histResult.IsSuccessful,
                histResult.ExecutionStatus,
                histResult.ChangedCount,
                histResult.AlreadyCompleteCount,
                histResult.ManualReviewCount,
                histResult.DuplicateReviewCount,
                histResult.BlockedCount,
                histResult.RolledBack,
                histResult.SchemaHardeningReady,
                histResult.Prompt3JAuthorized,
                histResult.AbortReason,
                histResult.Findings,
                histResult.PostDispositionIntegrity,
                histResult.Notes,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return histResult.IsSuccessful || string.Equals(histResult.ExecutionStatus, "AlreadyComplete", StringComparison.Ordinal)
                ? 0
                : 18;
        }

        if (mode is "--historical-disposition-execute" or "--prompt3kb-execute")
        {
            // Prompt 3K-B: "{\"disposition\":\"HISTORICAL_ARCHIVE\",\"semesterIds\":[2,3],\"reason\":\"...\"}"
            var payloadKb = args.ElementAtOrDefault(1);
            if (string.IsNullOrWhiteSpace(payloadKb))
            {
                Console.Error.WriteLine("ABORT: explicit 3K-B JSON required (disposition + semesterIds; no archive-all).");
                return 17;
            }

            var requestKb = JsonSerializer.Deserialize<HistoricalSemesterDispositionExecuteRequest>(
                payloadKb,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (requestKb is null || requestKb.SemesterIds is null || requestKb.SemesterIds.Count == 0)
            {
                Console.Error.WriteLine("ABORT: semesterIds required.");
                return 17;
            }

            var finAuditKb = new LegacySemesterFinalizationAuditService(db, user, new LegacySemesterMigrationAuditService(db, user), planSvc);
            var auditKb = new HistoricalSemesterDispositionAuditService(db, user, finAuditKb);
            var execKb = new HistoricalSemesterDispositionExecutionService(
                db, user, auditKb,
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<HistoricalSemesterDispositionExecutionService>());
            var kbResult = await execKb.ExecuteAsync(requestKb);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                kbResult.PromptCode,
                kbResult.Disposition,
                kbResult.IsSuccessful,
                kbResult.ExecutionStatus,
                kbResult.Requested,
                kbResult.Archived,
                kbResult.AlreadyComplete,
                kbResult.Rejected,
                kbResult.Blocked,
                kbResult.RolledBack,
                kbResult.TransactionCommitted,
                kbResult.TransactionModel,
                kbResult.AbortReason,
                kbResult.GroupIdInvented,
                kbResult.DownstreamEntitiesMutated,
                kbResult.SchemaHardeningDeferred,
                Results = kbResult.Results.Select(r => new
                {
                    r.SemesterId,
                    r.Result,
                    r.Classification,
                    r.GroupIdBefore,
                    r.GroupIdAfter,
                    r.IsHistoricalArchiveAfter,
                    r.SemesterRowMutated,
                    r.JournalWritten,
                    r.Reason,
                }),
                kbResult.Notes,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return kbResult.IsSuccessful
                   || string.Equals(kbResult.ExecutionStatus, "AlreadyComplete", StringComparison.Ordinal)
                ? 0
                : 18;
        }

        if (mode is "--section-remediate-execute")
        {
            var sectionExec = new SectionSemesterRemediationService(
                db, user,
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<SectionSemesterRemediationService>());
            var report = await sectionExec.ExecuteAsync();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                report.ExecutionStatus,
                report.RolledBack,
                report.TransactionCommitted,
                report.ChangedCount,
                report.AlreadyCompleteCount,
                report.EligibleCount,
                report.BlockedCount,
                report.ManualReviewCount,
                report.ApprovedSectionIds,
                report.AffectedSectionIds,
                report.TeachingGroupsUnchanged,
                report.TeachingGroupSectionsUnchanged,
                report.AbortReason,
                report.ConcurrencyResult,
                Items = report.Items.Select(i => new
                {
                    i.SectionId,
                    i.CurrentSemesterId,
                    i.TargetSemesterId,
                    i.StatusCode,
                    i.Reason,
                }),
                report.Notes,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return string.Equals(report.ExecutionStatus, "Aborted", StringComparison.Ordinal) ? 12 : 0;
        }

        if (mode is "--tg-remediate-execute")
        {
            var finAuditExec = new LegacySemesterFinalizationAuditService(db, user, new LegacySemesterMigrationAuditService(db, user), planSvc);
            var projectorExec = new Abhyanvaya.Application.Scheduling.TimetableSectionProjector(db, user);
            var svcExec = new TeachingGroupSemesterRemediationService(
                db, user, projectorExec, integrity, finAuditExec,
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<TeachingGroupSemesterRemediationService>());
            var report = await svcExec.ExecuteAsync();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                report.ExecutionStatus,
                report.RolledBack,
                report.TransactionCommitted,
                report.ChangedCount,
                report.AlreadyCompleteCount,
                report.ManualReviewCount,
                report.BlockedCount,
                report.AffectedTeachingGroupIds,
                report.OldSemesterIds,
                report.NewSemesterIds,
                report.AbortReason,
                report.ConcurrencyResult,
                Items = report.Items.Select(i => new
                {
                    i.TeachingGroupId,
                    i.CurrentSemesterId,
                    i.TargetSemesterId,
                    i.StatusCode,
                    i.Reason,
                }),
                PostTgResiduals = report.PostFinalizationAudit?.Summary.TeachingGroupResidualCount,
                PostHealthy = report.PostIntegrityAudit?.IsHealthy,
                report.Notes,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return string.Equals(report.ExecutionStatus, "Aborted", StringComparison.Ordinal) ? 12 : 0;
        }

        if (mode is "--finalization" or "--finalization-audit")
        {
            var fin = new LegacySemesterFinalizationAuditService(db, user, new LegacySemesterMigrationAuditService(db, user), planSvc);
            var report = await fin.BuildAuditAsync();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                report.IsReadOnly,
                report.NoMutationsPerformed,
                report.Summary,
                Legacy = report.LegacySemesters.Select(l => new
                {
                    l.SemesterId,
                    l.Number,
                    l.Name,
                    l.DispositionCode,
                    l.StudentReferenceCount,
                    l.AttendanceReferenceCount,
                    l.SubjectAllocationReferenceCount,
                    l.TimetableEntryReferenceCount,
                    l.TeachingGroupReferenceCount,
                    l.SubjectReferenceCount,
                    l.SectionReferenceCount,
                    l.Prompt3ADecision,
                }),
                Tg = report.TeachingGroupResiduals.Select(t => new
                {
                    t.TeachingGroupId,
                    t.Code,
                    t.GroupId,
                    t.LegacySemesterId,
                    t.CandidateTargetSemesterId,
                    t.RecommendationCode,
                    t.TeachingGroupSectionCount,
                    t.TimetableEntryCountUsingTg,
                }),
                report.DuplicateGroupSemesterNumbers,
                StudentIntegrity = report.StudentIntegrity,
                Downstream = report.DownstreamLegacyReferences,
                Hardening = new
                {
                    report.HardeningPreconditions.NotNullMayProceed,
                    report.HardeningPreconditions.UniqueMayProceed,
                    report.HardeningPreconditions.BlockingReasons,
                },
                WildcardCount = report.NullWildcardDependencies.Count,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        if (mode is "--finalization-preview")
        {
            var exec = new LegacySemesterFinalizationExecutionService(
                db, user,
                new LegacySemesterFinalizationAuditService(db, user, new LegacySemesterMigrationAuditService(db, user), planSvc),
                planSvc,
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<LegacySemesterFinalizationExecutionService>());
            var report = await exec.PreviewAsync();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                report.ExecutionStatus,
                report.IsReadOnly,
                report.RetainedCount,
                report.ChangedCount,
                report.BlockedCount,
                report.ManualReviewCount,
                report.DeferredTeachingGroupCount,
                report.AlreadyCompleteCount,
                Items = report.Items.Select(i => new
                {
                    i.SemesterId,
                    i.DispositionCode,
                    i.Action,
                    i.BlockingReason,
                    i.TeachingGroupIds,
                    i.CandidateTargetSemesterIdForTg,
                }),
                report.BlockingReasons,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        if (mode is "--finalization-execute")
        {
            var exec = new LegacySemesterFinalizationExecutionService(
                db, user,
                new LegacySemesterFinalizationAuditService(db, user, new LegacySemesterMigrationAuditService(db, user), planSvc),
                planSvc,
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<LegacySemesterFinalizationExecutionService>());
            var report = await exec.ExecuteAsync();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                report.ExecutionStatus,
                report.RolledBack,
                report.AbortReason,
                report.RetainedCount,
                report.ChangedCount,
                report.AlreadyCompleteCount,
                report.BlockedCount,
                report.ManualReviewCount,
                report.DeferredTeachingGroupCount,
                report.FinalizationTimestamp,
                report.AffectedSemesterIds,
                Items = report.Items.Select(i => new
                {
                    i.SemesterId,
                    i.DispositionCode,
                    i.Action,
                    i.JournalWritten,
                    i.SemesterRowMutated,
                    i.BlockingReason,
                }),
                report.BlockingReasons,
                report.Notes,
                SchemaHardeningReady = report.SchemaHardeningReady,
                Post = report.PostFinalizationAudit is null ? null : new
                {
                    report.PostFinalizationAudit.Summary,
                    Hardening = new
                    {
                        report.PostFinalizationAudit.HardeningPreconditions.NotNullMayProceed,
                        report.PostFinalizationAudit.HardeningPreconditions.UniqueMayProceed,
                        report.PostFinalizationAudit.HardeningPreconditions.BlockingReasons,
                    },
                },
            }, new JsonSerializerOptions { WriteIndented = true }));
            return string.Equals(report.ExecutionStatus, "Aborted", StringComparison.Ordinal) ? 12 : 0;
        }

        if (mode == "--remediate-execute")
        {
            var report = await remediate.ExecuteAsync();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                report.ExecutionStatus,
                report.RolledBack,
                report.AbortReason,
                report.LegacySemesterId,
                report.Summary,
                report.Notes,
                Post = report.PostIntegrityAudit is null ? null : new
                {
                    report.PostIntegrityAudit.IsHealthy,
                    report.PostIntegrityAudit.Summary,
                    WarningCodes = report.PostIntegrityAudit.Violations
                        .Where(v => v.Severity == IntegritySeverity.Warning)
                        .GroupBy(v => v.Code)
                        .Select(g => new { Code = g.Key, Count = g.Count() }),
                },
            }, new JsonSerializerOptions { WriteIndented = true }));
            return string.Equals(report.ExecutionStatus, "Aborted", StringComparison.Ordinal) ? 12 : 0;
        }

        if (mode is "--preproduction-cleanup-preview" or "--prompt3hc1-preview")
        {
            var resetPreview = new PreProductionTransactionalResetService(
                db, user,
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<PreProductionTransactionalResetService>());
            var preview = await resetPreview.PreviewAsync();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                preview.PromptCode,
                preview.IsReadOnly,
                preview.IsCleanupReady,
                preview.AbortReason,
                preview.TransactionalTotal,
                preview.StudentsUpdateRequired,
                preview.StudentsAlreadyCorrect,
                preview.StudentsFailClosed,
                preview.ProtectedBefore,
                Allowlist = preview.DeletionAllowlistCounts.Where(c => c.Count > 0),
                FailClosedStudents = preview.StudentReconciliation
                    .Where(r => r.ResolutionStatus is not "ALREADY_CORRECT" and not "UPDATE_REQUIRED")
                    .Select(r => new { r.StudentId, r.ResolutionStatus, r.Evidence }),
                preview.Notes,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return preview.IsCleanupReady ? 0 : 19;
        }

        if (mode is "--preproduction-cleanup-execute" or "--prompt3hc1-execute")
        {
            var resetExec = new PreProductionTransactionalResetService(
                db, user,
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<PreProductionTransactionalResetService>());
            var execResult = await resetExec.ExecuteAsync(new PreProductionTransactionalResetExecuteRequest
            {
                Confirm = true,
                ConfirmationPhrase = PreProductionTransactionalResetCodes.ConfirmationPhrase,
                Reason = args.ElementAtOrDefault(1) ?? "runner-3hc1",
            });
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                execResult.PromptCode,
                execResult.IsSuccessful,
                execResult.ExecutionStatus,
                execResult.RolledBack,
                execResult.TotalDeleted,
                execResult.StudentsUpdated,
                execResult.IdempotentZeroMutation,
                execResult.PostIntegrityPassed,
                execResult.ProtectedBefore,
                execResult.ProtectedAfter,
                execResult.AbortReason,
                Deleted = execResult.DeletedCounts.Where(c => c.Count > 0),
                execResult.Notes,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return execResult.IsSuccessful
                   || string.Equals(execResult.ExecutionStatus, "AlreadyComplete", StringComparison.Ordinal)
                ? 0
                : 20;
        }

        if (mode == "--integrity")
        {
            var report = await integrity.BuildAuditAsync();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                report.IsHealthy,
                report.Summary,
                Checks = report.Checks.Select(c => new { c.Code, c.Result, c.ViolationCount }),
                report.SemesterIiiSplit,
                Legacy = report.LegacySemesters.Select(l => new { l.SemesterId, l.Classification, l.StudentCount, l.DownstreamReferenceTotal }),
                Critical = report.Violations.Where(v => v.Severity == IntegritySeverity.Critical).Select(v => new { v.Code, v.Message }),
                Errors = report.Violations.Where(v => v.Severity == IntegritySeverity.Error).Select(v => new { v.Code, v.Message }),
                WarningCodes = report.Violations.Where(v => v.Severity == IntegritySeverity.Warning).GroupBy(v => v.Code).Select(g => new { Code = g.Key, Count = g.Count() }),
            }, new JsonSerializerOptions { WriteIndented = true }));
            return report.Summary.Critical == 0 && report.Summary.Errors == 0 ? 0 : 11;
        }

        var plan = await planSvc.BuildDecisionPlanAsync();

        Console.WriteLine($"MatchesPrompt2BBaseline={plan.MatchesPrompt2BBaseline}");
        Console.WriteLine($"DecisionCount={plan.Decisions.Count}");
        foreach (var d in plan.Decisions)
        {
            var counts = string.Join(",", d.StudentCountsByTargetGroup.Select(kv => $"{kv.Key}:{kv.Value}"));
            Console.WriteLine(
                $"  SemId={d.SemesterId} Number={d.Number} GroupId={(d.CurrentGroupId?.ToString() ?? "NULL")} Decision={d.DecisionCode} StudentsByGroup=[{counts}] MustNotModify={d.MustNotModify}");
        }

        var split = plan.Decisions.SingleOrDefault(d =>
            d.Decision == LegacySemesterMigrationDecision.Split && d.Number == 3 && d.CurrentGroupId is null);

        var finance = split?.StudentCountsByTargetGroup.SingleOrDefault(kv => kv.Value == 60) ?? default;
        var ca = split?.StudentCountsByTargetGroup.SingleOrDefault(kv => kv.Value == 236) ?? default;
        if (split is not null)
            Console.WriteLine($"Resolved FinanceGroupId={finance.Key} CAGroupId={ca.Key} SourceSemesterId={split.SemesterId}");

        if (mode == "--preflight")
        {
            if (split is null)
            {
                Console.WriteLine("PREFLIGHT_NOTE: No SPLIT row (may already be migrated). Runner will still allow --execute for AlreadyCompleted.");
                return 0;
            }

            if (!plan.MatchesPrompt2BBaseline)
            {
                Console.Error.WriteLine("ABORT: Prompt 3A baseline mismatch.");
                return 4;
            }

            Console.WriteLine("PREFLIGHT_OK");
            return 0;
        }

        if (mode != "--execute")
        {
            Console.Error.WriteLine("Usage: --preflight | --execute | --integrity | --remediate-* | --finalization* | --tg-remediate-* | --teaching-group-remediation-readiness | --section-remediate-* | --section-semester-remediation-audit | --finance-section-remediate-* | --subject-catalog-remediate-* | --prompt3h-audit | --wildcard-retire-preview | --wildcard-retire-execute | --legacy-wildcard-retirement-readiness | --schema-hardening-readiness | --legacy-final-disposition-readiness | --legacy-historical-disposition-preview | --legacy-historical-disposition-execute | --historical-disposition-audit | --historical-disposition-execute | --prompt3kb-execute | --preproduction-cleanup-preview | --preproduction-cleanup-execute | --prompt3hc1-preview | --prompt3hc1-execute");
            return 5;
        }

        if (split is not null && !plan.MatchesPrompt2BBaseline)
        {
            Console.Error.WriteLine("ABORT: Prompt 3A baseline mismatch with pending SPLIT.");
            return 4;
        }

        var mig = new SemesterIiiSplitStudentRemapMigrationService(
            db,
            user,
            planSvc,
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<SemesterIiiSplitStudentRemapMigrationService>());

        var result = await mig.ExecuteAsync();
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        return string.Equals(result.Status, "Completed", StringComparison.Ordinal)
               || string.Equals(result.Status, "AlreadyCompleted", StringComparison.Ordinal)
            ? 0
            : 10;
    }

    private sealed class FixedUser : ICurrentUserService
    {
        public int UserId { get; set; }
        public string Role { get; set; } = "";
        public int TenantId { get; set; }
        public int StaffId { get; set; }
        public int CourseId { get; set; }
        public int GroupId { get; set; }
    }
}
