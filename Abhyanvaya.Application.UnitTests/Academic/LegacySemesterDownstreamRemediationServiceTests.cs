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

public sealed class LegacySemesterDownstreamRemediationServiceTests
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

    private static (ApplicationDbContext Db, LegacySemesterDownstreamRemediationService Svc) CreateSut()
    {
        var user = new AmbientUser();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("p143c-" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);

        var integrity = new Mock<ISemesterPostMigrationIntegrityAuditService>();
        integrity.Setup(i => i.BuildAuditAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SemesterPostMigrationIntegrityAuditDto
            {
                IsHealthy = true,
                Summary = new SemesterPostMigrationIntegritySummaryDto(),
            });

        var svc = new LegacySemesterDownstreamRemediationService(
            db, user, integrity.Object, NullLogger<LegacySemesterDownstreamRemediationService>.Instance);
        return (db, svc);
    }

    private static async Task SeedAsync(ApplicationDbContext db)
    {
        db.Set<Course>().Add(new Course { Id = 1, TenantId = 1, Code = "BCOM", Name = "B.Com", DepartmentId = 1, CreatedDate = DateTime.UtcNow });
        db.Set<Group>().AddRange(
            new Group { Id = 1, TenantId = 1, CourseId = 1, Code = "13", Name = "FINANCE", CreatedDate = DateTime.UtcNow },
            new Group { Id = 2, TenantId = 1, CourseId = 1, Code = "05", Name = "CA", CreatedDate = DateTime.UtcNow });
        db.Set<Semester>().AddRange(
            new Semester { Id = 3, TenantId = 1, CourseId = 1, Number = 3, Name = "Semester III", GroupId = null, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 10, TenantId = 1, CourseId = 1, Number = 3, Name = "Semester III", GroupId = 1, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 11, TenantId = 1, CourseId = 1, Number = 3, Name = "Semester III", GroupId = 2, CreatedDate = DateTime.UtcNow });

        db.Set<AttendanceSession>().AddRange(
            new AttendanceSession
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                TenantId = 1, CourseId = 1, GroupId = 1, SemesterId = 3, SubjectId = 1,
                AttendanceDate = DateTime.UtcNow.Date, CreatedUtc = DateTime.UtcNow,
            },
            new AttendanceSession
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                TenantId = 1, CourseId = 1, GroupId = 2, SemesterId = 3, SubjectId = 1,
                AttendanceDate = DateTime.UtcNow.Date, CreatedUtc = DateTime.UtcNow,
            });

        db.Set<SubjectAllocation>().Add(new SubjectAllocation
        {
            Id = 1, TenantId = 1, AcademicYearId = 1, SubjectId = 1, StaffId = 1,
            CourseId = 1, GroupId = 2, SemesterId = 3, DepartmentId = 1,
            WeeklyHours = 1, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow), CreatedDate = DateTime.UtcNow,
        });

        db.Set<TimetableEntry>().Add(new TimetableEntry
        {
            Id = 1, TenantId = 1, TimetableId = 1, DayOfWeek = 1, TimeSlotId = 1,
            SubjectAllocationId = 1, StaffId = 1, RoomId = 1, DepartmentId = 1,
            CourseId = 1, GroupId = 2, SemesterId = 3, SubjectId = 1, CreatedDate = DateTime.UtcNow,
        });

        db.Set<TeachingGroup>().AddRange(
            new TeachingGroup
            {
                Id = 1, TenantId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2, SemesterId = 3,
                SubjectId = 1, SubjectAllocationId = 1, Code = "TG1", Name = "TG1", CreatedDate = DateTime.UtcNow,
            },
            new TeachingGroup
            {
                Id = 2, TenantId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2, SemesterId = 3,
                SubjectId = 1, SubjectAllocationId = 1, Code = "TG2", Name = "TG2", CreatedDate = DateTime.UtcNow,
            });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Preview_Does_Not_Mutate()
    {
        var (db, svc) = CreateSut();
        await SeedAsync(db);

        var preview = await svc.PreviewAsync();
        Assert.True(preview.IsReadOnly);
        Assert.Equal(4, preview.Items.Count(i => i.Status == DownstreamRemediationStatus.Ready));
        Assert.Equal(2, preview.Items.Count(i => i.Status == DownstreamRemediationStatus.DeferredByArchitectureBoundary));
        Assert.Equal(2, await db.Set<AttendanceSession>().CountAsync(a => a.SemesterId == 3));
        Assert.Equal(1, await db.Set<SubjectAllocation>().CountAsync(a => a.SemesterId == 3));
        Assert.Equal(1, await db.Set<TimetableEntry>().CountAsync(e => e.SemesterId == 3));
        Assert.Equal(2, await db.Set<TeachingGroup>().CountAsync(t => t.SemesterId == 3));
    }

    [Fact]
    public async Task Execute_Remediates_Approved_Entities_And_Leaves_TeachingGroups()
    {
        var (db, svc) = CreateSut();
        await SeedAsync(db);

        var result = await svc.ExecuteAsync();
        Assert.Equal("Completed", result.ExecutionStatus);
        Assert.False(result.RolledBack);
        Assert.Equal(4, result.Summary.Remediated);
        Assert.Equal(2, result.Summary.DeferredByArchitectureBoundary);

        Assert.Equal(0, await db.Set<AttendanceSession>().CountAsync(a => a.SemesterId == 3));
        Assert.Equal(1, await db.Set<AttendanceSession>().CountAsync(a => a.SemesterId == 10 && a.GroupId == 1));
        Assert.Equal(1, await db.Set<AttendanceSession>().CountAsync(a => a.SemesterId == 11 && a.GroupId == 2));
        Assert.Equal(0, await db.Set<SubjectAllocation>().CountAsync(a => a.SemesterId == 3));
        Assert.Equal(1, await db.Set<SubjectAllocation>().CountAsync(a => a.SemesterId == 11));
        Assert.Equal(0, await db.Set<TimetableEntry>().CountAsync(e => e.SemesterId == 3));
        Assert.Equal(1, await db.Set<TimetableEntry>().CountAsync(e => e.SemesterId == 11));
        Assert.Equal(2, await db.Set<TeachingGroup>().CountAsync(t => t.SemesterId == 3));
    }

    [Fact]
    public async Task Execute_Is_Idempotent()
    {
        var (db, svc) = CreateSut();
        await SeedAsync(db);
        _ = await svc.ExecuteAsync();
        var second = await svc.ExecuteAsync();
        Assert.Equal("AlreadyComplete", second.ExecutionStatus);
        Assert.Equal(0, second.Summary.Remediated);
        Assert.Equal(2, second.Summary.DeferredByArchitectureBoundary);
    }

    [Fact]
    public async Task Execute_Skips_Course_Mismatch()
    {
        var (db, svc) = CreateSut();
        await SeedAsync(db);
        db.Set<Course>().Add(new Course { Id = 99, TenantId = 1, Code = "X", Name = "X", DepartmentId = 1, CreatedDate = DateTime.UtcNow });
        var a = await db.Set<AttendanceSession>().FirstAsync(x => x.GroupId == 1);
        a.CourseId = 99;
        await db.SaveChangesAsync();

        var result = await svc.ExecuteAsync();
        Assert.Contains(result.Items, i => i.Status == DownstreamRemediationStatus.ManualReviewRequired);
        Assert.Equal(3, a.SemesterId); // unchanged after reload
        a = await db.Set<AttendanceSession>().FirstAsync(x => x.Id == a.Id);
        Assert.Equal(3, a.SemesterId);
    }

    [Fact]
    public async Task Execute_Rejects_Duplicate_Target_Semester()
    {
        var (db, svc) = CreateSut();
        await SeedAsync(db);
        db.Set<Semester>().Add(new Semester
        {
            Id = 12, TenantId = 1, CourseId = 1, Number = 3, Name = "Dup", GroupId = 1, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await svc.ExecuteAsync();
        Assert.Contains(result.Items, i =>
            i.EntityType == "AttendanceSession"
            && i.GroupId == 1
            && i.Status == DownstreamRemediationStatus.ManualReviewRequired);
    }
}

public sealed class AiSchedCatalogTimetableP14Prompt3CDownstreamRemediationGuardTests
{
    [Fact]
    public void Service_Does_Not_Mutate_TeachingGroup_Or_TimetableSection()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "LegacySemesterDownstreamRemediationService.cs"));
        Assert.Contains("DEFERRED / IDENTIFY-ONLY", src, StringComparison.Ordinal);
        Assert.Contains("ExecuteInTransactionAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("TimetableSection", src, StringComparison.Ordinal);
        Assert.DoesNotContain("TeachingGroupSection", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ConflictEngine", src, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("PlacementSize", src, StringComparison.Ordinal);
        Assert.Contains("SchedulingTeachingGroups.AsNoTracking", src, StringComparison.Ordinal);
        Assert.Contains("MutationAllowed = false", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_Exposes_Audit_Preview_Execute()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(root, "Abhyanvaya.API", "Controllers", "SemesterController.cs"));
        Assert.Contains("downstream-remediation-audit", src, StringComparison.Ordinal);
        Assert.Contains("downstream-remediation-preview", src, StringComparison.Ordinal);
        Assert.Contains("downstream-remediation/execute", src, StringComparison.Ordinal);
        Assert.Contains("CanManageSemesters", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Scope_Lock_Approved_Entities_Only()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "LegacySemesterDownstreamRemediationService.cs"));
        Assert.Contains("AttendanceSessions", src, StringComparison.Ordinal);
        Assert.Contains("SchedulingSubjectAllocations", src, StringComparison.Ordinal);
        Assert.Contains("SchedulingTimetableEntries", src, StringComparison.Ordinal);
        Assert.DoesNotContain("_db.Students", src, StringComparison.Ordinal);
        Assert.DoesNotContain("Students.Where", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Frozen_Boundaries()
    {
        Assert.Equal(typeof(int?), typeof(Semester).GetProperty(nameof(Semester.GroupId))!.PropertyType);
        Assert.NotNull(typeof(TimetableEntry).GetProperty(nameof(TimetableEntry.TeachingGroupId)));
        Assert.Null(typeof(TimetableEntry).GetProperty("SectionId"));
        Assert.Equal(typeof(int), typeof(Course).GetProperty(nameof(Course.DepartmentId))!.PropertyType);
    }

    [Fact]
    public void Documentation_Exists()
    {
        var root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(
            root, "docs", "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3C_DOWNSTREAM_SEMESTER_REMEDIATION.md")));
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
