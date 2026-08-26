using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

public sealed class SubjectCatalogSemesterRemediationServiceTests
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

    private static (ApplicationDbContext Db, SubjectCatalogSemesterRemediationService Svc) CreateSut()
    {
        var user = new AmbientUser();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("p143j-" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);
        var svc = new SubjectCatalogSemesterRemediationService(
            db, user, NullLogger<SubjectCatalogSemesterRemediationService>.Instance);
        return (db, svc);
    }

    private static async Task SeedBaseAsync(ApplicationDbContext db)
    {
        db.Set<Course>().Add(new Course
        {
            Id = 1, TenantId = 1, Code = "BCOM", Name = "B.Com", DepartmentId = 1, CreatedDate = DateTime.UtcNow,
        });
        db.Set<Group>().AddRange(
            new Group { Id = 1, TenantId = 1, CourseId = 1, Code = "01", Name = "Finance", CreatedDate = DateTime.UtcNow },
            new Group { Id = 2, TenantId = 1, CourseId = 1, Code = "05", Name = "CA", CreatedDate = DateTime.UtcNow });
        db.Set<Semester>().AddRange(
            new Semester { Id = 3, TenantId = 1, CourseId = 1, Number = 3, Name = "Semester III", GroupId = null, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 10, TenantId = 1, CourseId = 1, Number = 3, Name = "Semester III", GroupId = 1, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 11, TenantId = 1, CourseId = 1, Number = 3, Name = "Semester III", GroupId = 2, CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Preview_Is_ReadOnly_And_Classifies_Deterministic_Remap()
    {
        var (db, svc) = CreateSut();
        await SeedBaseAsync(db);
        db.Set<Subject>().Add(new Subject
        {
            Id = 1, TenantId = 1, TenantSubjectId = 100, CourseId = 1, GroupId = 1, SemesterId = 3, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var preview = await svc.PreviewAsync();
        Assert.True(preview.IsReadOnly);
        Assert.Equal(0, await db.Set<LegacySemesterDispositionJournal>().CountAsync());
        Assert.Contains(preview.Items, i => i.SubjectId == 1 && i.StatusKind == SubjectCatalogRemediationStatus.SafeToRemap && i.TargetSemesterId == 10);
        Assert.Equal(3, (await db.Set<Subject>().FindAsync(1))!.SemesterId);
    }

    [Fact]
    public async Task Execute_Remaps_Idempotently()
    {
        var (db, svc) = CreateSut();
        await SeedBaseAsync(db);
        db.Set<Subject>().Add(new Subject
        {
            Id = 1, TenantId = 1, TenantSubjectId = 100, CourseId = 1, GroupId = 2, SemesterId = 3, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var first = await svc.ExecuteAsync();
        Assert.Equal("Completed", first.ExecutionStatus);
        Assert.Equal(1, first.ChangedCount);
        Assert.Equal(11, (await db.Set<Subject>().FindAsync(1))!.SemesterId);
        Assert.Equal(1, await db.Set<LegacySemesterDispositionJournal>().CountAsync());

        var second = await svc.ExecuteAsync();
        Assert.Equal("AlreadyComplete", second.ExecutionStatus);
        Assert.Equal(0, second.ChangedCount);
        Assert.Equal(1, await db.Set<LegacySemesterDispositionJournal>().CountAsync());
    }

    [Fact]
    public async Task Multiple_Candidates_Require_Manual_Mapping()
    {
        var (db, svc) = CreateSut();
        await SeedBaseAsync(db);
        db.Set<Semester>().Add(new Semester
        {
            Id = 20, TenantId = 1, CourseId = 1, Number = 3, Name = "Dup", GroupId = 1, CreatedDate = DateTime.UtcNow,
        });
        db.Set<Subject>().Add(new Subject
        {
            Id = 1, TenantId = 1, TenantSubjectId = 1, CourseId = 1, GroupId = 1, SemesterId = 3, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var preview = await svc.PreviewAsync();
        Assert.Contains(preview.Items, i => i.StatusKind == SubjectCatalogRemediationStatus.ManualMappingRequired);
        var exec = await svc.ExecuteAsync();
        Assert.Equal(0, exec.ChangedCount);
        Assert.Equal(3, (await db.Set<Subject>().FindAsync(1))!.SemesterId);
    }

    [Fact]
    public async Task Missing_Target_Is_Historical_Or_Blocked()
    {
        var (db, svc) = CreateSut();
        await SeedBaseAsync(db);
        db.Set<Semester>().Remove(await db.Set<Semester>().FindAsync(10)!);
        db.Set<Subject>().Add(new Subject
        {
            Id = 1, TenantId = 1, TenantSubjectId = 1, CourseId = 1, GroupId = 1, SemesterId = 3, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var preview = await svc.PreviewAsync();
        Assert.Contains(preview.Items, i =>
            i.SubjectId == 1
            && i.StatusKind is SubjectCatalogRemediationStatus.HistoricalRetain or SubjectCatalogRemediationStatus.Blocked);
    }

    [Fact]
    public async Task Tg_Mismatch_Blocks_Without_Mutating_Tg()
    {
        var (db, svc) = CreateSut();
        await SeedBaseAsync(db);
        db.Set<Subject>().Add(new Subject
        {
            Id = 1, TenantId = 1, TenantSubjectId = 1, CourseId = 1, GroupId = 1, SemesterId = 3, CreatedDate = DateTime.UtcNow,
        });
        db.Set<TeachingGroup>().Add(new TeachingGroup
        {
            Id = 1, TenantId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 1, SemesterId = 3,
            SubjectId = 1, SubjectAllocationId = 1, Code = "TG1", Name = "TG1",
            Status = TeachingGroupStatus.Active, CreatedDate = DateTime.UtcNow,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
        });
        await db.SaveChangesAsync();

        var exec = await svc.ExecuteAsync();
        Assert.Equal(0, exec.ChangedCount);
        Assert.Equal(3, (await db.Set<Subject>().FindAsync(1))!.SemesterId);
        Assert.Equal(3, (await db.Set<TeachingGroup>().FindAsync(1))!.SemesterId);
    }

    [Fact]
    public async Task Cross_Tenant_Subject_Not_Included()
    {
        var (db, svc) = CreateSut();
        await SeedBaseAsync(db);
        db.Set<Subject>().Add(new Subject
        {
            Id = 99, TenantId = 2, TenantSubjectId = 1, CourseId = 1, GroupId = 1, SemesterId = 3, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var preview = await svc.PreviewAsync();
        Assert.DoesNotContain(preview.Items, i => i.SubjectId == 99);
    }

    [Fact]
    public async Task No_Sa_Mutation()
    {
        var (db, svc) = CreateSut();
        await SeedBaseAsync(db);
        db.Set<Subject>().Add(new Subject
        {
            Id = 1, TenantId = 1, TenantSubjectId = 1, CourseId = 1, GroupId = 1, SemesterId = 3, CreatedDate = DateTime.UtcNow,
        });
        db.Set<SubjectAllocation>().Add(new SubjectAllocation
        {
            Id = 1, TenantId = 1, AcademicYearId = 1, SubjectId = 1, StaffId = 1,
            CourseId = 1, GroupId = 1, SemesterId = 10, DepartmentId = 1,
            WeeklyHours = 1, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow), CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var saBefore = await db.Set<SubjectAllocation>().AsNoTracking().Select(a => new { a.Id, a.SemesterId }).ToListAsync();
        await svc.ExecuteAsync();
        var saAfter = await db.Set<SubjectAllocation>().AsNoTracking().Select(a => new { a.Id, a.SemesterId }).ToListAsync();
        Assert.Equal(saBefore, saAfter);
        Assert.Equal(10, (await db.Set<Subject>().FindAsync(1))!.SemesterId);
    }
}

public sealed class AiSchedCatalogTimetableP14Prompt3JSubjectCatalogRemediationArchitectureGuardTests
{
    [Fact]
    public void Service_Preserves_Boundaries()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "SubjectCatalogSemesterRemediationService.cs"));

        Assert.Contains("SUBJECT_CATALOG_SEMESTER_REMAP", src, StringComparison.Ordinal);
        Assert.Contains("P1-4-3J", src, StringComparison.Ordinal);
        Assert.Contains("ExecuteInTransactionAsync", src, StringComparison.Ordinal);
        Assert.Contains("ConcurrencyExceptionHelper.SaveChangesAsync", src, StringComparison.Ordinal);
        Assert.Contains("subject.SemesterId = targetId", src, StringComparison.Ordinal);
        Assert.DoesNotContain("tg.SemesterId =", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("new TeachingGroup(", src, StringComparison.Ordinal);
        Assert.DoesNotContain("new TimetableSection", src, StringComparison.Ordinal);
        Assert.DoesNotContain("new SubjectAllocation", src, StringComparison.Ordinal);
        Assert.DoesNotContain("legacy.GroupId =", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AlterColumn", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ConflictEngine", src, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishAsync", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_Endpoints_Exist()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(root, "Abhyanvaya.API", "Controllers", "SemesterController.cs"));
        Assert.Contains("subject-catalog-remediation-preview", src, StringComparison.Ordinal);
        Assert.Contains("subject-catalog-remediation/execute", src, StringComparison.Ordinal);
        Assert.Contains("ISubjectCatalogSemesterRemediationService", src, StringComparison.Ordinal);
        Assert.Contains("CanManageSemesters", src, StringComparison.Ordinal);
        Assert.DoesNotContain("Auto Fix All", src, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Documentation_Exists()
    {
        var root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(
            root, "docs", "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3J_SUBJECT_CATALOG_SEMESTER_REMEDIATION.md")));
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
