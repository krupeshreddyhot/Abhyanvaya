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

public sealed class LegacySemesterWildcardRetirementServiceTests
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

    private static (ApplicationDbContext Db, LegacySemesterWildcardRetirementService Svc) CreateSut(
        LegacySemesterFinalizationAuditDto? fin = null)
    {
        var user = new AmbientUser();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("p143l-" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);

        var finalization = new Mock<ILegacySemesterFinalizationAuditService>();
        finalization.Setup(f => f.BuildAuditAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(fin ?? new LegacySemesterFinalizationAuditDto
            {
                IsReadOnly = true,
                NoMutationsPerformed = true,
                Summary = new LegacySemesterFinalizationSummaryDto { LegacyNullGroupCount = 1 },
                LegacySemesters =
                [
                    new LegacySemesterInventoryRowDto
                    {
                        SemesterId = 2,
                        CourseId = 1,
                        CourseName = "B.Com",
                        Number = 2,
                        Name = "Semester II",
                        Disposition = LegacySemesterFinalizationDisposition.HistoricalRetain,
                        DispositionCode = "HISTORICAL_RETAIN",
                        DispositionEvidence = "Zero ops.",
                    },
                ],
                NullWildcardDependencies =
                [
                    new NullWildcardDependencyDto
                    {
                        Path = "AcademicTreeService",
                        Location = "tree",
                        Action = NullWildcardDependencyAction.ReplaceWithGroupScope,
                        ActionCode = "REPLACE_WITH_GROUP_SCOPE",
                    },
                    new NullWildcardDependencyDto
                    {
                        Path = "SemestersPage",
                        Location = "ui",
                        Action = NullWildcardDependencyAction.HistoricalReadOnly,
                        ActionCode = "HISTORICAL_READ_ONLY",
                    },
                ],
            });

        var integrity = new Mock<IPrompt3HPostSectionIntegrityAuditService>();
        integrity.Setup(i => i.BuildAuditAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Prompt3HPostSectionIntegrityAuditDto
            {
                IsHealthy = true,
                CanMakeGroupIdNotNull = false,
                CanAddGroupSemesterUniqueConstraint = false,
                CanRemoveLegacyWildcardSemantics = true,
                TenantIsolation = new Prompt3HTenantIsolationDto { Passed = true },
                TenantIsolationReady = true,
            });

        var svc = new LegacySemesterWildcardRetirementService(
            db, user, finalization.Object, integrity.Object,
            NullLogger<LegacySemesterWildcardRetirementService>.Instance);
        return (db, svc);
    }

    [Fact]
    public async Task Preview_Is_ReadOnly()
    {
        var (db, svc) = CreateSut();
        db.Set<Semester>().Add(new Semester
        {
            Id = 2, TenantId = 1, CourseId = 1, Number = 2, Name = "II", GroupId = null, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        var journals = await db.Set<LegacySemesterDispositionJournal>().CountAsync();

        var preview = await svc.PreviewAsync();
        Assert.True(preview.IsReadOnly);
        Assert.True(preview.NoMutationsPerformed);
        Assert.True(preview.OperationalWildcardRetiredInCode);
        Assert.Equal(journals, await db.Set<LegacySemesterDispositionJournal>().CountAsync());
        Assert.Contains(preview.WildcardSites, w => w.ClassificationCode == "SAFE_TO_REMOVE");
        Assert.Contains(preview.WildcardSites, w => w.ClassificationCode == "LEGACY_READ_ONLY_COMPATIBILITY");
    }

    [Fact]
    public async Task Execute_Is_Idempotent()
    {
        var (db, svc) = CreateSut();
        db.Set<Semester>().Add(new Semester
        {
            Id = 2, TenantId = 1, CourseId = 1, Number = 2, Name = "II", GroupId = null, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var first = await svc.ExecuteAsync();
        Assert.Equal("Completed", first.ExecutionStatus);
        Assert.True(first.ChangedCount >= 1);
        Assert.False(first.RolledBack);
        var afterFirst = await db.Set<LegacySemesterDispositionJournal>().CountAsync();

        var second = await svc.ExecuteAsync();
        Assert.Equal("AlreadyComplete", second.ExecutionStatus);
        Assert.Equal(0, second.ChangedCount);
        Assert.Equal(afterFirst, await db.Set<LegacySemesterDispositionJournal>().CountAsync());
    }

    [Fact]
    public async Task Active_Operational_Deps_Abort()
    {
        var (db, svc) = CreateSut(new LegacySemesterFinalizationAuditDto
        {
            IsReadOnly = true,
            LegacySemesters =
            [
                new LegacySemesterInventoryRowDto
                {
                    SemesterId = 3,
                    CourseId = 1,
                    Number = 3,
                    Name = "III",
                    StudentReferenceCount = 2,
                    Disposition = LegacySemesterFinalizationDisposition.ManualMappingRequired,
                    DispositionCode = "MANUAL_MAPPING_REQUIRED",
                    DispositionEvidence = "students remain",
                },
            ],
            NullWildcardDependencies = [],
        });

        var result = await svc.ExecuteAsync();
        Assert.Equal("Aborted", result.ExecutionStatus);
        Assert.True(result.RolledBack);
        Assert.Equal(0, result.ChangedCount);
        Assert.Equal(0, await db.Set<LegacySemesterDispositionJournal>().CountAsync());
    }

    [Fact]
    public async Task Subject_Only_Historical_Is_Manual_But_Executable()
    {
        var (db, svc) = CreateSut(new LegacySemesterFinalizationAuditDto
        {
            IsReadOnly = true,
            LegacySemesters =
            [
                new LegacySemesterInventoryRowDto
                {
                    SemesterId = 1,
                    CourseId = 1,
                    Number = 1,
                    Name = "I",
                    SubjectReferenceCount = 1,
                    Disposition = LegacySemesterFinalizationDisposition.ManualMappingRequired,
                    DispositionCode = "MANUAL_MAPPING_REQUIRED",
                    DispositionEvidence = "subject historical",
                },
            ],
            NullWildcardDependencies = [],
        });
        db.Set<Semester>().Add(new Semester
        {
            Id = 1, TenantId = 1, CourseId = 1, Number = 1, Name = "I", GroupId = null, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var preview = await svc.PreviewAsync();
        Assert.True(preview.ExecutionSafe);
        Assert.Contains(preview.Items, i => i.DispositionCode == "MANUAL_MAPPING_REQUIRED" && i.CanExecute);
        Assert.False(preview.CanMakeGroupIdNotNull);
    }

    [Fact]
    public async Task Readiness_Is_ReadOnly_And_Exposes_Contract()
    {
        var (db, svc) = CreateSut();
        db.Set<Semester>().Add(new Semester
        {
            Id = 1, TenantId = 1, CourseId = 1, Number = 1, Name = "I", GroupId = null, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var readiness = await svc.BuildReadinessAsync();
        Assert.True(readiness.IsReadOnly);
        Assert.False(readiness.SaveChangesInvoked);
        Assert.Equal("P1-4-3I3", readiness.PromptCode);
        Assert.True(readiness.NewNullGroupWritePathBlocked);
        Assert.NotNull(readiness.Blockers);
        Assert.False(readiness.CanMakeGroupIdNotNull);
        Assert.Contains(readiness.DispositionMatrix, i => i.SemesterId == 2 || i.SemesterId == 1);
    }
}

public sealed class AiSchedCatalogTimetableP14Prompt3LWildcardRetirementArchitectureGuardTests
{
    [Fact]
    public void Service_Does_Not_Mutate_Tg_Or_Schema()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "LegacySemesterWildcardRetirementService.cs"));
        Assert.Contains("P1-4-3L", src, StringComparison.Ordinal);
        Assert.Contains("BuildReadinessAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("tg.SemesterId =", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("new TeachingGroup(", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AlterColumn", src, StringComparison.Ordinal);
        Assert.DoesNotContain("IsRequired()", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ConflictEngine", src, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AssignedGroupId = groups", src, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Api_And_Docs_Exist()
    {
        var root = FindRepoRoot();
        var api = File.ReadAllText(Path.Combine(root, "Abhyanvaya.API", "Controllers", "SemesterController.cs"));
        Assert.Contains("legacy-wildcard-retirement-preview", api, StringComparison.Ordinal);
        Assert.Contains("legacy-wildcard-retirement/execute", api, StringComparison.Ordinal);
        Assert.Contains("legacy-wildcard-retirement-readiness", api, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(
            root, "docs", "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3I_LEGACY_WILDCARD_RETIREMENT.md")));
        Assert.True(File.Exists(Path.Combine(
            root, "docs", "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3I_LEGACY_SEMESTER_DISPOSITION_WILDCARD_RETIREMENT.md")));
    }

    [Fact]
    public void SubjectsPage_Does_Not_Use_Null_Group_Wildcard()
    {
        var root = FindRepoRoot();
        var page = File.ReadAllText(Path.Combine(root, "abhyanvaya-ui", "src", "pages", "setup", "SubjectsPage.tsx"));
        Assert.DoesNotContain("s.groupId == null || Number(s.groupId)", page, StringComparison.Ordinal);
        Assert.DoesNotContain("x.groupId == null || x.groupId === g0", page, StringComparison.Ordinal);
        Assert.Contains("filterSemestersForScope", page, StringComparison.Ordinal);
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
