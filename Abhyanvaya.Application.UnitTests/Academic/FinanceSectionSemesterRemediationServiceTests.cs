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

public sealed class FinanceSectionSemesterRemediationServiceTests
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

    private static (ApplicationDbContext Db, FinanceSectionSemesterRemediationService Svc) CreateSut(int tenantId = 1)
    {
        var user = new AmbientUser { TenantId = tenantId };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("p143i-" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);
        var svc = new FinanceSectionSemesterRemediationService(
            db, user, NullLogger<FinanceSectionSemesterRemediationService>.Instance);
        return (db, svc);
    }

    private static async Task SeedAsync(
        ApplicationDbContext db,
        bool includeFinanceOnLegacy = true,
        bool includeCaOnLegacy = true,
        bool tgOnFinanceSection = false,
        int? tgSemesterId = 11)
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

        if (includeFinanceOnLegacy)
        {
            db.Set<Section>().Add(new Section
            {
                Id = 9, TenantId = 1, CollegeId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 1,
                SemesterId = 3, SectionCode = "FA-A", SectionName = "FA A", Status = "Active", CreatedDate = DateTime.UtcNow,
            });
        }

        if (includeCaOnLegacy)
        {
            db.Set<Section>().Add(new Section
            {
                Id = 5, TenantId = 1, CollegeId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2,
                SemesterId = 3, SectionCode = "CA-B", SectionName = "CA B", Status = "Active", CreatedDate = DateTime.UtcNow,
            });
        }

        if (tgOnFinanceSection && includeFinanceOnLegacy)
        {
            db.Set<TeachingGroup>().Add(new TeachingGroup
            {
                Id = 50, TenantId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 1,
                SemesterId = tgSemesterId ?? 3, SubjectId = 1, SubjectAllocationId = 1,
                Code = "TG-FIN", Name = "TG Fin", Status = TeachingGroupStatus.Active,
                CreatedDate = DateTime.UtcNow, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            });
            db.Set<TeachingGroupSection>().Add(new TeachingGroupSection
            {
                Id = 50, TenantId = 1, TeachingGroupId = 50, SectionId = 9, CreatedDate = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Preview_Is_ReadOnly_And_Classifies_Finance_Vs_Ca()
    {
        var (db, svc) = CreateSut();
        await SeedAsync(db);

        var preview = await svc.PreviewAsync();
        Assert.True(preview.IsReadOnly);
        Assert.Equal("NotExecuted", preview.ExecutionStatus);
        Assert.Contains(preview.Items, i => i.SectionId == 9 && i.StatusKind == FinanceSectionRemediationStatus.SafeToRemediate);
        Assert.Contains(preview.Items, i => i.SectionId == 5 && i.StatusKind == FinanceSectionRemediationStatus.NotInScope);
        Assert.Equal(3, await db.Set<Section>().Where(s => s.Id == 9).Select(s => s.SemesterId).SingleAsync());
        Assert.Equal(0, await db.Set<LegacySemesterDispositionJournal>().CountAsync());
    }

    [Fact]
    public async Task Execute_Remediates_Finance_Only_Idempotently()
    {
        var (db, svc) = CreateSut();
        await SeedAsync(db);

        var first = await svc.ExecuteAsync();
        Assert.Equal("Completed", first.ExecutionStatus);
        Assert.Equal(1, first.ChangedCount);
        Assert.Equal([9], first.AffectedSectionIds.ToList());
        Assert.Equal(10, (await db.Set<Section>().FindAsync(9))!.SemesterId);
        Assert.Equal(3, (await db.Set<Section>().FindAsync(5))!.SemesterId);
        Assert.Equal(1, await db.Set<LegacySemesterDispositionJournal>().CountAsync());

        var second = await svc.ExecuteAsync();
        Assert.Equal("AlreadyComplete", second.ExecutionStatus);
        Assert.Equal(0, second.ChangedCount);
        Assert.Equal(1, await db.Set<LegacySemesterDispositionJournal>().CountAsync());
    }

    [Fact]
    public async Task Ca_Section_Is_Not_In_Scope()
    {
        var (db, svc) = CreateSut();
        await SeedAsync(db, includeFinanceOnLegacy: false, includeCaOnLegacy: true);

        var preview = await svc.PreviewAsync();
        Assert.Contains(preview.Items, i => i.SectionId == 5 && i.StatusKind == FinanceSectionRemediationStatus.NotInScope);
        Assert.Equal(0, preview.EligibleCount);
    }

    [Fact]
    public async Task Missing_Target_Semester_Aborts()
    {
        var (db, svc) = CreateSut();
        await SeedAsync(db);
        db.Set<Semester>().Remove(await db.Set<Semester>().FindAsync(10)!);
        await db.SaveChangesAsync();

        var preview = await svc.PreviewAsync();
        Assert.Equal("Aborted", preview.ExecutionStatus);
        Assert.Contains("not found", preview.AbortReason ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Multiple_Target_Semesters_Fail_Closed()
    {
        var (db, svc) = CreateSut();
        await SeedAsync(db);
        db.Set<Semester>().Add(new Semester
        {
            Id = 20, TenantId = 1, CourseId = 1, Number = 3, Name = "Dup Finance", GroupId = 1, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var preview = await svc.PreviewAsync();
        Assert.Equal("Aborted", preview.ExecutionStatus);
        Assert.Contains("Multiple", preview.AbortReason ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Wrong_Target_Group_On_Contract_Id_Aborts_Via_Resolution()
    {
        var (db, svc) = CreateSut();
        await SeedAsync(db);
        var target = await db.Set<Semester>().FindAsync(10);
        target!.GroupId = 2;
        await db.SaveChangesAsync();

        var preview = await svc.PreviewAsync();
        Assert.Equal("Aborted", preview.ExecutionStatus);
    }

    [Fact]
    public async Task TeachingGroup_Dependency_With_Wrong_Sem_Blocks()
    {
        var (db, svc) = CreateSut();
        await SeedAsync(db, tgOnFinanceSection: true, tgSemesterId: 3);

        var exec = await svc.ExecuteAsync();
        Assert.Equal("Aborted", exec.ExecutionStatus);
        Assert.True(exec.RolledBack);
        Assert.Equal(0, exec.ChangedCount);
        Assert.Equal(3, (await db.Set<Section>().FindAsync(9))!.SemesterId);
        Assert.Equal(3, (await db.Set<TeachingGroup>().FindAsync(50))!.SemesterId);
    }

    [Fact]
    public async Task TeachingGroup_Already_On_Target_Allows_Remediation()
    {
        var (db, svc) = CreateSut();
        await SeedAsync(db, tgOnFinanceSection: true, tgSemesterId: 10);

        var exec = await svc.ExecuteAsync();
        Assert.Equal("Completed", exec.ExecutionStatus);
        Assert.Equal(10, (await db.Set<Section>().FindAsync(9))!.SemesterId);
        Assert.Equal(10, (await db.Set<TeachingGroup>().FindAsync(50))!.SemesterId);
        Assert.Equal(9, (await db.Set<TeachingGroupSection>().FindAsync(50))!.SectionId);
    }

    [Fact]
    public async Task Cross_Tenant_Section_Not_Visible()
    {
        var (db, svc) = CreateSut();
        await SeedAsync(db);
        db.Set<Section>().Add(new Section
        {
            Id = 99, TenantId = 2, CollegeId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 1,
            SemesterId = 3, SectionCode = "X", SectionName = "X", Status = "Active", CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var preview = await svc.PreviewAsync();
        Assert.DoesNotContain(preview.Items, i => i.SectionId == 99);
    }

    [Fact]
    public async Task No_Student_Tg_Sa_Tt_Mutation()
    {
        var (db, svc) = CreateSut();
        await SeedAsync(db);
        db.Set<StudentSection>().Add(new StudentSection
        {
            Id = 1, TenantId = 1, StudentId = 1, SectionId = 9, IsCurrent = true,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow), CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var ssBefore = await db.Set<StudentSection>().AsNoTracking().Select(x => new { x.Id, x.SectionId }).ToListAsync();
        await svc.ExecuteAsync();
        var ssAfter = await db.Set<StudentSection>().AsNoTracking().Select(x => new { x.Id, x.SectionId }).ToListAsync();
        Assert.Equal(ssBefore, ssAfter);
    }
}

public sealed class AiSchedCatalogTimetableP14Prompt3IFinanceSectionRemediationArchitectureGuardTests
{
    [Fact]
    public void Service_Is_Finance_Specific_And_Preserves_Boundaries()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "FinanceSectionSemesterRemediationService.cs"));

        Assert.Contains("ExpectedLegacySemesterId = 3", src, StringComparison.Ordinal);
        Assert.Contains("ExpectedTargetSemesterId = 10", src, StringComparison.Ordinal);
        Assert.Contains("ExpectedFinanceGroupId = 1", src, StringComparison.Ordinal);
        Assert.Contains("FINANCE_SECTION_SEMESTER_REMAP", src, StringComparison.Ordinal);
        Assert.Contains("ExecuteInTransactionAsync", src, StringComparison.Ordinal);
        Assert.Contains("ConcurrencyExceptionHelper.SaveChangesAsync", src, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(src, "section.SemesterId = ExpectedTargetSemesterId"));
        Assert.DoesNotContain("tg.SemesterId =", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("new TeachingGroup(", src, StringComparison.Ordinal);
        Assert.DoesNotContain("new TeachingGroupSection", src, StringComparison.Ordinal);
        Assert.DoesNotContain("new TimetableSection", src, StringComparison.Ordinal);
        Assert.DoesNotContain("new Student(", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AlterColumn", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_Endpoints_Exist()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(root, "Abhyanvaya.API", "Controllers", "SemesterController.cs"));
        Assert.Contains("finance-section-remediation-preview", src, StringComparison.Ordinal);
        Assert.Contains("finance-section-remediation/execute", src, StringComparison.Ordinal);
        Assert.Contains("IFinanceSectionSemesterRemediationService", src, StringComparison.Ordinal);
        Assert.Contains("CanManageSemesters", src, StringComparison.Ordinal);
        Assert.DoesNotContain("Change Section Semester", src, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Documentation_Exists()
    {
        var root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(
            root, "docs", "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3I_FINANCE_SECTION_SEMESTER_REMEDIATION.md")));
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }

        return count;
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
