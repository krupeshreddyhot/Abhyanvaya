using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

public sealed class SemesterIiiSplitStudentRemapMigrationServiceTests
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

    private static (ApplicationDbContext Db, SemesterIiiSplitStudentRemapMigrationService Svc, AmbientUser User) CreateSut(
        Func<LegacySemesterMigrationDecisionPlanDto>? planFactory = null)
    {
        var user = new AmbientUser();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("p143b-" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);

        var plan = planFactory?.Invoke() ?? new LegacySemesterMigrationDecisionPlanDto
        {
            MatchesPrompt2BBaseline = true,
            IsReadOnly = true,
            TenantId = 1,
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
                    Decision = LegacySemesterMigrationDecision.Split,
                    DecisionCode = "SPLIT",
                    DecisionReason = "test",
                    TargetGroupIds = [1, 2],
                    StudentCountsByTargetGroup = new Dictionary<int, int> { [1] = 60, [2] = 236 },
                    RequiresManualApproval = true,
                },
            ],
        };

        var planMock = new Mock<ILegacySemesterMigrationDecisionPlanService>();
        planMock.Setup(p => p.BuildDecisionPlanAsync(It.IsAny<CancellationToken>())).ReturnsAsync(plan);

        var svc = new SemesterIiiSplitStudentRemapMigrationService(
            db, user, planMock.Object, NullLogger<SemesterIiiSplitStudentRemapMigrationService>.Instance);
        return (db, svc, user);
    }

    private static async Task SeedBaselineAsync(ApplicationDbContext db, int financeCount = 60, int caCount = 236)
    {
        db.Set<Course>().Add(new Course { Id = 1, TenantId = 1, Code = "BCOM", Name = "B.Com", DepartmentId = 1, CreatedDate = DateTime.UtcNow });
        db.Set<Group>().AddRange(
            new Group { Id = 1, TenantId = 1, CourseId = 1, Code = "13", Name = "FINANCE", CreatedDate = DateTime.UtcNow },
            new Group { Id = 2, TenantId = 1, CourseId = 1, Code = "05", Name = "COMPUTER APPLICATIONS", CreatedDate = DateTime.UtcNow });
        db.Set<Semester>().AddRange(
            new Semester { Id = 1, TenantId = 1, CourseId = 1, Number = 1, Name = "Semester I", GroupId = null, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 2, TenantId = 1, CourseId = 1, Number = 2, Name = "Semester II", GroupId = null, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 3, TenantId = 1, CourseId = 1, Number = 3, Name = "Semester III", GroupId = null, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 4, TenantId = 1, CourseId = 1, Number = 4, Name = "Semester VI", GroupId = null, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 5, TenantId = 1, CourseId = 1, Number = 4, Name = "Semester V", GroupId = null, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 9, TenantId = 1, CourseId = 1, Number = 4, Name = "Semester IV", GroupId = 2, CreatedDate = DateTime.UtcNow });

        var students = new List<Student>();
        for (var i = 0; i < financeCount; i++)
        {
            students.Add(new Student
            {
                Id = i + 1,
                TenantId = 1,
                StudentNumber = $"F{i}",
                Name = $"F{i}",
                CourseId = 1,
                GroupId = 1,
                SemesterId = 3,
                GenderId = 1,
                MediumId = 1,
                FirstLanguageId = 1,
                LanguageId = 1,
                CreatedDate = DateTime.UtcNow,
            });
        }

        for (var i = 0; i < caCount; i++)
        {
            students.Add(new Student
            {
                Id = financeCount + i + 1,
                TenantId = 1,
                StudentNumber = $"C{i}",
                Name = $"C{i}",
                CourseId = 1,
                GroupId = 2,
                SemesterId = 3,
                GenderId = 1,
                MediumId = 1,
                FirstLanguageId = 1,
                LanguageId = 1,
                CreatedDate = DateTime.UtcNow,
            });
        }

        db.Set<Student>().AddRange(students);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Execute_Creates_Targets_And_Remaps_296_Students()
    {
        var (db, svc, _) = CreateSut();
        await SeedBaselineAsync(db);

        var result = await svc.ExecuteAsync();

        Assert.Equal("Completed", result.Status);
        Assert.False(result.RolledBack);
        Assert.True(result.FinanceSemesterCreated);
        Assert.True(result.CaSemesterCreated);
        Assert.Equal(60, result.FinanceStudentsRemapped);
        Assert.Equal(236, result.CaStudentsRemapped);
        Assert.Equal(296, result.TotalStudentsRemapped);

        var legacy = await db.Set<Semester>().AsNoTracking().SingleAsync(s => s.Id == 3);
        Assert.Null(legacy.GroupId);

        var s9 = await db.Set<Semester>().AsNoTracking().SingleAsync(s => s.Id == 9);
        Assert.Equal(2, s9.GroupId);

        Assert.Equal(0, await db.Set<Student>().CountAsync(s => s.SemesterId == 3));
        Assert.Equal(60, await db.Set<Student>().CountAsync(s => s.SemesterId == result.FinanceTargetSemesterId && s.GroupId == 1));
        Assert.Equal(236, await db.Set<Student>().CountAsync(s => s.SemesterId == result.CaTargetSemesterId && s.GroupId == 2));
    }

    [Fact]
    public async Task Execute_Is_Idempotent_When_Already_Completed()
    {
        var (db, svc, _) = CreateSut();
        await SeedBaselineAsync(db);
        var first = await svc.ExecuteAsync();
        Assert.Equal("Completed", first.Status);

        // Second run: decision plan no longer matches Prompt 2B (legacy Sem III empty),
        // but AlreadyCompleted must still succeed.
        var planMock = new Mock<ILegacySemesterMigrationDecisionPlanService>();
        planMock.Setup(p => p.BuildDecisionPlanAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySemesterMigrationDecisionPlanDto
            {
                MatchesPrompt2BBaseline = false,
                Decisions = [],
            });
        var user = new AmbientUser();
        var svcIdem = new SemesterIiiSplitStudentRemapMigrationService(
            db, user, planMock.Object, NullLogger<SemesterIiiSplitStudentRemapMigrationService>.Instance);

        var second = await svcIdem.ExecuteAsync();
        Assert.Equal("AlreadyCompleted", second.Status);
        Assert.Equal(0, second.TotalStudentsRemapped);
        Assert.Equal(2, await db.Set<Semester>().CountAsync(s => s.Number == 3 && s.GroupId != null));
    }

    [Fact]
    public async Task Execute_Aborts_When_Student_Count_Differs()
    {
        var (db, svc, _) = CreateSut();
        await SeedBaselineAsync(db, financeCount: 59, caCount: 236);

        var result = await svc.ExecuteAsync();
        Assert.Equal("Aborted", result.Status);
        Assert.True(result.RolledBack);
        Assert.Equal(0, await db.Set<Semester>().CountAsync(s => s.Number == 3 && s.GroupId != null));
        Assert.Equal(295, await db.Set<Student>().CountAsync(s => s.SemesterId == 3));
    }

    [Fact]
    public async Task Execute_Aborts_When_Baseline_Mismatch()
    {
        var (db, svc, _) = CreateSut(() => new LegacySemesterMigrationDecisionPlanDto
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
                    Decision = LegacySemesterMigrationDecision.Split,
                    DecisionCode = "SPLIT",
                    DecisionReason = "test",
                    StudentCountsByTargetGroup = new Dictionary<int, int> { [1] = 60, [2] = 236 },
                },
            ],
        });
        await SeedBaselineAsync(db);

        var result = await svc.ExecuteAsync();
        Assert.Equal("Aborted", result.Status);
        Assert.Contains("baseline", result.AbortReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Execute_Reuses_Existing_Target_Semesters()
    {
        var (db, svc, _) = CreateSut();
        await SeedBaselineAsync(db);
        db.Set<Semester>().AddRange(
            new Semester { Id = 10, TenantId = 1, CourseId = 1, Number = 3, Name = "Semester III", GroupId = 1, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 11, TenantId = 1, CourseId = 1, Number = 3, Name = "Semester III", GroupId = 2, CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var result = await svc.ExecuteAsync();

        Assert.Equal("Completed", result.Status);
        Assert.True(result.FinanceSemesterReused);
        Assert.True(result.CaSemesterReused);
        Assert.False(result.FinanceSemesterCreated);
        Assert.False(result.CaSemesterCreated);
        Assert.Equal(10, result.FinanceTargetSemesterId);
        Assert.Equal(11, result.CaTargetSemesterId);
        Assert.Equal(296, result.TotalStudentsRemapped);
    }

    [Fact]
    public async Task Execute_Aborts_When_Duplicate_Target_Semesters()
    {
        var (db, svc, _) = CreateSut();
        await SeedBaselineAsync(db);
        db.Set<Semester>().AddRange(
            new Semester { Id = 10, TenantId = 1, CourseId = 1, Number = 3, Name = "Semester III", GroupId = 1, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 12, TenantId = 1, CourseId = 1, Number = 3, Name = "Semester III Dup", GroupId = 1, CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var result = await svc.ExecuteAsync();
        Assert.Equal("Aborted", result.Status);
        Assert.Contains("Multiple", result.AbortReason!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(296, await db.Set<Student>().CountAsync(s => s.SemesterId == 3));
    }

    [Fact]
    public async Task Execute_Aborts_When_Cross_Course_Group()
    {
        var (db, svc, _) = CreateSut();
        await SeedBaselineAsync(db);
        db.Set<Course>().Add(new Course { Id = 99, TenantId = 1, Code = "OTHER", Name = "Other", DepartmentId = 1, CreatedDate = DateTime.UtcNow });
        var finance = await db.Set<Group>().SingleAsync(g => g.Id == 1);
        finance.CourseId = 99;
        await db.SaveChangesAsync();

        var result = await svc.ExecuteAsync();
        Assert.Equal("Aborted", result.Status);
        Assert.Contains("Course", result.AbortReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Execute_Aborts_When_Student_Has_Invalid_GroupId()
    {
        var (db, svc, _) = CreateSut();
        await SeedBaselineAsync(db);
        var victim = await db.Set<Student>().FirstAsync(s => s.GroupId == 1);
        victim.GroupId = 0;
        // Keep total 296 by converting one Finance to invalid — distribution will fail closed
        await db.SaveChangesAsync();

        var result = await svc.ExecuteAsync();
        Assert.Equal("Aborted", result.Status);
        Assert.True(
            result.AbortReason!.Contains("Group", StringComparison.OrdinalIgnoreCase)
            || result.AbortReason.Contains("distribution", StringComparison.OrdinalIgnoreCase)
            || result.AbortReason.Contains("invalid", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(296, await db.Set<Student>().CountAsync(s => s.SemesterId == 3));
    }

    [Fact]
    public async Task Execute_Does_Not_Mutate_Downstream_Reference_Counts()
    {
        var (db, svc, _) = CreateSut();
        await SeedBaselineAsync(db);
        db.Set<Subject>().Add(new Subject
        {
            Id = 1,
            TenantId = 1,
            TenantSubjectId = 1,
            CourseId = 1,
            GroupId = 2,
            SemesterId = 3,
            CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var before = await db.Set<Subject>().CountAsync(s => s.SemesterId == 3);
        var result = await svc.ExecuteAsync();
        Assert.Equal("Completed", result.Status);
        Assert.Equal(before, await db.Set<Subject>().CountAsync(s => s.SemesterId == 3));
        Assert.Equal(1, result.DownstreamSubjectReferences);
    }
}

public sealed class AiSchedCatalogTimetableP14Prompt3BSemesterIiiSplitGuardTests
{
    [Fact]
    public void Migration_Service_Does_Not_Mutate_Downstream_Entities()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "SemesterIiiSplitStudentRemapMigrationService.cs"));
        Assert.Contains("ExecuteInTransactionAsync", src, StringComparison.Ordinal);
        Assert.Contains("Student.GroupId", src, StringComparison.Ordinal);
        Assert.DoesNotContain("TimetableSection", src, StringComparison.Ordinal);
        Assert.DoesNotContain("TeachingGroupSection", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ConflictEngine", src, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishAsync", src, StringComparison.Ordinal);
        // Counts only — no entity updates for these sets
        Assert.Contains("SnapshotDownstreamAsync", src, StringComparison.Ordinal);
        Assert.Contains("AttendanceSessions.CountAsync", src, StringComparison.Ordinal);
        Assert.Contains("Subjects.CountAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("_db.Subjects.Update", src, StringComparison.Ordinal);
        Assert.DoesNotContain("Subjects.Remove", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AttendanceSessions.Update", src, StringComparison.Ordinal);
        Assert.DoesNotContain("SchedulingTimetableEntries.Add", src, StringComparison.Ordinal);
        Assert.DoesNotContain("SchedulingTeachingGroups.Add", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_Does_Not_Assign_Null_Group_On_Create()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "SemesterIiiSplitStudentRemapMigrationService.cs"));
        Assert.Contains("GroupId = ownership.AlignedGroupId", src, StringComparison.Ordinal);
        Assert.DoesNotContain("GroupId = null", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_Is_Explicit_Migration_Endpoint_Not_Generic_Student_Put()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(root, "Abhyanvaya.API", "Controllers", "SemesterController.cs"));
        Assert.Contains("migrations/semester-iii-split-student-remap", src, StringComparison.Ordinal);
        Assert.DoesNotContain("students/{id}/semester", src, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Frozen_Boundaries()
    {
        Assert.Equal(typeof(int?), typeof(Semester).GetProperty(nameof(Semester.GroupId))!.PropertyType);
        Assert.NotNull(typeof(TimetableEntry).GetProperty(nameof(TimetableEntry.TeachingGroupId)));
        Assert.Null(typeof(TimetableEntry).GetProperty("SectionId"));
        Assert.Equal(typeof(int), typeof(Course).GetProperty(nameof(Course.DepartmentId))!.PropertyType);
        Assert.NotNull(typeof(TenantAcademicConfiguration).GetProperty(nameof(TenantAcademicConfiguration.EnablePrograms)));
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
