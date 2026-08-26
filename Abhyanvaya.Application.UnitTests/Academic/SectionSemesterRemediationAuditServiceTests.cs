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

public sealed class SectionSemesterRemediationAuditServiceTests
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

    private static (ApplicationDbContext Db, SectionSemesterRemediationAuditService Svc) CreateSut(int tenantId = 1)
    {
        var user = new AmbientUser { TenantId = tenantId };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("p143g1-" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);
        return (db, new SectionSemesterRemediationAuditService(db, user));
    }

    private static async Task SeedTargetsAsync(ApplicationDbContext db)
    {
        db.Set<Course>().Add(new Course
        {
            Id = 1, TenantId = 1, Code = "BCOM", Name = "B.Com", DepartmentId = 1, CreatedDate = DateTime.UtcNow,
        });
        db.Set<Group>().AddRange(
            new Group { Id = 1, TenantId = 1, CourseId = 1, Code = "01", Name = "Finance", CreatedDate = DateTime.UtcNow },
            new Group { Id = 2, TenantId = 1, CourseId = 1, Code = "05", Name = "CA", CreatedDate = DateTime.UtcNow });
        db.Set<Semester>().AddRange(
            new Semester { Id = 3, TenantId = 1, CourseId = 1, Number = 3, Name = "III", GroupId = null, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 10, TenantId = 1, CourseId = 1, Number = 3, Name = "III-Fin", GroupId = 1, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 11, TenantId = 1, CourseId = 1, Number = 3, Name = "III-CA", GroupId = 2, CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Discovers_Legacy_Sections_On_Sem3()
    {
        var (db, svc) = CreateSut();
        await SeedTargetsAsync(db);
        db.Set<Section>().Add(new Section
        {
            Id = 5, TenantId = 1, CollegeId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2,
            SemesterId = 3, SectionCode = "CA-A", SectionName = "CA A", Status = "Active", CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var report = await svc.BuildAuditAsync();
        Assert.Equal(1, report.TotalLegacySections);
        Assert.Contains(report.Sections, s => s.SectionId == 5 && s.CurrentSemesterId == 3);
    }

    [Fact]
    public async Task Finance_Section_Is_SafeForFinance()
    {
        var (db, svc) = CreateSut();
        await SeedTargetsAsync(db);
        db.Set<Section>().Add(new Section
        {
            Id = 4, TenantId = 1, CollegeId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 1,
            SemesterId = 3, SectionCode = "FIN-A", SectionName = "Finance A", Status = "Active", CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var report = await svc.BuildAuditAsync();
        var row = Assert.Single(report.Sections.Where(s => s.SectionId == 4));
        Assert.Equal(SectionSemesterAuditClassification.SafeForFinance, row.Classification);
        Assert.Equal(10, row.TargetSemesterId);
        Assert.True(row.IsDeterministic);
    }

    [Fact]
    public async Task Ca_Section_Is_SafeForCa()
    {
        var (db, svc) = CreateSut();
        await SeedTargetsAsync(db);
        db.Set<Section>().Add(new Section
        {
            Id = 5, TenantId = 1, CollegeId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2,
            SemesterId = 3, SectionCode = "CA-A", SectionName = "CA A", Status = "Active", CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var report = await svc.BuildAuditAsync();
        var row = Assert.Single(report.Sections.Where(s => s.SectionId == 5));
        Assert.Equal(SectionSemesterAuditClassification.SafeForCa, row.Classification);
        Assert.Equal(11, row.TargetSemesterId);
    }

    [Fact]
    public async Task Ambiguous_Other_Group_Is_ManualMappingRequired()
    {
        var (db, svc) = CreateSut();
        await SeedTargetsAsync(db);
        db.Set<Group>().Add(new Group
        {
            Id = 9, TenantId = 1, CourseId = 1, Code = "99", Name = "Other", CreatedDate = DateTime.UtcNow,
        });
        db.Set<Section>().Add(new Section
        {
            Id = 50, TenantId = 1, CollegeId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 9,
            SemesterId = 3, SectionCode = "OTH", SectionName = "Other", Status = "Active", CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var report = await svc.BuildAuditAsync();
        var row = Assert.Single(report.Sections.Where(s => s.SectionId == 50));
        Assert.Equal(SectionSemesterAuditClassification.ManualMappingRequired, row.Classification);
        Assert.False(report.IsReady);
        Assert.Equal(SectionSemesterAuditReadiness.NotReady, report.Readiness);
    }

    [Fact]
    public async Task Missing_Course_Is_InvalidReference()
    {
        var (db, svc) = CreateSut();
        await SeedTargetsAsync(db);
        db.Set<Section>().Add(new Section
        {
            Id = 60, TenantId = 1, CollegeId = 1, AcademicYearId = 1, CourseId = 999, GroupId = 2,
            SemesterId = 3, SectionCode = "X", SectionName = "X", Status = "Active", CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var report = await svc.BuildAuditAsync();
        var row = Assert.Single(report.Sections.Where(s => s.SectionId == 60));
        Assert.Equal(SectionSemesterAuditClassification.InvalidReference, row.Classification);
    }

    [Fact]
    public async Task Tg_On_Conflicting_Target_Semester_Blocks()
    {
        var (db, svc) = CreateSut();
        await SeedTargetsAsync(db);
        db.Set<Section>().Add(new Section
        {
            Id = 5, TenantId = 1, CollegeId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2,
            SemesterId = 3, SectionCode = "CA-A", SectionName = "CA A", Status = "Active", CreatedDate = DateTime.UtcNow,
        });
        // TG on Finance Sem 10 while Section is CA → incompatible
        db.Set<TeachingGroup>().Add(new TeachingGroup
        {
            Id = 1, TenantId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2, SemesterId = 10,
            SubjectId = 1, SubjectAllocationId = 1, Name = "TG", Status = TeachingGroupStatus.Active,
            CreatedDate = DateTime.UtcNow, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
        });
        db.Set<TeachingGroupSection>().Add(new TeachingGroupSection
        {
            Id = 1, TenantId = 1, TeachingGroupId = 1, SectionId = 5, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var report = await svc.BuildAuditAsync();
        var row = Assert.Single(report.Sections.Where(s => s.SectionId == 5));
        Assert.Equal(SectionSemesterAuditClassification.Blocked, row.Classification);
        Assert.Contains(report.TeachingGroupSections, t => t.Compatibility == TeachingGroupSectionCompatibilityStatus.Incompatible);
        Assert.False(report.IsReady);
    }

    [Fact]
    public async Task Cross_Tenant_Section_Course_Is_Blocked_Or_Invalid()
    {
        var (db, svc) = CreateSut();
        await SeedTargetsAsync(db);
        // Tenant-filtered Course (TenantId=2) is invisible under ambient tenant → missing/invalid path (fail closed).
        db.Set<Course>().Add(new Course
        {
            Id = 2, TenantId = 2, Code = "X", Name = "OtherTenant", DepartmentId = 1, CreatedDate = DateTime.UtcNow,
        });
        db.Set<Section>().Add(new Section
        {
            Id = 70, TenantId = 1, CollegeId = 1, AcademicYearId = 1, CourseId = 2, GroupId = 2,
            SemesterId = 3, SectionCode = "XT", SectionName = "XT", Status = "Active", CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var report = await svc.BuildAuditAsync();
        var row = Assert.Single(report.Sections.Where(s => s.SectionId == 70));
        Assert.Equal(SectionSemesterAuditClassification.InvalidReference, row.Classification);
        Assert.Contains(row.BlockingReasons, r =>
            r.Contains("missing", StringComparison.OrdinalIgnoreCase)
            || r.Contains("Cross-tenant", StringComparison.Ordinal)
            || r.Contains("!=", StringComparison.Ordinal));
        Assert.False(report.IsReady);
    }

    [Fact]
    public async Task Target_Semester_Validation_Reported()
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
            Id = 3, TenantId = 1, CourseId = 1, Number = 3, Name = "III", GroupId = null, CreatedDate = DateTime.UtcNow,
        });
        // Missing Finance Sem 10; CA 11 present
        db.Set<Semester>().Add(new Semester
        {
            Id = 11, TenantId = 1, CourseId = 1, Number = 3, Name = "III-CA", GroupId = 2, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var report = await svc.BuildAuditAsync();
        Assert.False(report.FinanceTargetValid);
        Assert.True(report.CaTargetValid);
        Assert.False(report.IsReady);
    }

    [Fact]
    public async Task Audit_Is_ReadOnly_And_Idempotent()
    {
        var (db, svc) = CreateSut();
        await SeedTargetsAsync(db);
        db.Set<Section>().Add(new Section
        {
            Id = 5, TenantId = 1, CollegeId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2,
            SemesterId = 3, SectionCode = "CA-A", SectionName = "CA A", Status = "Active", CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var before = await db.Set<Section>().AsNoTracking().Select(s => new { s.Id, s.SemesterId }).ToListAsync();
        var a = await svc.BuildAuditAsync();
        var b = await svc.BuildAuditAsync();

        Assert.True(a.IsReadOnly);
        Assert.True(a.NoMutationsPerformed);
        Assert.False(a.SaveChangesInvoked);
        Assert.Equal(a.Readiness, b.Readiness);
        Assert.Equal(a.TotalLegacySections, b.TotalLegacySections);
        Assert.Equal(before, await db.Set<Section>().AsNoTracking().Select(s => new { s.Id, s.SemesterId }).ToListAsync());
        Assert.Equal(0, await db.Set<TeachingGroup>().CountAsync(t => t.SemesterId != 3 && t.Id == 1));
    }

    [Fact]
    public async Task Already_On_Target_Is_AlreadyCorrect_And_Zero_Legacy_Is_Ready()
    {
        var (db, svc) = CreateSut();
        await SeedTargetsAsync(db);
        db.Set<Section>().Add(new Section
        {
            Id = 5, TenantId = 1, CollegeId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2,
            SemesterId = 11, SectionCode = "CA-A", SectionName = "CA A", Status = "Active", CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var report = await svc.BuildAuditAsync();
        Assert.Equal(0, report.TotalLegacySections);
        Assert.True(report.AlreadyCorrectCount >= 1);
        Assert.True(report.IsReady);
        Assert.Equal(SectionSemesterAuditReadiness.Ready, report.Readiness);
    }

    [Fact]
    public async Task Dual_Safe_Legacy_Sections_Are_Ready()
    {
        var (db, svc) = CreateSut();
        await SeedTargetsAsync(db);
        db.Set<Section>().AddRange(
            new Section
            {
                Id = 4, TenantId = 1, CollegeId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 1,
                SemesterId = 3, SectionCode = "FIN-A", SectionName = "Finance A", Status = "Active", CreatedDate = DateTime.UtcNow,
            },
            new Section
            {
                Id = 5, TenantId = 1, CollegeId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2,
                SemesterId = 3, SectionCode = "CA-A", SectionName = "CA A", Status = "Active", CreatedDate = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        var report = await svc.BuildAuditAsync();
        Assert.Equal(2, report.TotalLegacySections);
        Assert.Equal(1, report.SafeFinanceCount);
        Assert.Equal(1, report.SafeCaCount);
        Assert.True(report.IsReady);
    }
}

public sealed class AiSchedCatalogTimetableP14Prompt3G1SectionRemediationAuditArchitectureGuardTests
{
    [Fact]
    public void Service_Is_ReadOnly_Contract()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "SectionSemesterRemediationAuditService.cs"));
        Assert.Contains("P1-4-3G.1", src, StringComparison.Ordinal);
        Assert.Contains("AsNoTracking", src, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChangesAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChanges(", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteInTransactionAsync", src, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"section\.SemesterId\s*=(?!=)", src);
        Assert.DoesNotMatch(@"tg\.SemesterId\s*=(?!=)", src);
        Assert.DoesNotContain("ExecuteUpdate", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteDelete", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ConflictEngine", src, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AlterColumn", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_And_Documentation_Exist()
    {
        var root = FindRepoRoot();
        var api = File.ReadAllText(Path.Combine(root, "Abhyanvaya.API", "Controllers", "SemesterController.cs"));
        Assert.Contains("section-semester-remediation-audit", api, StringComparison.Ordinal);
        Assert.Contains("ISectionSemesterRemediationAuditService", api, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(
            root, "docs", "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3G1_SECTION_REMEDIATION_AUDIT.md")));
    }

    [Fact]
    public void Existing_Tg_Cap_Guards_Remain()
    {
        var root = FindRepoRoot();
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
