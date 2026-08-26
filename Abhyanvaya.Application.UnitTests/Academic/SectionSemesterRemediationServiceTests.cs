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

public sealed class SectionSemesterRemediationServiceTests
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

    private static (ApplicationDbContext Db, SectionSemesterRemediationService Svc, AmbientUser User) CreateSut(int tenantId = 1)
    {
        var user = new AmbientUser { TenantId = tenantId };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("p143g-" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);
        var svc = new SectionSemesterRemediationService(
            db, user, NullLogger<SectionSemesterRemediationService>.Instance);
        return (db, svc, user);
    }

    private static async Task SeedBaselineAsync(
        ApplicationDbContext db,
        bool includeFinanceSection = true,
        bool includeCaSection5 = true,
        int caSectionSemesterId = 3,
        bool includeTeachingGroupLink = true,
        int? wrongCourseCa = null,
        int? wrongGroupCa = null)
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

        if (includeFinanceSection)
        {
            db.Set<Section>().Add(new Section
            {
                Id = 4, TenantId = 1, CollegeId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 1,
                SemesterId = 3, SectionCode = "FIN-A", SectionName = "Finance A",
                Status = "Active", CreatedDate = DateTime.UtcNow,
            });
        }

        if (includeCaSection5)
        {
            db.Set<Section>().Add(new Section
            {
                Id = 5, TenantId = 1, CollegeId = 1, AcademicYearId = 1,
                CourseId = wrongCourseCa ?? 1,
                GroupId = wrongGroupCa ?? 2,
                SemesterId = caSectionSemesterId,
                SectionCode = "CA-A", SectionName = "CA A",
                Status = "Active", CreatedDate = DateTime.UtcNow,
            });
        }

        db.Set<TeachingGroup>().Add(new TeachingGroup
        {
            Id = 1, TenantId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2, SemesterId = 3,
            SubjectId = 1, SubjectAllocationId = 1, Code = "TG-PROOF-01", Name = "TG1",
            Status = TeachingGroupStatus.Active, CreatedDate = DateTime.UtcNow,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
        });

        if (includeTeachingGroupLink && includeCaSection5)
        {
            db.Set<TeachingGroupSection>().Add(new TeachingGroupSection
            {
                Id = 1, TenantId = 1, TeachingGroupId = 1, SectionId = 5, CreatedDate = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Preview_Is_ReadOnly_And_Discovers_Section_5()
    {
        var (db, svc, _) = CreateSut();
        await SeedBaselineAsync(db);

        var preview = await svc.PreviewAsync();

        Assert.True(preview.IsReadOnly);
        Assert.Equal("NotExecuted", preview.ExecutionStatus);
        Assert.Equal(0, preview.ChangedCount);
        Assert.Contains(5, preview.ApprovedSectionIds);
        Assert.Contains(preview.Items, i => i.SectionId == 5 && i.StatusKind == SectionSemesterRemediationStatus.Ready);
        Assert.Contains(preview.Items, i => i.SectionId == 4 && i.StatusKind == SectionSemesterRemediationStatus.Blocked);
        Assert.Equal(3, await db.Set<Section>().Where(s => s.Id == 5).Select(s => s.SemesterId).SingleAsync());
        Assert.Equal(0, await db.Set<LegacySemesterDispositionJournal>().CountAsync());
    }

    [Fact]
    public async Task Execute_Remediates_Only_Approved_Ca_Sections_Idempotently()
    {
        var (db, svc, _) = CreateSut();
        await SeedBaselineAsync(db);

        var first = await svc.ExecuteAsync();
        Assert.Equal("Completed", first.ExecutionStatus);
        Assert.False(first.RolledBack);
        Assert.Equal(1, first.ChangedCount);
        Assert.Equal([5], first.AffectedSectionIds.ToList());
        Assert.Equal(11, (await db.Set<Section>().FindAsync(5))!.SemesterId);
        Assert.Equal(3, (await db.Set<Section>().FindAsync(4))!.SemesterId);
        Assert.Equal(3, (await db.Set<TeachingGroup>().FindAsync(1))!.SemesterId);
        Assert.Equal(5, (await db.Set<TeachingGroupSection>().FindAsync(1))!.SectionId);
        Assert.Equal(1, await db.Set<LegacySemesterDispositionJournal>().CountAsync());

        var second = await svc.ExecuteAsync();
        Assert.Equal("AlreadyComplete", second.ExecutionStatus);
        Assert.Equal(0, second.ChangedCount);
        Assert.False(second.RolledBack);
        Assert.Equal(1, await db.Set<LegacySemesterDispositionJournal>().CountAsync());
    }

    [Fact]
    public async Task Execute_Does_Not_Mutate_Tg_Or_Tgs_Or_Student_Attendance_Sa_Tt()
    {
        var (db, svc, _) = CreateSut();
        await SeedBaselineAsync(db);
        db.Set<StudentSection>().Add(new StudentSection
        {
            Id = 1, TenantId = 1, StudentId = 1, SectionId = 5, IsCurrent = true,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow), CreatedDate = DateTime.UtcNow,
        });
        db.Set<SubjectAllocation>().Add(new SubjectAllocation
        {
            Id = 1, TenantId = 1, AcademicYearId = 1, SubjectId = 1, StaffId = 1,
            CourseId = 1, GroupId = 2, SemesterId = 11, DepartmentId = 1,
            WeeklyHours = 1, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow), CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var tgBefore = await db.Set<TeachingGroup>().AsNoTracking().Select(t => new { t.Id, t.SemesterId }).ToListAsync();
        var tgsBefore = await db.Set<TeachingGroupSection>().AsNoTracking().Select(x => new { x.Id, x.TeachingGroupId, x.SectionId }).ToListAsync();
        var ssBefore = await db.Set<StudentSection>().AsNoTracking().Select(x => new { x.Id, x.SectionId, x.IsCurrent }).ToListAsync();
        var saBefore = await db.Set<SubjectAllocation>().AsNoTracking().Select(x => new { x.Id, x.SemesterId }).ToListAsync();

        await svc.ExecuteAsync();

        var tgAfter = await db.Set<TeachingGroup>().AsNoTracking().Select(t => new { t.Id, t.SemesterId }).ToListAsync();
        var tgsAfter = await db.Set<TeachingGroupSection>().AsNoTracking().Select(x => new { x.Id, x.TeachingGroupId, x.SectionId }).ToListAsync();
        var ssAfter = await db.Set<StudentSection>().AsNoTracking().Select(x => new { x.Id, x.SectionId, x.IsCurrent }).ToListAsync();
        var saAfter = await db.Set<SubjectAllocation>().AsNoTracking().Select(x => new { x.Id, x.SemesterId }).ToListAsync();

        Assert.Equal(tgBefore, tgAfter);
        Assert.Equal(tgsBefore, tgsAfter);
        Assert.Equal(ssBefore, ssAfter);
        Assert.Equal(saBefore, saAfter);
        Assert.Empty(db.ChangeTracker.Entries<TimetableEntry>());
    }

    [Fact]
    public async Task Wrong_Course_Is_Rejected_Fail_Closed()
    {
        var (db, svc, _) = CreateSut();
        await SeedBaselineAsync(db, includeFinanceSection: false, wrongCourseCa: 99);

        var preview = await svc.PreviewAsync();
        Assert.False(preview.ExecutionSafe);
        Assert.Equal("Aborted", preview.ExecutionStatus);

        var exec = await svc.ExecuteAsync();
        Assert.Equal("Aborted", exec.ExecutionStatus);
        Assert.True(exec.RolledBack);
        Assert.Equal(3, (await db.Set<Section>().FindAsync(5))!.SemesterId);
    }

    [Fact]
    public async Task Wrong_Group_Is_Blocked_Out_Of_Approved_Set()
    {
        var (db, svc, _) = CreateSut();
        await SeedBaselineAsync(db, includeFinanceSection: false, wrongGroupCa: 1);

        var preview = await svc.PreviewAsync();
        Assert.Equal("Aborted", preview.ExecutionStatus);
        Assert.Contains("incompatible Course/Group", preview.AbortReason ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Wrong_Target_Semester_Baseline_Aborts()
    {
        var (db, svc, _) = CreateSut();
        await SeedBaselineAsync(db);
        var target = await db.Set<Semester>().FindAsync(11);
        target!.Number = 4;
        await db.SaveChangesAsync();

        var preview = await svc.PreviewAsync();
        Assert.Equal("Aborted", preview.ExecutionStatus);
        Assert.Contains("Number=", preview.AbortReason ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tenant_Isolation_Rejects_Cross_Tenant_Section()
    {
        var (db, svc, user) = CreateSut();
        await SeedBaselineAsync(db);
        db.Set<Section>().Add(new Section
        {
            Id = 50, TenantId = 2, CollegeId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2,
            SemesterId = 3, SectionCode = "X", SectionName = "X", Status = "Active", CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var preview = await svc.PreviewAsync();
        Assert.DoesNotContain(preview.Items, i => i.SectionId == 50);

        user.TenantId = 2;
        var other = await svc.PreviewAsync();
        Assert.Equal("Aborted", other.ExecutionStatus);
    }

    [Fact]
    public async Task Code_Collision_On_Target_Requires_Manual_Review_And_Rolls_Back()
    {
        var (db, svc, _) = CreateSut();
        await SeedBaselineAsync(db, includeFinanceSection: false);
        db.Set<Section>().Add(new Section
        {
            Id = 15, TenantId = 1, CollegeId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2,
            SemesterId = 11, SectionCode = "CA-A", SectionName = "Existing", Status = "Active", CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var exec = await svc.ExecuteAsync();
        Assert.Equal("Aborted", exec.ExecutionStatus);
        Assert.True(exec.RolledBack);
        Assert.Equal(0, exec.ChangedCount);
        Assert.Equal(3, (await db.Set<Section>().FindAsync(5))!.SemesterId);
        Assert.Contains(exec.Items, i => i.SectionId == 5 && i.StatusKind == SectionSemesterRemediationStatus.ManualReviewRequired);
    }

    [Fact]
    public async Task Missing_Required_Blocker_Section_Aborts()
    {
        var (db, svc, _) = CreateSut();
        await SeedBaselineAsync(db, includeFinanceSection: false, includeCaSection5: false);

        var preview = await svc.PreviewAsync();
        Assert.Equal("Aborted", preview.ExecutionStatus);
        Assert.Contains("Section Id=5", preview.AbortReason ?? "", StringComparison.Ordinal);
    }
}

public sealed class AiSchedCatalogTimetableP14Prompt3GSectionSemesterRemediationArchitectureGuardTests
{
    [Fact]
    public void Service_Only_Mutates_Approved_Section_SemesterId()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "SectionSemesterRemediationService.cs"));

        Assert.Contains("ExpectedLegacySemesterId = 3", src, StringComparison.Ordinal);
        Assert.Contains("ExpectedTargetSemesterId = 11", src, StringComparison.Ordinal);
        Assert.Contains("RequiredKnownBlockerSectionId = 5", src, StringComparison.Ordinal);
        Assert.Contains("section.SemesterId = ExpectedTargetSemesterId", src, StringComparison.Ordinal);
        Assert.Contains("ExecuteInTransactionAsync", src, StringComparison.Ordinal);
        Assert.Contains("ConcurrencyExceptionHelper.SaveChangesAsync", src, StringComparison.Ordinal);
        Assert.Contains("SECTION_SEMESTER_REMEDIATION", src, StringComparison.Ordinal);

        Assert.DoesNotContain("tg.SemesterId =", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("teachingGroup.SemesterId =", src, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("section.SemesterId = ExpectedTargetSemesterId", src, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(src, "section.SemesterId = ExpectedTargetSemesterId"));
        Assert.DoesNotContain("new TeachingGroup(", src, StringComparison.Ordinal);
        Assert.DoesNotContain("new TeachingGroupSection", src, StringComparison.Ordinal);
        Assert.DoesNotContain("new TeachingGroupMembership", src, StringComparison.Ordinal);
        Assert.DoesNotContain("new TimetableSection", src, StringComparison.Ordinal);
        Assert.DoesNotContain("new SubjectAllocation", src, StringComparison.Ordinal);
        Assert.DoesNotContain("new TimetableEntry", src, StringComparison.Ordinal);
        Assert.DoesNotContain("new Attendance", src, StringComparison.Ordinal);
        Assert.DoesNotContain("new StudentSection", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AddAsync(new Student", src, StringComparison.Ordinal);
        Assert.DoesNotContain("legacy.GroupId =", src, StringComparison.Ordinal);
        Assert.DoesNotContain("target.GroupId =", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ConflictEngine", src, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AlterColumn", src, StringComparison.Ordinal);
        Assert.DoesNotContain("DropColumn", src, StringComparison.Ordinal);
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

    [Fact]
    public void Api_Endpoints_Exist_Under_CanManageSemesters()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(root, "Abhyanvaya.API", "Controllers", "SemesterController.cs"));
        Assert.Contains("section-semester-remediation-preview", src, StringComparison.Ordinal);
        Assert.Contains("section-semester-remediation/execute", src, StringComparison.Ordinal);
        Assert.Contains("ISectionSemesterRemediationService", src, StringComparison.Ordinal);
        Assert.Contains("CanManageSemesters", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Documentation_Exists()
    {
        var root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(
            root, "docs", "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3G_SECTION_SEMESTER_REMEDIATION.md")));
    }

    [Fact]
    public void Frozen_Boundaries_Unchanged()
    {
        Assert.Equal(typeof(int?), typeof(Semester).GetProperty(nameof(Semester.GroupId))!.PropertyType);
        Assert.NotNull(typeof(Section).GetProperty(nameof(Section.SemesterId)));
        Assert.NotNull(typeof(TeachingGroupSection));
        Assert.Null(typeof(TimetableEntry).GetProperty("SectionId"));
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
