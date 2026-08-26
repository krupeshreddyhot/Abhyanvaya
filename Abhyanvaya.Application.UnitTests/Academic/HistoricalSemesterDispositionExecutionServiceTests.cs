using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

public sealed class HistoricalSemesterDispositionExecutionServiceTests
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

    private static (
        ApplicationDbContext Db,
        HistoricalSemesterDispositionExecutionService Svc,
        AmbientUser User,
        Mock<IHistoricalSemesterDispositionAuditService> Audit)
        CreateSut(params HistoricalSemesterDispositionDto[] auditItems)
    {
        var user = new AmbientUser();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("p143kb-" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);

        var audit = new Mock<IHistoricalSemesterDispositionAuditService>();
        audit.Setup(a => a.BuildAuditAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HistoricalSemesterDispositionAuditDto
            {
                IsReadOnly = true,
                Items = auditItems,
            });

        var svc = new HistoricalSemesterDispositionExecutionService(
            db, user, audit.Object, NullLogger<HistoricalSemesterDispositionExecutionService>.Instance);
        return (db, svc, user, audit);
    }

    private static HistoricalSemesterDispositionDto Eligible(int id, int courseId = 1, int number = 2)
        => new()
        {
            SemesterId = id,
            CourseId = courseId,
            SemesterNumber = number,
            Classification = HistoricalSemesterDispositionClassifications.ArchiveEligible,
            IsArchiveEligible = true,
            IsHistorical = true,
            RecommendedAction = "Eligible",
            DownstreamReferenceSummary = new HistoricalSemesterDownstreamReferenceSummaryDto(),
        };

    [Fact]
    public async Task Archive_Eligible_Succeeds_And_Does_Not_Invent_Group()
    {
        var (db, svc, _, _) = CreateSut(Eligible(2));
        db.Set<Semester>().Add(new Semester
        {
            Id = 2, TenantId = 1, CourseId = 1, Number = 2, Name = "II", GroupId = null, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await svc.ExecuteAsync(new HistoricalSemesterDispositionExecuteRequest
        {
            Disposition = "HISTORICAL_ARCHIVE",
            SemesterIds = [2],
        });

        Assert.True(result.IsSuccessful);
        Assert.Equal(1, result.Archived);
        Assert.False(result.GroupIdInvented);
        Assert.False(result.DownstreamEntitiesMutated);
        var sem = await db.Set<Semester>().SingleAsync(s => s.Id == 2);
        Assert.True(sem.IsHistoricalArchive);
        Assert.Null(sem.GroupId);
        Assert.Equal(1, await db.Set<LegacySemesterDispositionJournal>().CountAsync(j =>
            j.SemesterId == 2 && j.PromptCode == "P1-4-3KB"));
    }

    [Fact]
    public async Task Manual_And_Duplicate_Are_Rejected_All_Or_Nothing()
    {
        var (db, svc, _, _) = CreateSut(
            new HistoricalSemesterDispositionDto
            {
                SemesterId = 1,
                Classification = HistoricalSemesterDispositionClassifications.ManualMappingRequired,
                RecommendedAction = "manual",
                DownstreamReferenceSummary = new HistoricalSemesterDownstreamReferenceSummaryDto(),
            },
            Eligible(2));
        db.Set<Semester>().AddRange(
            new Semester { Id = 1, TenantId = 1, CourseId = 1, Number = 1, Name = "I", GroupId = null, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 2, TenantId = 1, CourseId = 1, Number = 2, Name = "II", GroupId = null, CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var result = await svc.ExecuteAsync(new HistoricalSemesterDispositionExecuteRequest
        {
            Disposition = "HISTORICAL_ARCHIVE",
            SemesterIds = [1, 2],
        });

        Assert.False(result.IsSuccessful);
        Assert.True(result.RolledBack);
        Assert.Equal(0, result.Archived);
        Assert.All(await db.Set<Semester>().ToListAsync(), s => Assert.False(s.IsHistoricalArchive));
        Assert.Empty(await db.Set<LegacySemesterDispositionJournal>().ToListAsync());
    }

    [Fact]
    public async Task Duplicate_Review_Rejected()
    {
        var (db, svc, _, _) = CreateSut(new HistoricalSemesterDispositionDto
        {
            SemesterId = 4,
            Classification = HistoricalSemesterDispositionClassifications.DuplicateReview,
            RecommendedAction = "dup",
            DownstreamReferenceSummary = new HistoricalSemesterDownstreamReferenceSummaryDto(),
        });
        db.Set<Semester>().Add(new Semester
        {
            Id = 4, TenantId = 1, CourseId = 1, Number = 4, Name = "IV", GroupId = null, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await svc.ExecuteAsync(new HistoricalSemesterDispositionExecuteRequest
        {
            Disposition = "HISTORICAL_ARCHIVE",
            SemesterIds = [4],
        });

        Assert.False(result.IsSuccessful);
        Assert.False(await db.Set<Semester>().Where(s => s.Id == 4).Select(s => s.IsHistoricalArchive).FirstAsync());
    }

    [Fact]
    public async Task Retain_Historical_Is_Rejected_Unchanged()
    {
        var (db, svc, _, _) = CreateSut(new HistoricalSemesterDispositionDto
        {
            SemesterId = 7,
            Classification = HistoricalSemesterDispositionClassifications.HistoricalRetain,
            RecommendedAction = "retain",
            DownstreamReferenceSummary = new HistoricalSemesterDownstreamReferenceSummaryDto { SubjectRefs = 2 },
        });
        db.Set<Semester>().Add(new Semester
        {
            Id = 7, TenantId = 1, CourseId = 1, Number = 7, Name = "VII", GroupId = null, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await svc.ExecuteAsync(new HistoricalSemesterDispositionExecuteRequest
        {
            Disposition = "HISTORICAL_ARCHIVE",
            SemesterIds = [7],
        });

        Assert.False(result.IsSuccessful);
        Assert.Equal(0, result.Archived);
        Assert.False(await db.Set<Semester>().Where(s => s.Id == 7).Select(s => s.IsHistoricalArchive).FirstAsync());
        Assert.Empty(await db.Set<LegacySemesterDispositionJournal>().ToListAsync());
    }

    [Fact]
    public async Task Tg_Blocked_Classification_Rejected()
    {
        var (db, svc, _, _) = CreateSut(new HistoricalSemesterDispositionDto
        {
            SemesterId = 3,
            Classification = HistoricalSemesterDispositionClassifications.BlockedByReference,
            RecommendedAction = "tg",
            DownstreamReferenceSummary = new HistoricalSemesterDownstreamReferenceSummaryDto { TeachingGroupRefs = 1, OperationalRefTotal = 1 },
        });
        db.Set<Semester>().Add(new Semester
        {
            Id = 3, TenantId = 1, CourseId = 1, Number = 3, Name = "III", GroupId = null, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await svc.ExecuteAsync(new HistoricalSemesterDispositionExecuteRequest
        {
            Disposition = "HISTORICAL_ARCHIVE",
            SemesterIds = [3],
        });

        Assert.False(result.IsSuccessful);
        Assert.Contains(result.Results, r => r.Result == "Rejected");
    }

    [Fact]
    public async Task Already_Archived_Is_Idempotent()
    {
        var (db, svc, _, _) = CreateSut(new HistoricalSemesterDispositionDto
        {
            SemesterId = 2,
            Classification = HistoricalSemesterDispositionClassifications.Archived,
            IsHistoricalArchive = true,
            DownstreamReferenceSummary = new HistoricalSemesterDownstreamReferenceSummaryDto(),
        });
        db.Set<Semester>().Add(new Semester
        {
            Id = 2, TenantId = 1, CourseId = 1, Number = 2, Name = "II", GroupId = null,
            IsHistoricalArchive = true, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var first = await svc.ExecuteAsync(new HistoricalSemesterDispositionExecuteRequest
        {
            Disposition = "HISTORICAL_ARCHIVE",
            SemesterIds = [2],
        });
        Assert.True(first.IsSuccessful);
        Assert.Equal("AlreadyComplete", first.ExecutionStatus);
        Assert.Equal(0, first.Archived);

        var journalsBefore = await db.Set<LegacySemesterDispositionJournal>().CountAsync();
        var second = await svc.ExecuteAsync(new HistoricalSemesterDispositionExecuteRequest
        {
            Disposition = "HISTORICAL_ARCHIVE",
            SemesterIds = [2],
        });
        Assert.True(second.IsSuccessful);
        Assert.Equal(journalsBefore, await db.Set<LegacySemesterDispositionJournal>().CountAsync());
    }

    [Fact]
    public async Task Second_Successful_Execution_Zero_Additional_Writes()
    {
        var (db, svc, _, audit) = CreateSut(Eligible(2));
        db.Set<Semester>().Add(new Semester
        {
            Id = 2, TenantId = 1, CourseId = 1, Number = 2, Name = "II", GroupId = null, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var first = await svc.ExecuteAsync(new HistoricalSemesterDispositionExecuteRequest
        {
            Disposition = "HISTORICAL_ARCHIVE",
            SemesterIds = [2],
        });
        Assert.Equal(1, first.Archived);

        audit.Setup(a => a.BuildAuditAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HistoricalSemesterDispositionAuditDto
            {
                Items =
                [
                    new HistoricalSemesterDispositionDto
                    {
                        SemesterId = 2,
                        Classification = HistoricalSemesterDispositionClassifications.Archived,
                        IsHistoricalArchive = true,
                        DownstreamReferenceSummary = new HistoricalSemesterDownstreamReferenceSummaryDto(),
                    },
                ],
            });

        var journals = await db.Set<LegacySemesterDispositionJournal>().CountAsync();
        var second = await svc.ExecuteAsync(new HistoricalSemesterDispositionExecuteRequest
        {
            Disposition = "HISTORICAL_ARCHIVE",
            SemesterIds = [2],
        });
        Assert.Equal(0, second.Archived);
        Assert.Equal(journals, await db.Set<LegacySemesterDispositionJournal>().CountAsync());
    }

    [Fact]
    public async Task Cross_Tenant_Semester_Rejected()
    {
        var (db, svc, _, _) = CreateSut(Eligible(99));
        db.Set<Semester>().Add(new Semester
        {
            Id = 99, TenantId = 2, CourseId = 1, Number = 1, Name = "Other", GroupId = null, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await svc.ExecuteAsync(new HistoricalSemesterDispositionExecuteRequest
        {
            Disposition = "HISTORICAL_ARCHIVE",
            SemesterIds = [99],
        });

        Assert.False(result.IsSuccessful);
        Assert.True(result.RolledBack);
        Assert.False(await db.Set<Semester>().IgnoreQueryFilters().Where(s => s.Id == 99).Select(s => s.IsHistoricalArchive).FirstAsync());
    }

    [Fact]
    public async Task Empty_Ids_Rejected_No_Archive_All()
    {
        var (_, svc, _, _) = CreateSut();
        var result = await svc.ExecuteAsync(new HistoricalSemesterDispositionExecuteRequest
        {
            Disposition = "HISTORICAL_ARCHIVE",
            SemesterIds = [],
        });
        Assert.False(result.IsSuccessful);
        Assert.Contains("explicit", result.AbortReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Audit_Eligible_Becomes_Archived_Classification()
    {
        var user = new AmbientUser();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("p143kb-post-" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);

        var fin = new Mock<ILegacySemesterFinalizationAuditService>();
        fin.Setup(f => f.BuildAuditAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySemesterFinalizationAuditDto
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

        var audit = new HistoricalSemesterDispositionAuditService(db, user, fin.Object);
        var exec = new HistoricalSemesterDispositionExecutionService(
            db, user, audit, NullLogger<HistoricalSemesterDispositionExecutionService>.Instance);

        db.Set<Course>().Add(new Course
        {
            Id = 1, TenantId = 1, Code = "BCOM", Name = "B.Com", DepartmentId = 1, CreatedDate = DateTime.UtcNow,
        });
        db.Set<Semester>().Add(new Semester
        {
            Id = 2, TenantId = 1, CourseId = 1, Number = 2, Name = "II", GroupId = null, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var before = await audit.BuildAuditAsync();
        Assert.Contains(before.Items, i =>
            i.SemesterId == 2 && i.Classification == HistoricalSemesterDispositionClassifications.ArchiveEligible);

        var result = await exec.ExecuteAsync(new HistoricalSemesterDispositionExecuteRequest
        {
            Disposition = "HISTORICAL_ARCHIVE",
            SemesterIds = [2],
        });
        Assert.True(result.IsSuccessful);

        var after = await audit.BuildAuditAsync();
        Assert.Contains(after.Items, i =>
            i.SemesterId == 2 && i.Classification == HistoricalSemesterDispositionClassifications.Archived);
    }
}

public sealed class AiSchedCatalogTimetableP14Prompt3KBHistoricalArchiveExecutionGuardTests
{
    [Fact]
    public void Execution_Reuses_Existing_Lifecycle_And_Does_Not_Mutate_Tg_Or_Cap()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "HistoricalSemesterDispositionExecutionService.cs"));
        Assert.Contains("P1-4-3KB", src, StringComparison.Ordinal);
        Assert.Contains("IsHistoricalArchive = true", src, StringComparison.Ordinal);
        Assert.Contains("LegacySemesterDispositionJournal", src, StringComparison.Ordinal);
        Assert.Contains("ALL_OR_NOTHING", src, StringComparison.Ordinal);
        Assert.Contains("ExecuteInTransactionAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AssignedGroupId = groups", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tg.SemesterId =", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("new TeachingGroup", src, StringComparison.Ordinal);
        Assert.DoesNotContain("TimetableSection", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ConflictEngine", src, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AlterColumn", src, StringComparison.Ordinal);
        Assert.DoesNotContain("class HistoricalSemesterStatus", src, StringComparison.Ordinal);
        Assert.DoesNotContain("Students.Update", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AttendanceSessions.Update", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_And_Docs_Exist()
    {
        var root = FindRepoRoot();
        var api = File.ReadAllText(Path.Combine(root, "Abhyanvaya.API", "Controllers", "SemesterController.cs"));
        Assert.Contains("historical-disposition/execute", api, StringComparison.Ordinal);
        Assert.Contains("IHistoricalSemesterDispositionExecutionService", api, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(
            root, "docs", "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3K_B_HISTORICAL_SEMESTER_ARCHIVAL_EXECUTION.md")));
    }

    [Fact]
    public void Active_Query_Does_Not_Treat_Null_Group_As_Wildcard()
    {
        var root = FindRepoRoot();
        var tree = File.ReadAllText(Path.Combine(root, "Abhyanvaya.Application", "Academic", "AcademicTreeService.cs"));
        Assert.DoesNotContain("s.GroupId == null || s.GroupId == g.Id", tree, StringComparison.Ordinal);
        var rules = File.ReadAllText(Path.Combine(root, "Abhyanvaya.Application", "Academic", "OperationalSemesterRules.cs"));
        Assert.Contains("!s.IsHistoricalArchive", rules, StringComparison.Ordinal);
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
