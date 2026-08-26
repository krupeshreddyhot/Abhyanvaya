using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

public sealed class HistoricalSemesterDispositionAuditServiceTests
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

    private static (ApplicationDbContext Db, HistoricalSemesterDispositionAuditService Svc) CreateSut(
        LegacySemesterFinalizationAuditDto? fin = null)
    {
        var user = new AmbientUser();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("p143ka-" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);

        var finalization = new Mock<ILegacySemesterFinalizationAuditService>();
        finalization.Setup(f => f.BuildAuditAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(fin ?? new LegacySemesterFinalizationAuditDto
            {
                IsReadOnly = true,
                Summary = new LegacySemesterFinalizationSummaryDto { LegacyNullGroupCount = 0 },
                LegacySemesters = [],
            });

        var svc = new HistoricalSemesterDispositionAuditService(db, user, finalization.Object);
        return (db, svc);
    }

    [Fact]
    public async Task Audit_Is_ReadOnly_And_Does_Not_Mutate()
    {
        var (db, svc) = CreateSut();
        db.Set<Course>().Add(new Course
        {
            Id = 1, TenantId = 1, Code = "BCOM", Name = "B.Com", DepartmentId = 1, CreatedDate = DateTime.UtcNow,
        });
        db.Set<Group>().Add(new Group
        {
            Id = 2, TenantId = 1, CourseId = 1, Code = "05", Name = "CA", CreatedDate = DateTime.UtcNow,
        });
        db.Set<Semester>().Add(new Semester
        {
            Id = 11, TenantId = 1, CourseId = 1, Number = 3, Name = "III", GroupId = 2, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var before = await db.Set<Semester>().AsNoTracking().Select(s => new { s.Id, s.GroupId, s.IsHistoricalArchive }).ToListAsync();
        var a = await svc.BuildAuditAsync();
        var b = await svc.BuildAuditAsync();

        Assert.True(a.IsReadOnly);
        Assert.False(a.SaveChangesInvoked);
        Assert.Equal("P1-4-3KA", a.PromptCode);
        Assert.Equal(a.ActiveOperationalCount, b.ActiveOperationalCount);
        Assert.Equal(before, await db.Set<Semester>().AsNoTracking().Select(s => new { s.Id, s.GroupId, s.IsHistoricalArchive }).ToListAsync());
        Assert.Contains(a.Items, i => i.Classification == HistoricalSemesterDispositionClassifications.ActiveOperational);
    }

    [Fact]
    public async Task Ops_Refs_Block_Archive_Eligibility()
    {
        var (db, svc) = CreateSut(new LegacySemesterFinalizationAuditDto
        {
            Summary = new LegacySemesterFinalizationSummaryDto { LegacyNullGroupCount = 1 },
            LegacySemesters =
            [
                new LegacySemesterInventoryRowDto
                {
                    SemesterId = 2,
                    CourseId = 1,
                    CourseName = "B.Com",
                    Number = 2,
                    Name = "II",
                    Disposition = LegacySemesterFinalizationDisposition.HistoricalRetain,
                    DispositionCode = "HISTORICAL_RETAIN",
                    DispositionEvidence = "test",
                    StudentReferenceCount = 1,
                },
            ],
        });
        db.Set<Semester>().Add(new Semester
        {
            Id = 2, TenantId = 1, CourseId = 1, Number = 2, Name = "II", GroupId = null, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var report = await svc.BuildAuditAsync();
        var row = Assert.Single(report.Items, i => i.SemesterId == 2);
        Assert.Equal(HistoricalSemesterDispositionClassifications.BlockedByReference, row.Classification);
        Assert.False(row.IsArchiveEligible);
    }

    [Fact]
    public async Task Manual_And_Duplicate_Are_Not_Silently_Converted()
    {
        var (db, svc) = CreateSut(new LegacySemesterFinalizationAuditDto
        {
            Summary = new LegacySemesterFinalizationSummaryDto { LegacyNullGroupCount = 2 },
            LegacySemesters =
            [
                new LegacySemesterInventoryRowDto
                {
                    SemesterId = 1, CourseId = 1, Number = 1, Name = "I",
                    Disposition = LegacySemesterFinalizationDisposition.ManualMappingRequired,
                    DispositionCode = "MANUAL_MAPPING_REQUIRED",
                    DispositionEvidence = "Subject historical",
                    SubjectReferenceCount = 1,
                },
                new LegacySemesterInventoryRowDto
                {
                    SemesterId = 4, CourseId = 1, Number = 4, Name = "IV",
                    Disposition = LegacySemesterFinalizationDisposition.DuplicateReview,
                    DispositionCode = "DUPLICATE_REVIEW",
                    DispositionEvidence = "dup",
                },
            ],
        });
        db.Set<Semester>().AddRange(
            new Semester { Id = 1, TenantId = 1, CourseId = 1, Number = 1, Name = "I", GroupId = null, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 4, TenantId = 1, CourseId = 1, Number = 4, Name = "IV", GroupId = null, CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var report = await svc.BuildAuditAsync();
        Assert.Contains(report.Items, i =>
            i.SemesterId == 1 && i.Classification == HistoricalSemesterDispositionClassifications.ManualMappingRequired);
        Assert.Contains(report.Items, i =>
            i.SemesterId == 4 && i.Classification == HistoricalSemesterDispositionClassifications.DuplicateReview);
        Assert.All(report.Items.Where(i => i.SemesterId is 1 or 4), i => Assert.Null(i.GroupId));
        Assert.False(report.Items.Any(i => i.SemesterId is 1 or 4 && i.IsArchiveEligible));
    }

    [Fact]
    public async Task Zero_Ops_Without_Manual_Is_Archive_Eligible()
    {
        var (db, svc) = CreateSut(new LegacySemesterFinalizationAuditDto
        {
            Summary = new LegacySemesterFinalizationSummaryDto { LegacyNullGroupCount = 1 },
            LegacySemesters =
            [
                new LegacySemesterInventoryRowDto
                {
                    SemesterId = 2, CourseId = 1, Number = 2, Name = "II",
                    Disposition = LegacySemesterFinalizationDisposition.HistoricalRetain,
                    DispositionCode = "HISTORICAL_RETAIN",
                    DispositionEvidence = "cleared",
                },
            ],
        });
        db.Set<Semester>().Add(new Semester
        {
            Id = 2, TenantId = 1, CourseId = 1, Number = 2, Name = "II", GroupId = null, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var report = await svc.BuildAuditAsync();
        var row = Assert.Single(report.Items, i => i.SemesterId == 2);
        Assert.Equal(HistoricalSemesterDispositionClassifications.ArchiveEligible, row.Classification);
        Assert.True(row.IsArchiveEligible);
        Assert.True(row.IsHistorical);
        Assert.False(row.IsOperational);
    }

    [Fact]
    public async Task Archived_Rows_Remain_Queryable_Classification()
    {
        var (db, svc) = CreateSut(new LegacySemesterFinalizationAuditDto
        {
            Summary = new LegacySemesterFinalizationSummaryDto { LegacyNullGroupCount = 0 },
            LegacySemesters =
            [
                new LegacySemesterInventoryRowDto
                {
                    SemesterId = 2, CourseId = 1, Number = 2, Name = "II",
                    Disposition = LegacySemesterFinalizationDisposition.HistoricalRetain,
                    DispositionCode = "HISTORICAL_RETAIN",
                    DispositionEvidence = "archived",
                },
            ],
        });
        db.Set<Course>().Add(new Course
        {
            Id = 1, TenantId = 1, Code = "BCOM", Name = "B.Com", DepartmentId = 1, CreatedDate = DateTime.UtcNow,
        });
        db.Set<Semester>().Add(new Semester
        {
            Id = 2, TenantId = 1, CourseId = 1, Number = 2, Name = "II", GroupId = null,
            IsHistoricalArchive = true, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var report = await svc.BuildAuditAsync();
        Assert.Contains(report.Items, i =>
            i.SemesterId == 2 && i.Classification == HistoricalSemesterDispositionClassifications.Archived);
        Assert.True(report.ExistingArchivePatternFound);
        Assert.True(report.CompetingLifecycleAvoided);
        Assert.True(report.SchemaHardeningDeferred);
    }
}

public sealed class AiSchedCatalogTimetableP14Prompt3KAHistoricalDispositionDiscoveryGuardTests
{
    [Fact]
    public void Service_Is_ReadOnly_Contract()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "HistoricalSemesterDispositionAuditService.cs"));
        Assert.Contains("P1-4-3KA", src, StringComparison.Ordinal);
        Assert.Contains("AsNoTracking", src, StringComparison.Ordinal);
        Assert.Contains("DISCOVERY + ARCHITECTURE CONTRACT ONLY", src, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChangesAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChanges(", src, StringComparison.Ordinal);
        Assert.DoesNotContain("_db.ExecuteInTransactionAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("semester.IsHistoricalArchive =", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tg.SemesterId =", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AlterColumn", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AssignedGroupId", src, StringComparison.Ordinal);
        Assert.DoesNotContain("using Abhyanvaya.Application.Scheduling.Capacity", src, StringComparison.Ordinal);
        Assert.Contains("IsHistoricalArchive", src, StringComparison.Ordinal);
        Assert.Contains("OperationalSemesterRules", File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "OperationalSemesterRules.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void Api_And_Documentation_Exist_For_Discovery_Get()
    {
        var root = FindRepoRoot();
        var api = File.ReadAllText(Path.Combine(root, "Abhyanvaya.API", "Controllers", "SemesterController.cs"));
        Assert.Contains("[HttpGet(\"historical-disposition-audit\")]", api, StringComparison.Ordinal);
        Assert.Contains("IHistoricalSemesterDispositionAuditService", api, StringComparison.Ordinal);
        // Prompt 3K-A discovery remains GET-only in its service; POST execute is Prompt 3K-B (P1-4-3KB).
        Assert.Contains("IHistoricalSemesterDispositionExecutionService", api, StringComparison.Ordinal);
        Assert.Contains("[HttpPost(\"historical-disposition/execute\")]", api, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(
            root, "docs", "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3K_A_HISTORICAL_SEMESTER_DISPOSITION_DISCOVERY.md")));
    }

    [Fact]
    public void Existing_P1_3_P1_4_Tg_Cap_Guards_Intact()
    {
        var root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(
            root, "Abhyanvaya.Application", "Scheduling", "TimetableEntryCourseDepartmentRules.cs")));
        Assert.True(File.Exists(Path.Combine(
            root, "Abhyanvaya.Application", "Scheduling", "SubjectAllocationCourseDepartmentRules.cs")));
        Assert.True(File.Exists(Path.Combine(
            root, "Abhyanvaya.Application.UnitTests", "Scheduling", "AiSchedTg6FinalArchitectureGuardTests.cs")));
        Assert.True(File.Exists(Path.Combine(
            root, "Abhyanvaya.Application.UnitTests", "Scheduling", "AiSchedCapPrompt11EndToEndAcceptanceGuardTests.cs")));
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
