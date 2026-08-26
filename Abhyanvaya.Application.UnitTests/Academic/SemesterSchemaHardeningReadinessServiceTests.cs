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

public sealed class SemesterSchemaHardeningReadinessServiceTests
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

    private static (ApplicationDbContext Db, SemesterSchemaHardeningReadinessService Svc) CreateSut(
        LegacySemesterFinalizationAuditDto? fin = null)
    {
        var user = new AmbientUser();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("p143m-" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);

        var finalization = new Mock<ILegacySemesterFinalizationAuditService>();
        finalization.Setup(f => f.BuildAuditAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(fin ?? new LegacySemesterFinalizationAuditDto
            {
                IsReadOnly = true,
                LegacySemesters = [],
                NullWildcardDependencies = [],
            });

        var svc = new SemesterSchemaHardeningReadinessService(db, user, finalization.Object);
        return (db, svc);
    }

    [Fact]
    public async Task Audit_Is_ReadOnly_And_Idempotent()
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

        var before = await db.Set<Semester>().AsNoTracking().Select(s => new { s.Id, s.GroupId }).ToListAsync();
        var a = await svc.BuildAsync();
        var b = await svc.BuildAsync();

        Assert.True(a.IsReadOnly);
        Assert.True(a.NoMutationsPerformed);
        Assert.False(a.SaveChangesInvoked);
        Assert.Equal(a.Decision, b.Decision);
        Assert.Equal(a.NullGroupSemesterCount, b.NullGroupSemesterCount);
        Assert.Equal(before, await db.Set<Semester>().AsNoTracking().Select(s => new { s.Id, s.GroupId }).ToListAsync());
    }

    [Fact]
    public async Task Null_Group_Forces_NoGo()
    {
        var (db, svc) = CreateSut(new LegacySemesterFinalizationAuditDto
        {
            LegacySemesters =
            [
                new LegacySemesterInventoryRowDto
                {
                    SemesterId = 3,
                    CourseId = 1,
                    Number = 3,
                    Disposition = LegacySemesterFinalizationDisposition.HistoricalRetain,
                    DispositionCode = "HISTORICAL_RETAIN",
                    DispositionEvidence = "retain",
                },
            ],
        });
        db.Set<Course>().Add(new Course
        {
            Id = 1, TenantId = 1, Code = "BCOM", Name = "B.Com", DepartmentId = 1, CreatedDate = DateTime.UtcNow,
        });
        db.Set<Semester>().Add(new Semester
        {
            Id = 3, TenantId = 1, CourseId = 1, Number = 3, Name = "III", GroupId = null, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var report = await svc.BuildAsync();
        Assert.Equal(SemesterSchemaHardeningDecision.NoGo, report.Decision);
        Assert.False(report.IsReady);
        Assert.False(report.NotNullReady);
        Assert.True(report.NullGroupSemesterCount >= 1);
        Assert.Contains(report.BlockingFindings, f => f.Code == "SEMESTER_NULL_GROUP");
        Assert.Contains(report.ReadinessCodes, c => c == SemesterSchemaHardeningReadinessCodes.NotReadyNullSemesters);
        Assert.Equal(SemesterSchemaHardeningReadinessCodes.NotReadyNullSemesters, report.DecisionCode);
    }

    [Fact]
    public async Task Duplicate_Group_Number_Forces_NoGo()
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
        db.Set<Semester>().AddRange(
            new Semester { Id = 11, TenantId = 1, CourseId = 1, Number = 3, Name = "III", GroupId = 2, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 12, TenantId = 1, CourseId = 1, Number = 3, Name = "III-dup", GroupId = 2, CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var report = await svc.BuildAsync();
        Assert.Equal(SemesterSchemaHardeningDecision.NoGo, report.Decision);
        Assert.False(report.UniqueReady);
        Assert.True(report.DuplicateKeyCount >= 1);
        Assert.Contains(report.ReadinessCodes, c => c == SemesterSchemaHardeningReadinessCodes.NotReadyDuplicates);
    }

    [Fact]
    public async Task Student_Hierarchy_Violation_Forces_NoGo()
    {
        var (db, svc) = CreateSut();
        db.Set<Course>().Add(new Course
        {
            Id = 1, TenantId = 1, Code = "BCOM", Name = "B.Com", DepartmentId = 1, CreatedDate = DateTime.UtcNow,
        });
        db.Set<Group>().AddRange(
            new Group { Id = 1, TenantId = 1, CourseId = 1, Code = "01", Name = "Fin", CreatedDate = DateTime.UtcNow },
            new Group { Id = 2, TenantId = 1, CourseId = 1, Code = "05", Name = "CA", CreatedDate = DateTime.UtcNow });
        db.Set<Semester>().Add(new Semester
        {
            Id = 11, TenantId = 1, CourseId = 1, Number = 3, Name = "III", GroupId = 2, CreatedDate = DateTime.UtcNow,
        });
        db.Set<Student>().Add(new Student
        {
            Id = 1, TenantId = 1, StudentNumber = "A1", Name = "A",
            CourseId = 1, GroupId = 1, SemesterId = 11,
            GenderId = 1, MediumId = 1, FirstLanguageId = 1, LanguageId = 1, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var report = await svc.BuildAsync();
        Assert.Equal(SemesterSchemaHardeningDecision.NoGo, report.Decision);
        Assert.True(report.StudentIntegrityViolationCount >= 1);
        Assert.Contains(report.ReadinessCodes, c => c == SemesterSchemaHardeningReadinessCodes.NotReadyStudentIntegrity);
    }

    [Fact]
    public async Task Section_Mismatch_Forces_NoGo()
    {
        var (db, svc) = CreateSut();
        db.Set<Course>().Add(new Course
        {
            Id = 1, TenantId = 1, Code = "BCOM", Name = "B.Com", DepartmentId = 1, CreatedDate = DateTime.UtcNow,
        });
        db.Set<Group>().AddRange(
            new Group { Id = 1, TenantId = 1, CourseId = 1, Code = "01", Name = "Fin", CreatedDate = DateTime.UtcNow },
            new Group { Id = 2, TenantId = 1, CourseId = 1, Code = "05", Name = "CA", CreatedDate = DateTime.UtcNow });
        db.Set<Semester>().Add(new Semester
        {
            Id = 11, TenantId = 1, CourseId = 1, Number = 3, Name = "III", GroupId = 2, CreatedDate = DateTime.UtcNow,
        });
        db.Set<Section>().Add(new Section
        {
            Id = 1, TenantId = 1, CollegeId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 1, SemesterId = 11,
            SectionCode = "A", SectionName = "A", CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var report = await svc.BuildAsync();
        Assert.False(report.IsReady);
        Assert.True(report.SectionIntegrityErrorCount >= 1);
    }

    [Fact]
    public async Task Clean_Dataset_Is_Go()
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
        db.Set<Student>().Add(new Student
        {
            Id = 1, TenantId = 1, StudentNumber = "A1", Name = "A",
            CourseId = 1, GroupId = 2, SemesterId = 11,
            GenderId = 1, MediumId = 1, FirstLanguageId = 1, LanguageId = 1, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var report = await svc.BuildAsync();
        Assert.Equal(0, report.NullGroupSemesterCount);
        Assert.Equal(0, report.DuplicateKeyCount);
        Assert.Equal(0, report.StudentIntegrityViolationCount);
        Assert.True(report.NotNullReady);
        Assert.True(report.UniqueReady);
        Assert.Equal(SemesterSchemaHardeningDecision.Go, report.Decision);
        Assert.True(report.IsReady);
        Assert.Equal(SemesterSchemaHardeningReadinessCodes.ReadyForSchemaHardening, report.DecisionCode);
        Assert.Equal("CLOSED", report.WildcardConsumerClosureStatus);
        Assert.Contains(report.ReadinessCodes, c => c == SemesterSchemaHardeningReadinessCodes.ReadyForSchemaHardening);
    }

    [Fact]
    public async Task Tenant_Isolation_Ignores_Other_Tenant_Null_Rows()
    {
        var (db, svc) = CreateSut();
        db.Set<Semester>().Add(new Semester
        {
            Id = 99, TenantId = 2, CourseId = 1, Number = 1, Name = "Other", GroupId = null, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var report = await svc.BuildAsync();
        Assert.DoesNotContain(report.NullGroupSemesters, r => r.SemesterId == 99);
    }
}

public sealed class AiSchedCatalogTimetableP14Prompt3JSchemaHardeningArchitectureGuardTests
{
    [Fact]
    public void Service_Is_ReadOnly_Contract()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "SemesterSchemaHardeningReadinessService.cs"));
        Assert.Contains("P1-4-3J3", src, StringComparison.Ordinal);
        Assert.Contains("AsNoTracking", src, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChangesAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChanges(", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteInTransactionAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AlterColumn", src, StringComparison.Ordinal);
        Assert.DoesNotContain("IsRequired()", src, StringComparison.Ordinal);
        Assert.DoesNotContain("section.SemesterId =", src, StringComparison.Ordinal);
        Assert.DoesNotContain("tg.SemesterId =", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConflictEngine", src, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishAsync", src, StringComparison.Ordinal);
        Assert.Contains("ComputeReadinessCodes", src, StringComparison.Ordinal);
        Assert.Contains("READY_FOR_SCHEMA_HARDENING", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_And_Documentation_Exist()
    {
        var root = FindRepoRoot();
        var api = File.ReadAllText(Path.Combine(root, "Abhyanvaya.API", "Controllers", "SemesterController.cs"));
        Assert.Contains("schema-hardening-readiness", api, StringComparison.Ordinal);
        Assert.Contains("ISemesterSchemaHardeningReadinessService", api, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(
            root, "docs", "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3J_SCHEMA_HARDENING_READINESS.md")));
        Assert.True(File.Exists(Path.Combine(
            root, "docs", "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3J_FINAL_SCHEMA_HARDENING_READINESS.md")));
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
