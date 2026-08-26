using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

public sealed class StudentSemesterOwnershipRulesTests
{
    [Fact]
    public void Accepts_Matching_Course_Group_Semester()
    {
        var d = StudentSemesterOwnershipRules.EvaluateWrite(
            1, 1, 2, 11,
            new StudentSemesterOwnershipRules.GroupSnapshot(2, 1, 1, false),
            new StudentSemesterOwnershipRules.SemesterSnapshot(11, 1, 1, 2, false, false));
        Assert.True(d.Accepted);
    }

    [Fact]
    public void Rejects_Semester_For_Other_Group()
    {
        var d = StudentSemesterOwnershipRules.EvaluateWrite(
            1, 1, 1, 11,
            new StudentSemesterOwnershipRules.GroupSnapshot(1, 1, 1, false),
            new StudentSemesterOwnershipRules.SemesterSnapshot(11, 1, 1, 2, false, false));
        Assert.False(d.Accepted);
        Assert.Contains("Group", d.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_Legacy_Null_Group_Semester()
    {
        var d = StudentSemesterOwnershipRules.EvaluateWrite(
            1, 1, 1, 3,
            new StudentSemesterOwnershipRules.GroupSnapshot(1, 1, 1, false),
            new StudentSemesterOwnershipRules.SemesterSnapshot(3, 1, 1, null, false, false));
        Assert.False(d.Accepted);
        Assert.Contains("Group-specific", d.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_Cross_Course_Group()
    {
        var d = StudentSemesterOwnershipRules.EvaluateWrite(
            1, 1, 2, 11,
            new StudentSemesterOwnershipRules.GroupSnapshot(2, 1, 99, false),
            new StudentSemesterOwnershipRules.SemesterSnapshot(11, 1, 1, 2, false, false));
        Assert.False(d.Accepted);
    }

    [Fact]
    public void Rejects_Cross_Tenant_Semester()
    {
        var d = StudentSemesterOwnershipRules.EvaluateWrite(
            1, 1, 2, 11,
            new StudentSemesterOwnershipRules.GroupSnapshot(2, 1, 1, false),
            new StudentSemesterOwnershipRules.SemesterSnapshot(11, 9, 1, 2, false, false));
        Assert.False(d.Accepted);
        Assert.Contains("tenant", d.Error!, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class SemesterPostMigrationIntegrityAuditServiceTests
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

    private static (ApplicationDbContext Db, SemesterPostMigrationIntegrityAuditService Svc) CreateSut(
        LegacySemesterMigrationDecisionPlanDto? plan = null)
    {
        var user = new AmbientUser();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("p143ba-" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);

        var planDto = plan ?? new LegacySemesterMigrationDecisionPlanDto
        {
            MatchesPrompt2BBaseline = false,
            Decisions =
            [
                new LegacySemesterMigrationDecisionRowDto
                {
                    SemesterId = 3,
                    CourseId = 1,
                    CourseName = "B.Com",
                    Number = 3,
                    Name = "Semester III",
                    CurrentGroupId = null,
                    Decision = LegacySemesterMigrationDecision.RetainLegacyPendingDecision,
                    DecisionCode = "RETAIN_LEGACY_PENDING_DECISION",
                    DecisionReason = "post-split",
                },
            ],
        };

        var planMock = new Mock<ILegacySemesterMigrationDecisionPlanService>();
        planMock.Setup(p => p.BuildDecisionPlanAsync(It.IsAny<CancellationToken>())).ReturnsAsync(planDto);

        var svc = new SemesterPostMigrationIntegrityAuditService(db, user, planMock.Object);
        return (db, svc);
    }

    private static async Task SeedHealthyPostSplitAsync(ApplicationDbContext db)
    {
        db.Set<Course>().Add(new Course { Id = 1, TenantId = 1, Code = "BCOM", Name = "B.Com", DepartmentId = 1, CreatedDate = DateTime.UtcNow });
        db.Set<Group>().AddRange(
            new Group { Id = 1, TenantId = 1, CourseId = 1, Code = "13", Name = "FINANCE", CreatedDate = DateTime.UtcNow },
            new Group { Id = 2, TenantId = 1, CourseId = 1, Code = "05", Name = "CA", CreatedDate = DateTime.UtcNow });
        db.Set<Semester>().AddRange(
            new Semester { Id = 1, TenantId = 1, CourseId = 1, Number = 1, Name = "Semester I", GroupId = null, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 3, TenantId = 1, CourseId = 1, Number = 3, Name = "Semester III", GroupId = null, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 10, TenantId = 1, CourseId = 1, Number = 3, Name = "Semester III", GroupId = 1, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 11, TenantId = 1, CourseId = 1, Number = 3, Name = "Semester III", GroupId = 2, CreatedDate = DateTime.UtcNow });

        for (var i = 0; i < 60; i++)
        {
            db.Set<Student>().Add(new Student
            {
                Id = i + 1, TenantId = 1, StudentNumber = $"F{i}", Name = $"F{i}",
                CourseId = 1, GroupId = 1, SemesterId = 10,
                GenderId = 1, MediumId = 1, FirstLanguageId = 1, LanguageId = 1, CreatedDate = DateTime.UtcNow,
            });
        }

        for (var i = 0; i < 236; i++)
        {
            db.Set<Student>().Add(new Student
            {
                Id = 1000 + i, TenantId = 1, StudentNumber = $"C{i}", Name = $"C{i}",
                CourseId = 1, GroupId = 2, SemesterId = 11,
                GenderId = 1, MediumId = 1, FirstLanguageId = 1, LanguageId = 1, CreatedDate = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Audit_Healthy_Post_Migration_Dataset()
    {
        var (db, svc) = CreateSut();
        await SeedHealthyPostSplitAsync(db);

        var report = await svc.BuildAuditAsync();

        Assert.True(report.IsReadOnly);
        Assert.True(report.IsHealthy);
        Assert.Equal(0, report.Summary.Critical);
        Assert.Equal(0, report.Summary.Errors);
        Assert.True(report.SemesterIiiSplit.MigratedStudentsFullyRemapped);
        Assert.Equal(10, report.SemesterIiiSplit.FinanceSemesterId);
        Assert.Equal(11, report.SemesterIiiSplit.CaSemesterId);
        Assert.Equal(0, report.SemesterIiiSplit.StudentsOnLegacySemesterIii);
    }

    [Fact]
    public async Task Audit_Detects_Student_Semester_Group_Mismatch()
    {
        var (db, svc) = CreateSut();
        await SeedHealthyPostSplitAsync(db);
        var victim = await db.Set<Student>().FirstAsync(s => s.GroupId == 1);
        victim.SemesterId = 11;
        await db.SaveChangesAsync();

        var report = await svc.BuildAuditAsync();
        Assert.False(report.IsHealthy);
        Assert.Contains(report.Violations, v => v.Code == "STUDENT_SEMESTER_GROUP_MISMATCH");
    }

    [Fact]
    public async Task Audit_Detects_Duplicate_Group_Semester_Number()
    {
        var (db, svc) = CreateSut();
        await SeedHealthyPostSplitAsync(db);
        db.Set<Semester>().Add(new Semester
        {
            Id = 12, TenantId = 1, CourseId = 1, Number = 3, Name = "Dup", GroupId = 1, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var report = await svc.BuildAuditAsync();
        Assert.Contains(report.Violations, v => v.Code == "DUPLICATE_GROUP_SEMESTER_NUMBER");
    }

    [Fact]
    public async Task Audit_Classifies_Legacy_Null_Group()
    {
        var (db, svc) = CreateSut();
        await SeedHealthyPostSplitAsync(db);
        var report = await svc.BuildAuditAsync();
        Assert.Contains(report.Violations, v => v.Code == "LEGACY_COURSE_WIDE_SEMESTER");
        Assert.Contains(report.LegacySemesters, l => l.SemesterId == 3);
    }

    [Fact]
    public async Task Audit_Performs_Zero_Writes()
    {
        var (db, svc) = CreateSut();
        await SeedHealthyPostSplitAsync(db);
        var beforeStudents = await db.Set<Student>().AsNoTracking().Select(s => new { s.Id, s.SemesterId, s.UpdatedDate }).ToListAsync();
        var beforeSemesters = await db.Set<Semester>().AsNoTracking().Select(s => new { s.Id, s.GroupId, s.UpdatedDate }).ToListAsync();

        _ = await svc.BuildAuditAsync();
        _ = await svc.BuildAuditAsync();

        var afterStudents = await db.Set<Student>().AsNoTracking().Select(s => new { s.Id, s.SemesterId, s.UpdatedDate }).ToListAsync();
        var afterSemesters = await db.Set<Semester>().AsNoTracking().Select(s => new { s.Id, s.GroupId, s.UpdatedDate }).ToListAsync();
        Assert.Equal(beforeStudents, afterStudents);
        Assert.Equal(beforeSemesters, afterSemesters);
    }
}

public sealed class AiSchedCatalogTimetableP14Prompt3BAPostMigrationIntegrityGuardTests
{
    [Fact]
    public void Audit_Service_Is_Read_Only()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "SemesterPostMigrationIntegrityAuditService.cs"));
        Assert.Contains("AsNoTracking", src, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChanges", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AddAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("TimetableSection", src, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ConflictEngine", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Student_Ownership_Rules_Reject_Null_Group_Semester()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "StudentSemesterOwnershipRules.cs"));
        Assert.Contains("Group-specific", src, StringComparison.Ordinal);
        Assert.Contains("does not belong to the selected Group", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_Exposes_Read_Only_Integrity_Audit()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(root, "Abhyanvaya.API", "Controllers", "SemesterController.cs"));
        Assert.Contains("post-migration-integrity-audit", src, StringComparison.Ordinal);
        Assert.Contains("ISemesterPostMigrationIntegrityAuditService", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Student_Controller_Enforces_Ownership()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(root, "Abhyanvaya.API", "Controllers", "StudentController.cs"));
        Assert.Contains("ValidateStudentCatalogOwnershipAsync", src, StringComparison.Ordinal);
        Assert.Contains("StudentSemesterOwnershipRules", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Frozen_Boundaries_Unchanged()
    {
        Assert.Equal(typeof(int?), typeof(Semester).GetProperty(nameof(Semester.GroupId))!.PropertyType);
        Assert.NotNull(typeof(TimetableEntry).GetProperty(nameof(TimetableEntry.TeachingGroupId)));
        Assert.Null(typeof(TimetableEntry).GetProperty("SectionId"));
        Assert.Equal(typeof(int), typeof(Course).GetProperty(nameof(Course.DepartmentId))!.PropertyType);
        Assert.NotNull(typeof(TenantAcademicConfiguration).GetProperty(nameof(TenantAcademicConfiguration.EnablePrograms)));
    }

    [Fact]
    public void Documentation_Exists()
    {
        var root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(
            root, "docs", "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3B_A_POST_MIGRATION_INTEGRITY_AUDIT.md")));
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
