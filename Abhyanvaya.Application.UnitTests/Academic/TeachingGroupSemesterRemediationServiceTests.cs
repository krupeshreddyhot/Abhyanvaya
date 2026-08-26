using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

public sealed class TeachingGroupSemesterRemediationServiceTests
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

    private static (ApplicationDbContext Db, TeachingGroupSemesterRemediationService Svc) CreateSut(
        Action<Mock<ITimetableSectionProjector>>? configureProjector = null)
    {
        var user = new AmbientUser();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("p143f-" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);

        var projector = new Mock<ITimetableSectionProjector>();
        projector.Setup(p => p.SyncTeachingGroupSectionsToTimetableEntriesAsync(
                It.IsAny<int>(), It.IsAny<IReadOnlyList<int>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        configureProjector?.Invoke(projector);

        var integrity = new Mock<ISemesterPostMigrationIntegrityAuditService>();
        integrity.Setup(i => i.BuildAuditAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SemesterPostMigrationIntegrityAuditDto
            {
                IsHealthy = true,
                Summary = new SemesterPostMigrationIntegritySummaryDto(),
            });

        var finalization = new Mock<ILegacySemesterFinalizationAuditService>();
        finalization.Setup(f => f.BuildAuditAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySemesterFinalizationAuditDto
            {
                Summary = new LegacySemesterFinalizationSummaryDto
                {
                    LegacyNullGroupCount = 5,
                    TeachingGroupResidualCount = 0,
                },
            });

        var svc = new TeachingGroupSemesterRemediationService(
            db, user, projector.Object, integrity.Object, finalization.Object,
            NullLogger<TeachingGroupSemesterRemediationService>.Instance);
        return (db, svc);
    }

    private static async Task SeedCompatibleAsync(ApplicationDbContext db, bool withSection = true)
    {
        db.Set<Course>().Add(new Course
        {
            Id = 1, TenantId = 1, Code = "BCOM", Name = "B.Com", DepartmentId = 1, CreatedDate = DateTime.UtcNow,
        });
        db.Set<Group>().Add(new Group
        {
            Id = 2, TenantId = 1, CourseId = 1, Code = "05", Name = "CA", CreatedDate = DateTime.UtcNow,
        });
        db.Set<Semester>().AddRange(
            new Semester { Id = 3, TenantId = 1, CourseId = 1, Number = 3, Name = "Semester III", GroupId = null, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 11, TenantId = 1, CourseId = 1, Number = 3, Name = "Semester III", GroupId = 2, CreatedDate = DateTime.UtcNow });

        db.Set<SubjectAllocation>().Add(new SubjectAllocation
        {
            Id = 1, TenantId = 1, AcademicYearId = 1, SubjectId = 1, StaffId = 1,
            CourseId = 1, GroupId = 2, SemesterId = 11, DepartmentId = 1,
            WeeklyHours = 1, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow), CreatedDate = DateTime.UtcNow,
        });

        db.Set<TeachingGroup>().AddRange(
            new TeachingGroup
            {
                Id = 1, TenantId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2, SemesterId = 3,
                SubjectId = 1, SubjectAllocationId = 1, Code = "TG-PROOF-01", Name = "TG1",
                Status = TeachingGroupStatus.Active, CreatedDate = DateTime.UtcNow,
                EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            },
            new TeachingGroup
            {
                Id = 2, TenantId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2, SemesterId = 3,
                SubjectId = 1, SubjectAllocationId = 1, Code = "TG-PROOF-02", Name = "TG2",
                Status = TeachingGroupStatus.Active, CreatedDate = DateTime.UtcNow,
                EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            });

        if (withSection)
        {
            db.Set<Section>().Add(new Section
            {
                Id = 10, TenantId = 1, CollegeId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2,
                SemesterId = 11, SectionCode = "CA-A", SectionName = "CA A", CreatedDate = DateTime.UtcNow,
            });
            db.Set<TeachingGroupSection>().Add(new TeachingGroupSection
            {
                Id = 1, TenantId = 1, TeachingGroupId = 1, SectionId = 10, CreatedDate = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Preview_Zero_Writes_And_Ready_When_Compatible()
    {
        var (db, svc) = CreateSut();
        await SeedCompatibleAsync(db);

        var preview = await svc.PreviewAsync();
        Assert.True(preview.IsReadOnly);
        Assert.Equal("NotExecuted", preview.ExecutionStatus);
        Assert.True(preview.ExecutionSafe);
        Assert.Equal(2, preview.Items.Count(i => i.StatusKind == TeachingGroupSemesterRemediationStatus.Ready));
        Assert.Equal(3, await db.Set<TeachingGroup>().Where(t => t.Id == 1).Select(t => t.SemesterId).SingleAsync());
    }

    [Fact]
    public async Task Execute_Remediates_Both_TGs_Idempotently()
    {
        var (db, svc) = CreateSut();
        await SeedCompatibleAsync(db);

        var first = await svc.ExecuteAsync();
        Assert.Equal("Completed", first.ExecutionStatus);
        Assert.False(first.RolledBack);
        Assert.Equal(2, first.ChangedCount);
        Assert.Equal([3], first.OldSemesterIds);
        Assert.Equal([11], first.NewSemesterIds);
        Assert.Equal(11, (await db.Set<TeachingGroup>().FindAsync(1))!.SemesterId);
        Assert.Equal(11, (await db.Set<TeachingGroup>().FindAsync(2))!.SemesterId);
        Assert.Equal(1, await db.Set<TeachingGroupSection>().CountAsync());
        Assert.Equal(0, await db.Set<TeachingGroupMembership>().CountAsync());

        var second = await svc.ExecuteAsync();
        Assert.Equal("AlreadyComplete", second.ExecutionStatus);
        Assert.Equal(0, second.ChangedCount);
        Assert.Equal(2, second.AlreadyCompleteCount);
    }

    [Fact]
    public async Task Incompatible_Section_Semester_Fails_Closed()
    {
        var (db, svc) = CreateSut();
        await SeedCompatibleAsync(db, withSection: false);
        db.Set<Section>().Add(new Section
        {
            Id = 10, TenantId = 1, CollegeId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2,
            SemesterId = 3, SectionCode = "LEG", SectionName = "Legacy", CreatedDate = DateTime.UtcNow,
        });
        db.Set<TeachingGroupSection>().Add(new TeachingGroupSection
        {
            Id = 1, TenantId = 1, TeachingGroupId = 1, SectionId = 10, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var preview = await svc.PreviewAsync();
        Assert.False(preview.ExecutionSafe);
        Assert.Contains(preview.Items, i => i.StatusKind == TeachingGroupSemesterRemediationStatus.ManualReviewRequired);

        var exec = await svc.ExecuteAsync();
        Assert.Equal("Aborted", exec.ExecutionStatus);
        Assert.True(exec.RolledBack);
        Assert.Equal(3, (await db.Set<TeachingGroup>().FindAsync(1))!.SemesterId);
        Assert.Equal(3, (await db.Set<TeachingGroup>().FindAsync(2))!.SemesterId);
    }

    [Fact]
    public async Task Wrong_Target_Group_Is_Rejected()
    {
        var (db, svc) = CreateSut();
        await SeedCompatibleAsync(db);
        var tg = await db.Set<TeachingGroup>().FindAsync(1);
        tg!.GroupId = 99;
        await db.SaveChangesAsync();

        var preview = await svc.PreviewAsync();
        Assert.Contains(preview.Items.Where(i => i.TeachingGroupId == 1),
            i => i.StatusKind == TeachingGroupSemesterRemediationStatus.ManualReviewRequired);
    }

    [Fact]
    public async Task Null_Group_Target_Aborts()
    {
        var (db, svc) = CreateSut();
        await SeedCompatibleAsync(db);
        var sem = await db.Set<Semester>().FindAsync(11);
        sem!.GroupId = null;
        await db.SaveChangesAsync();

        var preview = await svc.PreviewAsync();
        Assert.Equal("Aborted", preview.ExecutionStatus);
        Assert.Contains("NULL-group", preview.AbortReason ?? "");
    }

    [Fact]
    public async Task Sa_On_Legacy_Requires_Manual_Review()
    {
        var (db, svc) = CreateSut();
        await SeedCompatibleAsync(db);
        var sa = await db.Set<SubjectAllocation>().FindAsync(1);
        sa!.SemesterId = 3;
        await db.SaveChangesAsync();

        var preview = await svc.PreviewAsync();
        Assert.True(preview.ManualReviewCount >= 1);
        Assert.False(preview.ExecutionSafe);
    }

    [Fact]
    public async Task Unexpected_Extra_Tg_On_Legacy_Aborts()
    {
        var (db, svc) = CreateSut();
        await SeedCompatibleAsync(db);
        db.Set<TeachingGroup>().Add(new TeachingGroup
        {
            Id = 99, TenantId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2, SemesterId = 3,
            SubjectId = 1, SubjectAllocationId = 1, Code = "X", Name = "Extra",
            Status = TeachingGroupStatus.Active, CreatedDate = DateTime.UtcNow,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
        });
        await db.SaveChangesAsync();

        var preview = await svc.PreviewAsync();
        Assert.Equal("Aborted", preview.ExecutionStatus);
        Assert.Contains("Unexpected", preview.AbortReason ?? "");
    }

    [Fact]
    public async Task Zero_Section_Tg_Is_Ready()
    {
        var (db, svc) = CreateSut();
        await SeedCompatibleAsync(db, withSection: false);

        var preview = await svc.PreviewAsync();
        Assert.True(preview.ExecutionSafe);
        Assert.All(preview.Items, i => Assert.Equal(TeachingGroupSemesterRemediationStatus.Ready, i.StatusKind));
    }
}

public sealed class AiSchedCatalogTimetableP14Prompt3FTeachingGroupSemesterRemediationArchitectureGuardTests
{
    [Fact]
    public void Service_Only_Allows_Approved_Tg_Ids_And_Preserves_Boundaries()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "TeachingGroupSemesterRemediationService.cs"));
        Assert.Contains("ApprovedTeachingGroupIds = [1, 2]", src, StringComparison.Ordinal);
        Assert.Contains("ExpectedTargetSemesterId = 11", src, StringComparison.Ordinal);
        Assert.Contains("ExecuteInTransactionAsync", src, StringComparison.Ordinal);
        Assert.Contains("ITimetableSectionProjector", src, StringComparison.Ordinal);
        Assert.Contains("ConcurrencyExceptionHelper.SaveChangesAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("StudentSections", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AttendanceSessions", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ConflictEngine", src, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishAsync", src, StringComparison.Ordinal);
        // Must not assign TeachingGroupSection properties
        Assert.DoesNotContain("TeachingGroupSection).", src, StringComparison.Ordinal);
        Assert.DoesNotContain("new TeachingGroupSection", src, StringComparison.Ordinal);
        Assert.DoesNotContain("new TeachingGroupMembership", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AddAsync(new TimetableSection", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AlterColumn", src, StringComparison.Ordinal);
        Assert.DoesNotContain("DropColumn", src, StringComparison.Ordinal);
        Assert.DoesNotContain("UNIQUE(TenantId", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_Endpoints_Exist_Under_CanManageSemesters()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(root, "Abhyanvaya.API", "Controllers", "SemesterController.cs"));
        Assert.Contains("teaching-group-remediation-preview", src, StringComparison.Ordinal);
        Assert.Contains("teaching-group-remediation/execute", src, StringComparison.Ordinal);
        Assert.Contains("ITeachingGroupSemesterRemediationService", src, StringComparison.Ordinal);
        Assert.Contains("CanManageSemesters", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Documentation_Exists()
    {
        var root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(
            root, "docs", "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3F_TEACHING_GROUP_SEMESTER_REMEDIATION.md")));
        Assert.True(File.Exists(Path.Combine(
            root, "docs", "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3F_REEXECUTION.md")));
    }

    [Fact]
    public void Reexecution_Doc_Records_Post_3G_Unblock_And_Stop()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(
            root, "docs", "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3F_REEXECUTION.md"));
        Assert.Contains("Prompt 3G", src, StringComparison.Ordinal);
        Assert.Contains("AlreadyComplete", src, StringComparison.Ordinal);
        Assert.Contains("ChangedCount", src, StringComparison.Ordinal);
        Assert.Contains("TG residual", src, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("## 9. STOP", src, StringComparison.Ordinal);
        Assert.Contains("Approved TG IDs:** **1**, **2**", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Frozen_Boundaries_Unchanged()
    {
        Assert.Equal(typeof(int?), typeof(Semester).GetProperty(nameof(Semester.GroupId))!.PropertyType);
        Assert.NotNull(typeof(TimetableEntry).GetProperty(nameof(TimetableEntry.TeachingGroupId)));
        Assert.Null(typeof(TimetableEntry).GetProperty("SectionId"));
        Assert.NotNull(typeof(TeachingGroupSection));
        Assert.NotNull(typeof(ITimetableSectionProjector));
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
