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
using Moq;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

public sealed class TeachingGroupRemediationReadinessServiceTests
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

    private static (ApplicationDbContext Db, TeachingGroupRemediationReadinessService Svc, Mock<ITeachingGroupSemesterRemediationService> Prompt3F)
        CreateSut(TeachingGroupSemesterRemediationResultDto? preview = null)
    {
        var user = new AmbientUser();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("p143h2-" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);

        var prompt3F = new Mock<ITeachingGroupSemesterRemediationService>();
        prompt3F.Setup(p => p.PreviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(preview ?? new TeachingGroupSemesterRemediationResultDto
            {
                IsReadOnly = true,
                ExecutionStatus = "Preview",
                ApprovedTeachingGroupIds = TeachingGroupSemesterRemediationService.ApprovedTeachingGroupIds,
                Items = [],
            });
        prompt3F.Setup(p => p.ExecuteAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Execute must not be called from readiness audit."));

        var svc = new TeachingGroupRemediationReadinessService(db, user, prompt3F.Object);
        return (db, svc, prompt3F);
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

    private static TeachingGroupSemesterRemediationItemDto Item(
        int id, TeachingGroupSemesterRemediationStatus kind, int currentSem, string reason = "")
        => new()
        {
            TeachingGroupId = id,
            TenantId = 1,
            CourseId = 1,
            GroupId = 2,
            CurrentSemesterId = currentSem,
            TargetSemesterId = 11,
            StatusKind = kind,
            StatusCode = kind.ToString().ToUpperInvariant(),
            Reason = reason,
            MutationAllowed = kind == TeachingGroupSemesterRemediationStatus.Ready,
        };

    [Fact]
    public async Task Audit_Is_ReadOnly_And_Idempotent()
    {
        var preview = new TeachingGroupSemesterRemediationResultDto
        {
            IsReadOnly = true,
            ExecutionStatus = "AlreadyComplete",
            Items =
            [
                Item(1, TeachingGroupSemesterRemediationStatus.AlreadyComplete, 11),
                Item(2, TeachingGroupSemesterRemediationStatus.AlreadyComplete, 11),
            ],
        };
        var (db, svc, prompt3F) = CreateSut(preview);
        await SeedTargetsAsync(db);
        db.Set<TeachingGroup>().AddRange(
            new TeachingGroup
            {
                Id = 1, TenantId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2, SemesterId = 11,
                SubjectId = 1, SubjectAllocationId = 1, Name = "TG1", Status = TeachingGroupStatus.Active,
                CreatedDate = DateTime.UtcNow, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            },
            new TeachingGroup
            {
                Id = 2, TenantId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2, SemesterId = 11,
                SubjectId = 1, SubjectAllocationId = 1, Name = "TG2", Status = TeachingGroupStatus.Active,
                CreatedDate = DateTime.UtcNow, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            });
        await db.SaveChangesAsync();

        var before = await db.Set<TeachingGroup>().AsNoTracking().Select(t => new { t.Id, t.SemesterId }).ToListAsync();
        var a = await svc.BuildAsync();
        var b = await svc.BuildAsync();

        Assert.True(a.IsReadOnly);
        Assert.False(a.SaveChangesInvoked);
        Assert.False(a.Prompt3FExecuteInvoked);
        Assert.Equal(a.IsHealthy, b.IsHealthy);
        prompt3F.Verify(p => p.PreviewAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        prompt3F.Verify(p => p.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(before, await db.Set<TeachingGroup>().AsNoTracking().Select(t => new { t.Id, t.SemesterId }).ToListAsync());
    }

    [Fact]
    public async Task Detects_Legacy_Section_References()
    {
        var (db, svc, _) = CreateSut();
        await SeedTargetsAsync(db);
        db.Set<Section>().Add(new Section
        {
            Id = 5, TenantId = 1, CollegeId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2,
            SemesterId = 3, SectionCode = "CA-A", SectionName = "CA A", Status = "Active", CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var report = await svc.BuildAsync();
        Assert.True(report.SectionLegacyReferenceCount >= 1);
        Assert.Contains(report.Findings, f => f.Code == "SECTION_LEGACY_SEMESTER");
    }

    [Fact]
    public async Task Compatible_Section_Recognized_And_Ready()
    {
        var preview = new TeachingGroupSemesterRemediationResultDto
        {
            IsReadOnly = true,
            ExecutionStatus = "Preview",
            Items =
            [
                Item(1, TeachingGroupSemesterRemediationStatus.Ready, 3, "Validated"),
                Item(2, TeachingGroupSemesterRemediationStatus.Ready, 3, "Validated"),
            ],
        };
        var (db, svc, _) = CreateSut(preview);
        await SeedTargetsAsync(db);
        db.Set<Section>().Add(new Section
        {
            Id = 5, TenantId = 1, CollegeId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2,
            SemesterId = 11, SectionCode = "CA-A", SectionName = "CA A", Status = "Active", CreatedDate = DateTime.UtcNow,
        });
        db.Set<TeachingGroup>().AddRange(
            new TeachingGroup
            {
                Id = 1, TenantId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2, SemesterId = 3,
                SubjectId = 1, SubjectAllocationId = 1, Name = "TG1", Status = TeachingGroupStatus.Active,
                CreatedDate = DateTime.UtcNow, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            },
            new TeachingGroup
            {
                Id = 2, TenantId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2, SemesterId = 3,
                SubjectId = 1, SubjectAllocationId = 1, Name = "TG2", Status = TeachingGroupStatus.Active,
                CreatedDate = DateTime.UtcNow, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            });
        db.Set<TeachingGroupSection>().Add(new TeachingGroupSection
        {
            Id = 1, TenantId = 1, TeachingGroupId = 1, SectionId = 5, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var report = await svc.BuildAsync();
        Assert.Contains(1, report.ReadyTeachingGroupIds);
        Assert.True(report.CanReExecuteTeachingGroupRemediation);
        var row = Assert.Single(report.TeachingGroups.Where(t => t.TeachingGroupId == 1));
        Assert.Equal(1, row.CompatibleSectionCount);
        Assert.Equal(0, row.IncompatibleSectionCount);
    }

    [Fact]
    public async Task Incompatible_Section_Blocks_Ready()
    {
        var preview = new TeachingGroupSemesterRemediationResultDto
        {
            IsReadOnly = true,
            ExecutionStatus = "Preview",
            Items =
            [
                Item(1, TeachingGroupSemesterRemediationStatus.Ready, 3, "Would be ready"),
                Item(2, TeachingGroupSemesterRemediationStatus.Ready, 3, "Would be ready"),
            ],
        };
        var (db, svc, _) = CreateSut(preview);
        await SeedTargetsAsync(db);
        db.Set<Section>().Add(new Section
        {
            Id = 5, TenantId = 1, CollegeId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2,
            SemesterId = 3, SectionCode = "CA-A", SectionName = "CA A", Status = "Active", CreatedDate = DateTime.UtcNow,
        });
        db.Set<TeachingGroup>().AddRange(
            new TeachingGroup
            {
                Id = 1, TenantId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2, SemesterId = 3,
                SubjectId = 1, SubjectAllocationId = 1, Name = "TG1", Status = TeachingGroupStatus.Active,
                CreatedDate = DateTime.UtcNow, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            },
            new TeachingGroup
            {
                Id = 2, TenantId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2, SemesterId = 3,
                SubjectId = 1, SubjectAllocationId = 1, Name = "TG2", Status = TeachingGroupStatus.Active,
                CreatedDate = DateTime.UtcNow, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            });
        db.Set<TeachingGroupSection>().Add(new TeachingGroupSection
        {
            Id = 1, TenantId = 1, TeachingGroupId = 1, SectionId = 5, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var report = await svc.BuildAsync();
        Assert.Contains(1, report.BlockedTeachingGroupIds);
        Assert.DoesNotContain(1, report.ReadyTeachingGroupIds);
        Assert.False(report.CanReExecuteTeachingGroupRemediation);
        Assert.Contains(report.Findings, f => f.Code == "TG_SECTION_INCOMPATIBLE");
    }

    [Fact]
    public async Task Already_Complete_Idempotent()
    {
        var preview = new TeachingGroupSemesterRemediationResultDto
        {
            IsReadOnly = true,
            ExecutionStatus = "AlreadyComplete",
            Items =
            [
                Item(1, TeachingGroupSemesterRemediationStatus.AlreadyComplete, 11),
                Item(2, TeachingGroupSemesterRemediationStatus.AlreadyComplete, 11),
            ],
        };
        var (db, svc, _) = CreateSut(preview);
        await SeedTargetsAsync(db);
        db.Set<TeachingGroup>().AddRange(
            new TeachingGroup
            {
                Id = 1, TenantId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2, SemesterId = 11,
                SubjectId = 1, SubjectAllocationId = 1, Name = "TG1", Status = TeachingGroupStatus.Active,
                CreatedDate = DateTime.UtcNow, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            },
            new TeachingGroup
            {
                Id = 2, TenantId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2, SemesterId = 11,
                SubjectId = 1, SubjectAllocationId = 1, Name = "TG2", Status = TeachingGroupStatus.Active,
                CreatedDate = DateTime.UtcNow, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            });
        await db.SaveChangesAsync();

        var report = await svc.BuildAsync();
        Assert.Equal(2, report.AlreadyCompleteTeachingGroupIds.Count);
        Assert.Empty(report.ReadyTeachingGroupIds);
        Assert.False(report.CanReExecuteTeachingGroupRemediation);
        Assert.True(report.IsHealthy);
    }

    [Fact]
    public async Task Target_Invalid_Never_Ready()
    {
        var preview = new TeachingGroupSemesterRemediationResultDto
        {
            IsReadOnly = true,
            Items = [Item(1, TeachingGroupSemesterRemediationStatus.Ready, 3)],
        };
        var (db, svc, _) = CreateSut(preview);
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
        Assert.False(report.TargetSemesterValidation.TargetValid);
        Assert.Empty(report.ReadyTeachingGroupIds);
        Assert.False(report.CanReExecuteTeachingGroupRemediation);
    }

    [Fact]
    public async Task Sa_Regression_On_Sem3_Blocks()
    {
        var preview = new TeachingGroupSemesterRemediationResultDto
        {
            IsReadOnly = true,
            Items =
            [
                Item(1, TeachingGroupSemesterRemediationStatus.AlreadyComplete, 11),
                Item(2, TeachingGroupSemesterRemediationStatus.AlreadyComplete, 11),
            ],
        };
        var (db, svc, _) = CreateSut(preview);
        await SeedTargetsAsync(db);
        db.Set<TeachingGroup>().AddRange(
            new TeachingGroup
            {
                Id = 1, TenantId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2, SemesterId = 11,
                SubjectId = 1, SubjectAllocationId = 1, Name = "TG1", Status = TeachingGroupStatus.Active,
                CreatedDate = DateTime.UtcNow, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            },
            new TeachingGroup
            {
                Id = 2, TenantId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2, SemesterId = 11,
                SubjectId = 1, SubjectAllocationId = 1, Name = "TG2", Status = TeachingGroupStatus.Active,
                CreatedDate = DateTime.UtcNow, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            });
        db.Set<SubjectAllocation>().Add(new SubjectAllocation
        {
            Id = 99, TenantId = 1, AcademicYearId = 1, SubjectId = 2, StaffId = 1,
            CourseId = 1, GroupId = 2, SemesterId = 3, DepartmentId = 1,
            WeeklyHours = 1, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow), CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var report = await svc.BuildAsync();
        Assert.True(report.DownstreamRegression.AttendanceSaTtRegressionDetected);
        Assert.False(report.CanReExecuteTeachingGroupRemediation);
        Assert.False(report.IsHealthy);
    }
}

public sealed class AiSchedCatalogTimetableP14Prompt3H2TgRemediationReadinessArchitectureGuardTests
{
    [Fact]
    public void Service_Is_ReadOnly_And_Does_Not_Execute_3F()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "TeachingGroupRemediationReadinessService.cs"));
        Assert.Contains("P1-4-3H2", src, StringComparison.Ordinal);
        Assert.Contains("AsNoTracking", src, StringComparison.Ordinal);
        Assert.Contains("PreviewAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChangesAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChanges(", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteInTransactionAsync", src, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"tg\.SemesterId\s*=(?!=)", src);
        Assert.DoesNotMatch(@"section\.SemesterId\s*=(?!=)", src);
        Assert.DoesNotContain("ConflictEngine", src, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishAsync", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Prompt3F_Service_Unchanged_Contract()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "TeachingGroupSemesterRemediationService.cs"));
        Assert.Contains("ApprovedTeachingGroupIds = [1, 2]", src, StringComparison.Ordinal);
        Assert.Contains("ExpectedTargetSemesterId = 11", src, StringComparison.Ordinal);
        Assert.Contains("ExpectedLegacySemesterId = 3", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_And_Documentation_Exist()
    {
        var root = FindRepoRoot();
        var api = File.ReadAllText(Path.Combine(root, "Abhyanvaya.API", "Controllers", "SemesterController.cs"));
        Assert.Contains("teaching-group-remediation-readiness", api, StringComparison.Ordinal);
        Assert.Contains("ITeachingGroupRemediationReadinessService", api, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(
            root, "docs", "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3H_TG_REMEDIATION_READINESS.md")));
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
