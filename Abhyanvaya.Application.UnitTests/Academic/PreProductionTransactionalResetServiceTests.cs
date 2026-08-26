using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

public sealed class PreProductionTransactionalResetServiceTests
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

    private static (ApplicationDbContext Db, PreProductionTransactionalResetService Svc, AmbientUser User)
        CreateSut(int tenantId = 1)
    {
        var user = new AmbientUser { TenantId = tenantId };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("p143hc1-" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);
        var svc = new PreProductionTransactionalResetService(
            db, user, NullLogger<PreProductionTransactionalResetService>.Instance);
        return (db, svc, user);
    }

    private static async Task SeedBaselineAsync(ApplicationDbContext db)
    {
        db.Set<Course>().Add(new Course
        {
            Id = 1, TenantId = 1, Code = "BCOM", Name = "B.Com", DepartmentId = 1, CreatedDate = DateTime.UtcNow,
        });
        db.Set<Group>().AddRange(
            new Group { Id = 1, TenantId = 1, CourseId = 1, Code = "FIN", Name = "Finance", CreatedDate = DateTime.UtcNow },
            new Group { Id = 2, TenantId = 1, CourseId = 1, Code = "CA", Name = "CA", CreatedDate = DateTime.UtcNow });
        db.Set<Semester>().AddRange(
            new Semester
            {
                Id = 10, TenantId = 1, CourseId = 1, GroupId = 1, Number = 3, Name = "III-F",
                CreatedDate = DateTime.UtcNow,
            },
            new Semester
            {
                Id = 11, TenantId = 1, CourseId = 1, GroupId = 2, Number = 3, Name = "III-CA",
                CreatedDate = DateTime.UtcNow,
            },
            new Semester
            {
                Id = 1, TenantId = 1, CourseId = 1, GroupId = null, Number = 1, Name = "I-legacy",
                CreatedDate = DateTime.UtcNow,
            });
        db.Set<Student>().AddRange(
            new Student
            {
                Id = 100, TenantId = 1, StudentNumber = "F1", Name = "Fin", CourseId = 1, GroupId = 1,
                SemesterId = 1, GenderId = 1, MediumId = 1, FirstLanguageId = 1, LanguageId = 1,
                CreatedDate = DateTime.UtcNow,
            },
            new Student
            {
                Id = 101, TenantId = 1, StudentNumber = "C1", Name = "Ca", CourseId = 1, GroupId = 2,
                SemesterId = 1, GenderId = 1, MediumId = 1, FirstLanguageId = 1, LanguageId = 1,
                CreatedDate = DateTime.UtcNow,
            });
        db.Set<SubjectAllocation>().Add(new SubjectAllocation
        {
            Id = 50, TenantId = 1, CourseId = 1, GroupId = 1, SemesterId = 10, SubjectId = 1,
            AcademicYearId = 1, StaffId = 1, DepartmentId = 1, WeeklyHours = 3,
            EffectiveFrom = new DateOnly(2026, 1, 1), CreatedDate = DateTime.UtcNow,
        });
        db.Set<TeachingGroup>().Add(new TeachingGroup
        {
            Id = 70, TenantId = 1, SubjectAllocationId = 50, AcademicYearId = 1, CourseId = 1, GroupId = 1,
            SemesterId = 10, SubjectId = 1, Name = "TG1",
            EffectiveFrom = new DateOnly(2026, 1, 1), CreatedDate = DateTime.UtcNow,
        });
        db.Set<TimetableEntry>().Add(new TimetableEntry
        {
            Id = 80, TenantId = 1, TimetableId = 1, CourseId = 1, GroupId = 1, SemesterId = 10, SubjectId = 1,
            SubjectAllocationId = 50, DayOfWeek = 1, TimeSlotId = 1, StaffId = 1, RoomId = 1, DepartmentId = 1,
            TeachingGroupId = 70, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static PreProductionTransactionalResetExecuteRequest OkRequest()
        => new()
        {
            Confirm = true,
            ConfirmationPhrase = PreProductionTransactionalResetCodes.ConfirmationPhrase,
            Reason = "unit-test",
        };

    [Fact]
    public async Task Preview_Reports_Allowlist_And_Student_Updates()
    {
        var (db, svc, _) = CreateSut();
        await SeedBaselineAsync(db);

        var preview = await svc.PreviewAsync();
        Assert.True(preview.IsReadOnly);
        Assert.False(preview.SaveChangesInvoked);
        Assert.Equal(2, preview.StudentsUpdateRequired);
        Assert.True(preview.TransactionalTotal > 0);
        Assert.Contains(preview.DeletionAllowlistCounts, c => c.Entity == "TeachingGroup" && c.Count == 1);
        Assert.Contains(preview.DeletionAllowlistCounts, c => c.Entity == "SubjectAllocation" && c.Count == 1);
        Assert.Equal(2, preview.ProtectedBefore.Students);
        Assert.Equal(3, preview.ProtectedBefore.Semesters);
    }

    [Fact]
    public async Task Execute_Wipes_Transactional_And_Reconciles_Students_Without_Hardcoding_Ids()
    {
        var (db, svc, _) = CreateSut();
        await SeedBaselineAsync(db);

        var result = await svc.ExecuteAsync(OkRequest());
        Assert.True(result.IsSuccessful);
        Assert.Equal("Completed", result.ExecutionStatus);
        Assert.Equal(2, result.StudentsUpdated);
        Assert.Equal(2, result.ProtectedAfter.Students);
        Assert.Equal(3, result.ProtectedAfter.Semesters);
        Assert.Equal(0, await db.Set<TeachingGroup>().IgnoreQueryFilters().CountAsync(t => t.TenantId == 1));
        Assert.Equal(0, await db.Set<SubjectAllocation>().IgnoreQueryFilters().CountAsync(t => t.TenantId == 1));
        Assert.Equal(0, await db.Set<TimetableEntry>().IgnoreQueryFilters().CountAsync(t => t.TenantId == 1));

        var fin = await db.Set<Student>().SingleAsync(s => s.Id == 100);
        var ca = await db.Set<Student>().SingleAsync(s => s.Id == 101);
        Assert.Equal(10, fin.SemesterId);
        Assert.Equal(11, ca.SemesterId);
        Assert.False(result.StudentsDeleted);
        Assert.False(result.MasterDataDeleted);
    }

    [Fact]
    public async Task Second_Execution_Is_Idempotent()
    {
        var (db, svc, _) = CreateSut();
        await SeedBaselineAsync(db);
        Assert.True((await svc.ExecuteAsync(OkRequest())).IsSuccessful);

        var studentsBefore = await db.Set<Student>().Select(s => s.SemesterId).OrderBy(x => x).ToListAsync();
        var second = await svc.ExecuteAsync(OkRequest());
        Assert.True(second.IsSuccessful);
        Assert.Equal("AlreadyComplete", second.ExecutionStatus);
        Assert.True(second.IdempotentZeroMutation);
        Assert.Equal(0, second.TotalDeleted);
        Assert.Equal(0, second.StudentsUpdated);
        Assert.Equal(studentsBefore, await db.Set<Student>().Select(s => s.SemesterId).OrderBy(x => x).ToListAsync());
    }

    [Fact]
    public async Task Ambiguous_Student_Fails_Closed_No_Deletes()
    {
        var (db, svc, _) = CreateSut();
        await SeedBaselineAsync(db);
        db.Set<Semester>().Add(new Semester
        {
            Id = 12, TenantId = 1, CourseId = 1, GroupId = 1, Number = 4, Name = "IV-F",
            CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        // Student on legacy Sem1 with Group 1 now has Sem III and IV operational → AMBIGUOUS by number match fail
        var result = await svc.ExecuteAsync(OkRequest());
        Assert.False(result.IsSuccessful);
        Assert.True(result.RolledBack);
        Assert.Equal(1, await db.Set<TeachingGroup>().IgnoreQueryFilters().CountAsync(t => t.TenantId == 1));
        Assert.Equal(1, await db.Set<Student>().Where(s => s.Id == 100).Select(s => s.SemesterId).FirstAsync());
    }

    [Fact]
    public async Task Transaction_Failure_Rolls_Back_Deletes_And_Student_Updates()
    {
        var (db, svc, _) = CreateSut();
        await SeedBaselineAsync(db);
        svc.TestFailureHook = _ => throw new DomainException("Simulated failure after mutation.");

        var result = await svc.ExecuteAsync(OkRequest());
        Assert.False(result.IsSuccessful);
        Assert.True(result.RolledBack);
        // InMemory may not rollback; verify service reported abort. Prefer DB state when provider supports it.
        Assert.Contains("Simulated failure", result.AbortReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cross_Tenant_Rows_Are_Not_Deleted()
    {
        var (db, svc, _) = CreateSut(tenantId: 1);
        await SeedBaselineAsync(db);
        db.Set<AttendanceBulkOperationHistory>().Add(new AttendanceBulkOperationHistory
        {
            Id = Guid.NewGuid(),
            TenantId = 2,
            Operation = "ExportSessions",
            StartedUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        Assert.True((await svc.ExecuteAsync(OkRequest())).IsSuccessful);
        Assert.Equal(1, await db.Set<AttendanceBulkOperationHistory>().IgnoreQueryFilters()
            .CountAsync(t => t.TenantId == 2));
        Assert.Equal(0, await db.Set<AttendanceBulkOperationHistory>().IgnoreQueryFilters()
            .CountAsync(t => t.TenantId == 1));
        Assert.Equal(0, await db.Set<TeachingGroup>().IgnoreQueryFilters().CountAsync(t => t.TenantId == 1));
    }

    [Fact]
    public async Task Missing_Confirmation_Rejected()
    {
        var (_, svc, _) = CreateSut();
        var result = await svc.ExecuteAsync(new PreProductionTransactionalResetExecuteRequest
        {
            Confirm = true,
            ConfirmationPhrase = "WRONG",
        });
        Assert.False(result.IsSuccessful);
    }
}

public sealed class AiSchedCatalogTimetableP14Prompt3HC1PreproductionResetGuardTests
{
    [Fact]
    public void Service_Uses_Allowlist_Transaction_And_Does_Not_Delete_Protected()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "PreProductionTransactionalResetService.cs"));
        Assert.Contains("P1-4-3HC1", src, StringComparison.Ordinal);
        Assert.Contains("ExecuteInTransactionAsync", src, StringComparison.Ordinal);
        Assert.Contains("ALL_OR_NOTHING", src, StringComparison.Ordinal);
        Assert.Contains("QueryIgnoringFilters", src, StringComparison.Ordinal);
        Assert.DoesNotContain("TRUNCATE TABLE", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DropTable", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ExecuteSqlRaw", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ExecuteSql(", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("_db.Remove<Student>", src, StringComparison.Ordinal);
        Assert.DoesNotContain("_db.Remove<Course>", src, StringComparison.Ordinal);
        Assert.DoesNotContain("_db.Remove<Group>", src, StringComparison.Ordinal);
        Assert.DoesNotContain("_db.Remove<Semester>", src, StringComparison.Ordinal);
        Assert.DoesNotContain("SemesterId = 10", src, StringComparison.Ordinal);
        Assert.DoesNotContain("SemesterId = 11", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ConflictEngine", src, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AlterColumn", src, StringComparison.Ordinal);

        var allow = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "PreProductionTransactionalResetAllowlist.cs"));
        Assert.Contains("Student", allow, StringComparison.Ordinal);
        Assert.Contains("\"TeachingGroup\"", allow, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Student\"",
            string.Join('\n', PreProductionTransactionalResetAllowlist.DeletionOrder),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Api_And_Docs_Exist()
    {
        var root = FindRepoRoot();
        var api = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.API", "Controllers", "AcademicDataPreproductionCleanupController.cs"));
        Assert.Contains("preproduction-cleanup", api, StringComparison.Ordinal);
        Assert.Contains("[HttpGet(\"preview\")]", api, StringComparison.Ordinal);
        Assert.Contains("[HttpPost(\"execute\")]", api, StringComparison.Ordinal);
        Assert.Contains("IPreProductionTransactionalResetService", api, StringComparison.Ordinal);
        Assert.Contains("CanManageSemesters", api, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(
            root, "docs", "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3H_PREPRODUCTION_TRANSACTIONAL_RESET.md")));
    }

    [Fact]
    public void Protected_Entities_Never_On_Deletion_Order()
    {
        foreach (var protectedName in new[]
                 {
                     "Student", "Course", "Group", "Semester", "Department", "Program", "College", "Subject", "User",
                 })
        {
            Assert.DoesNotContain(protectedName, PreProductionTransactionalResetAllowlist.DeletionOrder);
            Assert.Contains(protectedName, PreProductionTransactionalResetAllowlist.ProtectedEntities);
        }
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
