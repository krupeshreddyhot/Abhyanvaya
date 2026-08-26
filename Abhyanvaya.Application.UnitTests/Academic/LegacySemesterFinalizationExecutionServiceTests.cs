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

public sealed class LegacySemesterFinalizationExecutionPlannerTests
{
    [Fact]
    public void Retain_Historical_Is_Eligible_When_Empty()
    {
        var r = LegacySemesterFinalizationExecutionPlanner.Plan(new(
            2, 2, "Semester II", 1, null,
            LegacySemesterFinalizationDisposition.HistoricalRetain,
            "empty", false, false, null, [], null));
        Assert.Equal(LegacySemesterExecutionDispositionCodes.RetainHistorical, r.DispositionCode);
        Assert.Equal("Retain", r.Action);
        Assert.True(r.WriteRetainJournal);
        Assert.False(r.AssignGroupId);
    }

    [Fact]
    public void Manual_Mapping_Blocks()
    {
        var r = LegacySemesterFinalizationExecutionPlanner.Plan(new(
            1, 1, "Semester I", 1, null,
            LegacySemesterFinalizationDisposition.ManualMappingRequired,
            "subj", false, false, null, [], null));
        Assert.Equal(LegacySemesterExecutionDispositionCodes.ManualMappingRequired, r.DispositionCode);
        Assert.Equal("Block", r.Action);
        Assert.False(r.MutationAllowed);
    }

    [Fact]
    public void Duplicate_Review_Blocks()
    {
        var r = LegacySemesterFinalizationExecutionPlanner.Plan(new(
            4, 4, "Semester VI", 1, null,
            LegacySemesterFinalizationDisposition.DuplicateReview,
            "dup", false, false, null, [], null));
        Assert.Equal(LegacySemesterExecutionDispositionCodes.DuplicateReview, r.DispositionCode);
        Assert.Equal("Block", r.Action);
    }

    [Fact]
    public void Teaching_Group_Is_Deferred()
    {
        var r = LegacySemesterFinalizationExecutionPlanner.Plan(new(
            3, 3, "Semester III", 1, null,
            LegacySemesterFinalizationDisposition.BlockedByTeachingGroupReference,
            "tg", false, false, null, [1, 2], 11));
        Assert.Equal(LegacySemesterExecutionDispositionCodes.BlockedByTeachingGroupReference, r.DispositionCode);
        Assert.Equal("DeferTg", r.Action);
        Assert.False(r.MutationAllowed);
    }

    [Fact]
    public void Already_Group_Specific_Skips()
    {
        var r = LegacySemesterFinalizationExecutionPlanner.Plan(new(
            9, 4, "Semester IV", 1, 2,
            LegacySemesterFinalizationDisposition.AlreadyGroupSpecific,
            "owned", false, false, null, [], null));
        Assert.Equal(LegacySemesterExecutionDispositionCodes.AlreadyGroupSpecific, r.DispositionCode);
        Assert.Equal("Skip", r.Action);
    }

    [Fact]
    public void Safe_Single_Group_Rejected_Without_Approval()
    {
        var r = LegacySemesterFinalizationExecutionPlanner.Plan(new(
            7, 1, "Sem", 1, null,
            LegacySemesterFinalizationDisposition.SafeSingleGroupMapping,
            "one group", false, false, null, [], null));
        Assert.Equal(LegacySemesterExecutionDispositionCodes.ManualMappingRequired, r.DispositionCode);
        Assert.Equal("Block", r.Action);
        Assert.False(r.AssignGroupId);
    }

    [Fact]
    public void Safe_Single_Group_Finalizes_Only_With_Explicit_Approval()
    {
        var r = LegacySemesterFinalizationExecutionPlanner.Plan(new(
            7, 1, "Sem", 1, null,
            LegacySemesterFinalizationDisposition.SafeSingleGroupMapping,
            "one group", false, true, 1, [], null));
        Assert.Equal(LegacySemesterExecutionDispositionCodes.FinalizedLegacy, r.DispositionCode);
        Assert.Equal("Finalize", r.Action);
        Assert.True(r.AssignGroupId);
        Assert.Equal(1, r.AssignedGroupId);
    }

