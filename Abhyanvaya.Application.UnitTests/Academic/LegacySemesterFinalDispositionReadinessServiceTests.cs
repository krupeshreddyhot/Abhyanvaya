using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Moq;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

public sealed class LegacySemesterFinalDispositionReadinessServiceTests
{
    private sealed class AmbientUser : ICurrentUserService
    {
        public int UserId { get; set; } = 1;
        public string Role { get; set; } = "Admin";
        public int TenantId { get; set; } = 1;
        public int StaffId { get; set; }
        public int CourseId { get; set; }
        public int GroupId { get; set; }
    }

    private static LegacySemesterFinalDispositionReadinessService CreateSut(
        SemesterSchemaHardeningReadinessResult schema,
        LegacySemesterFinalizationAuditDto? fin = null,
        TeachingGroupRemediationReadinessResultDto? tg = null)
    {
        var user = new AmbientUser();
        var db = new Mock<IApplicationDbContext>();
        var schemaSvc = new Mock<ISemesterSchemaHardeningReadinessService>();
        schemaSvc.Setup(s => s.BuildAsync(It.IsAny<CancellationToken>())).ReturnsAsync(schema);
        var finSvc = new Mock<ILegacySemesterFinalizationAuditService>();
        finSvc.Setup(f => f.BuildAuditAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(fin ?? new LegacySemesterFinalizationAuditDto { IsReadOnly = true, LegacySemesters = [] });
        var tgSvc = new Mock<ITeachingGroupRemediationReadinessService>();
        tgSvc.Setup(t => t.BuildAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tg ?? new TeachingGroupRemediationReadinessResultDto
            {
                IsHealthy = true,
                TenantIsolationOk = true,
                ApprovedTeachingGroupIds = [1, 2],
                AlreadyCompleteTeachingGroupIds = [1, 2],
                TeachingGroupLegacyReferenceCount = 0,
            });
        return new LegacySemesterFinalDispositionReadinessService(
            db.Object, user, schemaSvc.Object, finSvc.Object, tgSvc.Object);
    }

    private static SemesterSchemaHardeningReadinessResult BaseSchema(bool ready = false)
        => new()
        {
            IsReadOnly = true,
            NoMutationsPerformed = true,
            SaveChangesInvoked = false,
            IsReady = ready,
            Decision = ready ? SemesterSchemaHardeningDecision.Go : SemesterSchemaHardeningDecision.NoGo,
            DecisionCode = ready ? "GO" : "NO_GO",
            SemesterCount = ready ? 2 : 8,
            NullGroupSemesterCount = ready ? 0 : 5,
            DuplicateKeyCount = 0,
            UniqueReady = ready,
            NotNullReady = ready,
            StudentIntegrityViolationCount = 0,
            DownstreamLegacyReferenceCount = ready ? 0 : 1,
            SchedulingIntegrityViolationCount = 0,
            TeachingGroupBlockingCount = 0,
            SectionBlockingCount = 0,
            CrossTenantViolationCount = 0,
            WildcardProductionDependencyCount = 0,
            WritePathsGroupOwned = true,
            NoActiveNullGroupWritePath = true,
            ArchitectureGuardsIntact = true,
            NullGroupSemesters = ready
                ? []
                :
                [
                    new NullGroupSemesterAuditRowDto
                    {
                        TenantId = 1, SemesterId = 1, Number = 1, Name = "I", CourseId = 1,
                        Disposition = NullGroupSemesterDisposition.OtherExplicitApprovedState,
                        DispositionCode = "OTHER_EXPLICIT_APPROVED_STATE",
                        Evidence = "Subject historical",
                        DownstreamReferenceCount = 1,
                    },
                    new NullGroupSemesterAuditRowDto
                    {
                        TenantId = 1, SemesterId = 2, Number = 2, Name = "II", CourseId = 1,
                        Disposition = NullGroupSemesterDisposition.RetainHistorical,
                        DispositionCode = "RETAIN_HISTORICAL",
                        Evidence = "retain",
                    },
                ],
            DownstreamLegacyReferences = ready
                ? []
                :
                [
                    new DownstreamLegacyReferenceRowDto
                    {
                        TenantId = 1, SemesterId = 1, SemesterNumber = 1, CourseId = 1,
                        ReferenceEntity = "Subject", ReferenceCount = 1, ReferenceIds = ["10"],
                        Disposition = "BLOCKED", BlockingReason = "Subject refs Sem 1",
                    },
                ],
            WildcardDependencies =
            [
                new WildcardDependencyAuditRowDto
                {
                    Path = "AcademicTreeService", Kind = WildcardDependencyKind.DeadUnreachable,
                    KindCode = "DEAD_UNREACHABLE", BlocksHardening = false,
                },
            ],
            StudentIntegrity = new StudentIntegrityAuditSummaryDto { TotalAudited = 10, Valid = 10 },
            DuplicateKeys = [],
            Warnings = ["Soft-deleted scan required."],
        };

    [Fact]
    public async Task Readiness_False_When_Null_Group_Rows_Remain()
    {
        var svc = CreateSut(BaseSchema(ready: false), new LegacySemesterFinalizationAuditDto
        {
            LegacySemesters =
            [
                new LegacySemesterInventoryRowDto
                {
                    SemesterId = 1, CourseId = 1, Number = 1,
                    Disposition = LegacySemesterFinalizationDisposition.HistoricalRetain,
                    DispositionCode = "HISTORICAL_RETAIN",
                    DispositionEvidence = "retain",
                    SubjectReferenceCount = 1,
                },
                new LegacySemesterInventoryRowDto
                {
                    SemesterId = 2, CourseId = 1, Number = 2,
                    Disposition = LegacySemesterFinalizationDisposition.HistoricalRetain,
                    DispositionCode = "HISTORICAL_RETAIN",
                    DispositionEvidence = "retain",
                },
            ],
        });

        var report = await svc.BuildAsync();
        Assert.False(report.IsReady);
        Assert.False(report.SchemaHardeningReady);
        Assert.False(report.NullGroupReady);
        Assert.True(report.WritePathReady);
        Assert.True(report.WildcardDependencyReady);
        Assert.True(report.IsReadOnly);
        Assert.False(report.SaveChangesInvoked);
        Assert.Contains(report.LegacySemesters, r => r.SemesterId == 1);
        Assert.False(report.NextMigrationContract!.AuthorizedForExecution);
    }

    [Fact]
    public async Task Readiness_True_Only_When_All_Flags_Pass()
    {
        var svc = CreateSut(BaseSchema(ready: true));
        var report = await svc.BuildAsync();
        Assert.True(report.IsReady);
        Assert.True(report.SchemaHardeningReady);
        Assert.True(report.NullGroupReady);
        Assert.True(report.UniqueKeyReady);
        Assert.True(report.StudentIntegrityReady);
        Assert.True(report.DownstreamReferenceReady);
        Assert.True(report.TeachingGroupBoundaryReady);
        Assert.True(report.TenantIsolationReady);
        Assert.True(report.WildcardDependencyReady);
        Assert.True(report.WritePathReady);
        Assert.True(report.MigrationSafetyReady);
        Assert.True(report.NextMigrationContract!.AuthorizedForExecution);
        Assert.Contains(report.NextMigrationContract.Steps, s => s.Contains("ALTER", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Duplicate_Review_Mapped()
    {
        var schema = BaseSchema(false);
        schema = new SemesterSchemaHardeningReadinessResult
        {
            IsReadOnly = true,
            NullGroupSemesterCount = 1,
            SemesterCount = 3,
            UniqueReady = false,
            WritePathsGroupOwned = true,
            NoActiveNullGroupWritePath = true,
            ArchitectureGuardsIntact = true,
            NullGroupSemesters =
            [
                new NullGroupSemesterAuditRowDto
                {
                    TenantId = 1, SemesterId = 4, Number = 4, Name = "IV", CourseId = 1,
                    DispositionCode = "OTHER_EXPLICIT_APPROVED_STATE",
                    Evidence = "DUPLICATE_REVIEW: dup",
                },
            ],
            DownstreamLegacyReferences = [],
            WildcardDependencies = [],
            DuplicateKeys = [],
            StudentIntegrity = new StudentIntegrityAuditSummaryDto(),
            Warnings = [],
        };
        var svc = CreateSut(schema, new LegacySemesterFinalizationAuditDto
        {
            LegacySemesters =
            [
                new LegacySemesterInventoryRowDto
                {
                    SemesterId = 4, CourseId = 1, Number = 4,
                    Disposition = LegacySemesterFinalizationDisposition.DuplicateReview,
                    DispositionCode = "DUPLICATE_REVIEW",
                    DispositionEvidence = "dup",
                },
            ],
        });

        var report = await svc.BuildAsync();
        var row = Assert.Single(report.LegacySemesters);
        Assert.Equal(FinalLegacySemesterDisposition.DuplicateReview, row.Disposition);
        Assert.Equal("DUPLICATE_REVIEW", row.DispositionCode);
        Assert.False(row.MutationPermitted);
    }

    [Fact]
    public async Task Idempotent_Repeated_Audit()
    {
        var svc = CreateSut(BaseSchema(false));
        var a = await svc.BuildAsync();
        var b = await svc.BuildAsync();
        Assert.Equal(a.IsReady, b.IsReady);
        Assert.Equal(a.NullGroupReady, b.NullGroupReady);
        Assert.Equal(a.EvidenceCounts.NullGroupSemesters, b.EvidenceCounts.NullGroupSemesters);
    }
}

public sealed class AiSchedCatalogTimetableP14Prompt3I2FinalDispositionArchitectureGuardTests
{
    [Fact]
    public void Service_Is_ReadOnly_Contract()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "LegacySemesterFinalDispositionReadinessService.cs"));
        Assert.Contains("P1-4-3N", src, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChangesAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChanges(", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteInTransactionAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AlterColumn", src, StringComparison.Ordinal);
        Assert.DoesNotContain("IsRequired()", src, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"tg\.SemesterId\s*=(?!=)", src);
        Assert.DoesNotContain("ConflictEngine", src, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishAsync", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_And_Documentation_Exist()
    {
        var root = FindRepoRoot();
        var api = File.ReadAllText(Path.Combine(root, "Abhyanvaya.API", "Controllers", "SemesterController.cs"));
        Assert.Contains("legacy-final-disposition-schema-hardening-readiness", api, StringComparison.Ordinal);
        Assert.Contains("ILegacySemesterFinalDispositionReadinessService", api, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(
            root, "docs",
            "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3I_LEGACY_SEMESTER_FINAL_DISPOSITION_AND_SCHEMA_HARDENING_READINESS.md")));
    }

    [Fact]
    public void Finance_PromptCode_3I_Unchanged()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "FinanceSectionSemesterRemediationService.cs"));
        Assert.Contains("PromptCode = \"P1-4-3I\"", src, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Abhyanvaya.sln"))
                || Directory.Exists(Path.Combine(dir.FullName, "Abhyanvaya.Domain")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found.");
    }
}