    [Fact]
    public void Retain_AlreadyComplete_When_Journal_Exists()
    {
        var r = LegacySemesterFinalizationExecutionPlanner.Plan(new(
            2, 2, "Semester II", 1, null,
            LegacySemesterFinalizationDisposition.HistoricalRetain,
            "empty", true, false, null, [], null));
        Assert.Equal("AlreadyComplete", r.Action);
        Assert.False(r.WriteRetainJournal);
    }
}

public sealed class LegacySemesterFinalizationExecutionServiceTests
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

    private static (ApplicationDbContext Db, LegacySemesterFinalizationExecutionService Svc, AmbientUser User) CreateSut()
    {
        var user = new AmbientUser();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("p143e-" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);

        var decision = new Mock<ILegacySemesterMigrationDecisionPlanService>();
        decision.Setup(d => d.BuildDecisionPlanAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySemesterMigrationDecisionPlanDto
            {
                MatchesPrompt2BBaseline = true,
                Decisions =
                [
                    new LegacySemesterMigrationDecisionRowDto
                    {
                        SemesterId = 1, DecisionCode = "RETAIN_LEGACY_PENDING_DECISION",
                    },
                    new LegacySemesterMigrationDecisionRowDto
                    {
                        SemesterId = 2, DecisionCode = "RETAIN_LEGACY_PENDING_DECISION",
                    },
                    new LegacySemesterMigrationDecisionRowDto
                    {
                        SemesterId = 3, DecisionCode = "RETAIN_LEGACY_PENDING_DECISION",
                    },
                    new LegacySemesterMigrationDecisionRowDto
                    {
                        SemesterId = 4, DecisionCode = "DUPLICATE_REVIEW",
                    },
                    new LegacySemesterMigrationDecisionRowDto
                    {
                        SemesterId = 5, DecisionCode = "DUPLICATE_REVIEW",
                    },
                ],
            });

        var audit = new Mock<ILegacySemesterFinalizationAuditService>();
        audit.Setup(a => a.BuildAuditAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegacySemesterFinalizationAuditDto
            {
                Summary = new LegacySemesterFinalizationSummaryDto
                {
                    LegacyNullGroupCount = 5,
                    TeachingGroupResidualCount = 2,
                    NotNullReady = false,
                    UniqueConstraintReady = false,
                },
                HardeningPreconditions = new DatabaseHardeningPreconditionDto
                {
                    BlockingReasons = ["NULL-group remain"],
                },
            });

        var svc = new LegacySemesterFinalizationExecutionService(
            db, user, audit.Object, decision.Object, NullLogger<LegacySemesterFinalizationExecutionService>.Instance);
        return (db, svc, user);
    }

    private static async Task SeedAsync(ApplicationDbContext db)
    {
        db.Set<Course>().Add(new Course
        {
            Id = 1, TenantId = 1, Code = "BCOM", Name = "B.Com", DepartmentId = 1, CreatedDate = DateTime.UtcNow,
        });
        db.Set<Group>().AddRange(
            new Group { Id = 1, TenantId = 1, CourseId = 1, Code = "13", Name = "FINANCE", CreatedDate = DateTime.UtcNow },
            new Group { Id = 2, TenantId = 1, CourseId = 1, Code = "05", Name = "CA", CreatedDate = DateTime.UtcNow });
        db.Set<Semester>().AddRange(
            new Semester { Id = 1, TenantId = 1, CourseId = 1, Number = 1, Name = "Semester I", GroupId = null, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 2, TenantId = 1, CourseId = 1, Number = 2, Name = "Semester II", GroupId = null, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 3, TenantId = 1, CourseId = 1, Number = 3, Name = "Semester III", GroupId = null, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 4, TenantId = 1, CourseId = 1, Number = 4, Name = "Semester VI", GroupId = null, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 5, TenantId = 1, CourseId = 1, Number = 4, Name = "Semester V", GroupId = null, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 9, TenantId = 1, CourseId = 1, Number = 4, Name = "Semester IV", GroupId = 2, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 11, TenantId = 1, CourseId = 1, Number = 3, Name = "Semester III", GroupId = 2, CreatedDate = DateTime.UtcNow });

        // Sem 1 has Subject → MANUAL_MAPPING_REQUIRED
        db.Set<Subject>().Add(new Subject
        {
            Id = 1, TenantId = 1, CourseId = 1, GroupId = 1, SemesterId = 1, TenantSubjectId = 1,
            CreatedDate = DateTime.UtcNow,
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
    public async Task Preview_Performs_Zero_Writes()
    {
        var (db, svc, _) = CreateSut();
        await SeedAsync(db);

        var preview = await svc.PreviewAsync();
        Assert.True(preview.IsReadOnly);
        Assert.Equal("NotExecuted", preview.ExecutionStatus);
        Assert.Equal(0, await db.Set<LegacySemesterDispositionJournal>().CountAsync());
        Assert.Equal(2, await db.Set<TeachingGroup>().CountAsync(t => t.SemesterId == 3));
        Assert.All(await db.Set<Semester>().Where(s => s.Id <= 5).ToListAsync(), s => Assert.Null(s.GroupId));
    }

    [Fact]
    public async Task Execute_Retains_Historical_Only_And_Blocks_Others()
    {
        var (db, svc, _) = CreateSut();
        await SeedAsync(db);

        var result = await svc.ExecuteAsync();
        Assert.Equal("Completed", result.ExecutionStatus);
        Assert.False(result.RolledBack);
        Assert.Equal(1, result.RetainedCount);
        Assert.Equal(0, result.ChangedCount);
        Assert.True(result.ManualReviewCount >= 1);
        Assert.True(result.BlockedCount >= 2);
        Assert.Equal(1, result.DeferredTeachingGroupCount);
        Assert.Contains(2, result.AffectedSemesterIds);

        Assert.Equal(1, await db.Set<LegacySemesterDispositionJournal>().CountAsync());
        var journal = await db.Set<LegacySemesterDispositionJournal>().SingleAsync();
        Assert.Equal(2, journal.SemesterId);
        Assert.Equal(LegacySemesterExecutionDispositionCodes.RetainHistorical, journal.DispositionCode);
        Assert.False(journal.SemesterRowMutated);

        Assert.Null((await db.Set<Semester>().FindAsync(2))!.GroupId);
        Assert.Null((await db.Set<Semester>().FindAsync(1))!.GroupId);
        Assert.Null((await db.Set<Semester>().FindAsync(3))!.GroupId);
        Assert.Equal(2, await db.Set<TeachingGroup>().CountAsync(t => t.SemesterId == 3));
        Assert.Equal(0, await db.Set<AttendanceSession>().CountAsync());
    }

    [Fact]
    public async Task Execute_Is_Idempotent()
    {
        var (db, svc, _) = CreateSut();
        await SeedAsync(db);
        _ = await svc.ExecuteAsync();
        var second = await svc.ExecuteAsync();
        Assert.Equal("AlreadyComplete", second.ExecutionStatus);
        Assert.Equal(0, second.RetainedCount);
        Assert.Equal(0, second.ChangedCount);
        Assert.Equal(1, second.AlreadyCompleteCount);
        Assert.Equal(1, await db.Set<LegacySemesterDispositionJournal>().CountAsync());
    }

    [Fact]
    public async Task TeachingGroup_And_TimetableSection_Immutable()
    {
        var (db, svc, _) = CreateSut();
        await SeedAsync(db);
        db.Set<TimetableSection>().Add(new TimetableSection
        {
            Id = 1, TenantId = 1, TimetableId = 1, TimetableEntryId = 1, SectionId = 1, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        _ = await svc.ExecuteAsync();
        Assert.Equal(2, await db.Set<TeachingGroup>().CountAsync(t => t.SemesterId == 3));
        Assert.Equal(1, await db.Set<TimetableSection>().CountAsync());
        Assert.Equal(0, await db.Set<TeachingGroupSection>().CountAsync());
    }

    [Fact]
    public async Task Cross_Tenant_Semester_Is_Not_Finalized()
    {
        var (db, svc, user) = CreateSut();
        await SeedAsync(db);
        db.Set<Semester>().Add(new Semester
        {
            Id = 50, TenantId = 99, CourseId = 1, Number = 9, Name = "Other", GroupId = null, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        user.TenantId = 1;
        var result = await svc.ExecuteAsync();
        Assert.DoesNotContain(result.Items, i => i.SemesterId == 50);
        Assert.Equal(0, await db.Set<LegacySemesterDispositionJournal>().CountAsync(j => j.SemesterId == 50));
    }

    [Fact]
    public async Task Student_On_Historical_Prevents_Retain()
    {
        var (db, svc, _) = CreateSut();
        await SeedAsync(db);
        db.Set<Student>().Add(new Student
        {
            Id = 1, TenantId = 1, CourseId = 1, GroupId = 1, SemesterId = 2,
            StudentNumber = "S1", Name = "A B", GenderId = 1, MediumId = 1,
            FirstLanguageId = 1, LanguageId = 1, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await svc.ExecuteAsync();
        Assert.NotEqual("Aborted", result.ExecutionStatus);
        Assert.Equal(0, result.RetainedCount);
        Assert.Equal(0, await db.Set<LegacySemesterDispositionJournal>().CountAsync());
    }
}

public sealed class AiSchedCatalogTimetableP14Prompt3ELegacySemesterFinalizationArchitectureGuardTests
{
    [Fact]
    public void Execution_Service_Does_Not_Mutate_TeachingGroups_Or_Attendance()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "LegacySemesterFinalizationExecutionService.cs"));
        Assert.Contains("ExecuteInTransactionAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("SchedulingTeachingGroupSections", src, StringComparison.Ordinal);
        Assert.DoesNotContain("TimetableSections", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ConflictEngine", src, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("StudentSections", src, StringComparison.Ordinal);
        Assert.DoesNotContain("a.SemesterId =", src, StringComparison.Ordinal);
        Assert.Contains("AllowSafeSingleGroupFinalization: false", src, StringComparison.Ordinal);
        Assert.Contains("AsNoTracking", src, StringComparison.Ordinal);
        Assert.Contains("LegacySemesterDispositionJournal", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_And_Execute_Endpoints_Exist()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(root, "Abhyanvaya.API", "Controllers", "SemesterController.cs"));
        Assert.Contains("legacy-finalization-execution-preview", src, StringComparison.Ordinal);
        Assert.Contains("legacy-finalization/execute", src, StringComparison.Ordinal);
        Assert.Contains("ILegacySemesterFinalizationExecutionService", src, StringComparison.Ordinal);
        Assert.Contains("CanManageSemesters", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Documentation_Exists()
    {
        var root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(
            root, "docs", "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3E_LEGACY_SEMESTER_FINALIZATION.md")));
    }

    [Fact]
    public void Journal_Migration_Does_Not_Harden_Semester_Schema()
    {
        var root = FindRepoRoot();
        var mig = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Infrastructure", "Persistence", "Migrations",
            "20260822180000_AI_SCHED_CATALOG_P1_4_Prompt3E_LegacySemesterDispositionJournal.cs"));
        Assert.Contains("LegacySemesterDispositionJournals", mig, StringComparison.Ordinal);
        Assert.DoesNotContain("AlterColumn", mig, StringComparison.Ordinal);
        Assert.DoesNotContain("DropColumn", mig, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Semester\"", mig, StringComparison.Ordinal);
    }

    [Fact]
    public void Frozen_Boundaries_Unchanged()
    {
        Assert.Equal(typeof(int?), typeof(Semester).GetProperty(nameof(Semester.GroupId))!.PropertyType);
        Assert.NotNull(typeof(TimetableEntry).GetProperty(nameof(TimetableEntry.TeachingGroupId)));
        Assert.Null(typeof(TimetableEntry).GetProperty("SectionId"));
        Assert.Equal(typeof(int), typeof(Course).GetProperty(nameof(Course.DepartmentId))!.PropertyType);
        Assert.NotNull(typeof(TenantAcademicConfiguration).GetProperty(nameof(TenantAcademicConfiguration.EnablePrograms)));
        Assert.NotNull(typeof(LegacySemesterDispositionJournal));
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
