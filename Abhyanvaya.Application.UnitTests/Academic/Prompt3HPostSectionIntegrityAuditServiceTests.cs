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

public sealed class Prompt3HPostSectionIntegrityAuditServiceTests
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

    private static (ApplicationDbContext Db, Prompt3HPostSectionIntegrityAuditService Svc) CreateSut(
        Func<LegacySemesterFinalizationAuditDto>? finalizationFactory = null)
    {
        var user = new AmbientUser();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("p143h-" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);

        var integrity = new Mock<ISemesterPostMigrationIntegrityAuditService>();
        integrity.Setup(i => i.BuildAuditAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SemesterPostMigrationIntegrityAuditDto
            {
                IsHealthy = true,
                IsReadOnly = true,
                Summary = new SemesterPostMigrationIntegritySummaryDto(),
            });

        var finalizationDto = finalizationFactory?.Invoke() ?? new LegacySemesterFinalizationAuditDto
        {
            IsReadOnly = true,
            NoMutationsPerformed = true,
            Summary = new LegacySemesterFinalizationSummaryDto
            {
                LegacyNullGroupCount = 1,
                NotNullReady = false,
                UniqueConstraintReady = false,
            },
            LegacySemesters =
            [
                new LegacySemesterInventoryRowDto
                {
                    SemesterId = 3,
                    CourseId = 1,
                    Number = 3,
                    Name = "Semester III",
                    SectionReferenceCount = 4,
                    Disposition = LegacySemesterFinalizationDisposition.ManualMappingRequired,
                    DispositionCode = "MANUAL_MAPPING_REQUIRED",
                    DispositionEvidence = "Finance sections remain.",
                },
            ],
            NullWildcardDependencies =
            [
                new NullWildcardDependencyDto
                {
                    Path = "AcademicTreeService",
                    Location = "filter",
                    Action = NullWildcardDependencyAction.ReplaceWithGroupScope,
                    ActionCode = "REPLACE_WITH_GROUP_SCOPE",
                },
            ],
            HardeningPreconditions = new DatabaseHardeningPreconditionDto
            {
                BlockingReasons = ["NULL-group remain"],
            },
        };

        var finalization = new Mock<ILegacySemesterFinalizationAuditService>();
        finalization.Setup(f => f.BuildAuditAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(finalizationDto);

        var svc = new Prompt3HPostSectionIntegrityAuditService(
            db, user, integrity.Object, finalization.Object);
        return (db, svc);
    }

    private static async Task SeedAsync(
        ApplicationDbContext db,
        bool with3GJournal = true,
        bool financeOnLegacy = true,
        bool studentHealthy = true,
        bool subjectOnLegacy = false)
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
            new Semester { Id = 11, TenantId = 1, CourseId = 1, Number = 3, Name = "Semester III", GroupId = 2, CreatedDate = DateTime.UtcNow },
            new Semester { Id = 10, TenantId = 1, CourseId = 1, Number = 3, Name = "Semester III", GroupId = 1, CreatedDate = DateTime.UtcNow });

        db.Set<Section>().Add(new Section
        {
            Id = 5, TenantId = 1, CollegeId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2,
            SemesterId = 11, SectionCode = "CA-B", SectionName = "CA B", Status = "Active", CreatedDate = DateTime.UtcNow,
        });
        if (financeOnLegacy)
        {
            db.Set<Section>().Add(new Section
            {
                Id = 9, TenantId = 1, CollegeId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 1,
                SemesterId = 3, SectionCode = "FA-A", SectionName = "FA A", Status = "Active", CreatedDate = DateTime.UtcNow,
            });
        }
        else
        {
            db.Set<Section>().Add(new Section
            {
                Id = 9, TenantId = 1, CollegeId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 1,
                SemesterId = 10, SectionCode = "FA-A", SectionName = "FA A", Status = "Active", CreatedDate = DateTime.UtcNow,
            });
        }

        db.Set<Student>().Add(new Student
        {
            Id = 1, TenantId = 1, StudentNumber = "A1", Name = "Student A",
            CourseId = 1,
            GroupId = studentHealthy ? 2 : 1,
            SemesterId = 11,
            GenderId = 1, MediumId = 1, FirstLanguageId = 1, LanguageId = 1, CreatedDate = DateTime.UtcNow,
        });

        if (subjectOnLegacy)
        {
            db.Set<Subject>().Add(new Subject
            {
                Id = 11, TenantId = 1, TenantSubjectId = 11, CourseId = 1, GroupId = 1, SemesterId = 3,
                CreatedDate = DateTime.UtcNow,
            });
        }

        db.Set<TeachingGroup>().Add(new TeachingGroup
        {
            Id = 1, TenantId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2, SemesterId = 11,
            SubjectId = 1, SubjectAllocationId = 1, Code = "TG1", Name = "TG1",
            Status = TeachingGroupStatus.Active, CreatedDate = DateTime.UtcNow,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
        });
        db.Set<TeachingGroupSection>().Add(new TeachingGroupSection
        {
            Id = 1, TenantId = 1, TeachingGroupId = 1, SectionId = 5, CreatedDate = DateTime.UtcNow,
        });

        if (with3GJournal)
        {
            db.Set<LegacySemesterDispositionJournal>().Add(new LegacySemesterDispositionJournal
            {
                TenantId = 1,
                SemesterId = 11,
                DispositionCode = SectionSemesterRemediationService.JournalDispositionCode,
                PromptCode = SectionSemesterRemediationService.PromptCode,
                Evidence = "SectionIds=[5]; legacy=3; actor=1",
                SemesterRowMutated = false,
                FinalizedUtc = DateTime.UtcNow,
                CreatedDate = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Audit_Is_ReadOnly_And_Does_Not_Mutate()
    {
        var (db, svc) = CreateSut();
        await SeedAsync(db);

        var beforeSections = await db.Set<Section>().AsNoTracking().Select(s => new { s.Id, s.SemesterId }).ToListAsync();
        var beforeTg = await db.Set<TeachingGroup>().AsNoTracking().Select(t => new { t.Id, t.SemesterId }).ToListAsync();
        var beforeTgs = await db.Set<TeachingGroupSection>().AsNoTracking().CountAsync();
        var beforeJournal = await db.Set<LegacySemesterDispositionJournal>().AsNoTracking().CountAsync();

        var report = await svc.BuildAuditAsync();

        Assert.True(report.IsReadOnly);
        Assert.True(report.NoMutationsPerformed);
        Assert.False(report.SaveChangesInvoked);
        Assert.Equal(beforeSections, await db.Set<Section>().AsNoTracking().Select(s => new { s.Id, s.SemesterId }).ToListAsync());
        Assert.Equal(beforeTg, await db.Set<TeachingGroup>().AsNoTracking().Select(t => new { t.Id, t.SemesterId }).ToListAsync());
        Assert.Equal(beforeTgs, await db.Set<TeachingGroupSection>().AsNoTracking().CountAsync());
        Assert.Equal(beforeJournal, await db.Set<LegacySemesterDispositionJournal>().AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task NotNullReady_False_When_Legacy_Section_Refs_Remain()
    {
        var (db, svc) = CreateSut();
        await SeedAsync(db, financeOnLegacy: true);

        var report = await svc.BuildAuditAsync();

        Assert.False(report.SchemaHardening.NotNullReady);
        Assert.False(report.CanMakeGroupIdNotNull);
        Assert.Equal("NOT READY", report.SchemaHardening.NotNullVerdict);
        Assert.True(report.Sections.LegacyNullGroupRefs >= 1);
        Assert.False(report.SchemaHardening.SchemaHardeningPromptSafeToBegin);
        Assert.Contains(report.ExactBlockers, b => b.Contains("Section", StringComparison.OrdinalIgnoreCase)
            || b.Contains("NULL-group", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UniqueReady_False_When_Duplicate_Group_Number_Exists()
    {
        var (db, svc) = CreateSut();
        await SeedAsync(db);
        db.Set<Semester>().Add(new Semester
        {
            Id = 12, TenantId = 1, CourseId = 1, Number = 3, Name = "Dup", GroupId = 2, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var report = await svc.BuildAuditAsync();

        Assert.False(report.SchemaHardening.UniqueReady);
        Assert.False(report.CanAddGroupSemesterUniqueConstraint);
        Assert.True(report.SemesterInventory.DuplicateGroupNumberCandidateCount >= 1);
    }

    [Fact]
    public async Task Prompt3G_Verification_Uses_Journal_And_Live_Sections()
    {
        var (db, svc) = CreateSut();
        await SeedAsync(db);

        var report = await svc.BuildAuditAsync();

        Assert.True(report.Prompt3GVerification.JournalEvidenceFound);
        Assert.Contains(5, report.Prompt3GVerification.JournaledSectionIds);
        Assert.Contains(5, report.Prompt3GVerification.RemediatedOnTargetSemester);
        Assert.Contains(9, report.Prompt3GVerification.FinanceResidualOnLegacy);
        Assert.True(report.Prompt3GVerification.Prompt3GContractSatisfied);
        Assert.Equal(0, report.TeachingGroupSections.IncompatibleCount);
    }

    [Fact]
    public async Task Tenant_Isolation_Scopes_Inventory()
    {
        var (db, svc) = CreateSut();
        await SeedAsync(db);
        db.Set<Semester>().Add(new Semester
        {
            Id = 99, TenantId = 2, CourseId = 1, Number = 1, Name = "Other", GroupId = null, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var report = await svc.BuildAuditAsync();
        Assert.DoesNotContain(99, report.SemesterInventory.NullGroupSemesterIds);
    }

    [Fact]
    public async Task Student_WrongGroup_Is_Incompatible()
    {
        var (db, svc) = CreateSut();
        await SeedAsync(db, studentHealthy: false);

        var report = await svc.BuildAuditAsync();
        Assert.True(report.Students.IncompatibleRefs >= 1);
        Assert.False(report.IsHealthy);
        Assert.True(report.ErrorCount >= 1);
    }

    [Fact]
    public async Task Section_Remediated_State_Is_Healthy_When_Finance_Cleared()
    {
        var (db, svc) = CreateSut(() => new LegacySemesterFinalizationAuditDto
        {
            IsReadOnly = true,
            NoMutationsPerformed = true,
            Summary = new LegacySemesterFinalizationSummaryDto
            {
                LegacyNullGroupCount = 1,
                NotNullReady = false,
                UniqueConstraintReady = false,
            },
            LegacySemesters =
            [
                new LegacySemesterInventoryRowDto
                {
                    SemesterId = 3,
                    CourseId = 1,
                    Number = 3,
                    Name = "Semester III",
                    Disposition = LegacySemesterFinalizationDisposition.HistoricalRetain,
                    DispositionCode = "HISTORICAL_RETAIN",
                    DispositionEvidence = "Zero operational refs.",
                },
            ],
            NullWildcardDependencies =
            [
                new NullWildcardDependencyDto
                {
                    Path = "AcademicTreeService",
                    Location = "filter",
                    Action = NullWildcardDependencyAction.ReplaceWithGroupScope,
                    ActionCode = "REPLACE_WITH_GROUP_SCOPE",
                },
            ],
        });
        await SeedAsync(db, financeOnLegacy: false);

        var report = await svc.BuildAuditAsync();
        Assert.Equal(0, report.Sections.LegacyNullGroupRefs);
        Assert.Equal(0, report.Sections.IncompatibleRefs);
        Assert.Contains(5, report.Prompt3GVerification.RemediatedOnTargetSemester);
        Assert.Empty(report.Prompt3GVerification.FinanceResidualOnLegacy);
        Assert.True(report.IsHealthy);
    }

    [Fact]
    public async Task Subject_Legacy_NullGroup_Keeps_NotNull_False()
    {
        var (db, svc) = CreateSut();
        await SeedAsync(db, financeOnLegacy: false, subjectOnLegacy: true);

        var report = await svc.BuildAuditAsync();
        Assert.True(report.Subjects.LegacyNullGroupRefs >= 1);
        Assert.False(report.CanMakeGroupIdNotNull);
    }

    [Fact]
    public async Task TeachingGroup_ClassifyOnly_No_Mutation_And_Residuals_Detected()
    {
        var (db, svc) = CreateSut();
        await SeedAsync(db);
        var tg = await db.Set<TeachingGroup>().FindAsync(1);
        tg!.SemesterId = 3;
        await db.SaveChangesAsync();

        var before = tg.SemesterId;
        var report = await svc.BuildAuditAsync();

        Assert.True(report.TeachingGroups.LegacyNullGroupRefs >= 1);
        Assert.Equal(before, (await db.Set<TeachingGroup>().AsNoTracking().FirstAsync(t => t.Id == 1)).SemesterId);
        Assert.Contains("classify-only", report.TeachingGroups.ClassificationOnlyNote, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Wildcard_Dependencies_Are_Classified_Not_Removed()
    {
        var (db, svc) = CreateSut();
        await SeedAsync(db);

        var report = await svc.BuildAuditAsync();
        Assert.NotEmpty(report.WildcardDependencyStatus);
        Assert.Contains(report.WildcardDependencyStatus,
            w => w.ClassificationCode == "ACTIVE_RUNTIME_DEPENDENCY");
        Assert.False(report.CanRemoveLegacyWildcardSemantics);
    }

    [Fact]
    public async Task Hardening_Ready_Only_When_All_Prerequisites_Satisfied()
    {
        var (db, svc) = CreateSut(() => new LegacySemesterFinalizationAuditDto
        {
            IsReadOnly = true,
            NoMutationsPerformed = true,
            LegacySemesters = [],
            NullWildcardDependencies = [],
            Summary = new LegacySemesterFinalizationSummaryDto
            {
                LegacyNullGroupCount = 0,
                NotNullReady = true,
                UniqueConstraintReady = true,
            },
        });

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
            Id = 11, TenantId = 1, CourseId = 1, Number = 3, Name = "Semester III", GroupId = 2, CreatedDate = DateTime.UtcNow,
        });
        db.Set<Section>().Add(new Section
        {
            Id = 5, TenantId = 1, CollegeId = 1, AcademicYearId = 1, CourseId = 1, GroupId = 2,
            SemesterId = 11, SectionCode = "CA-B", SectionName = "CA B", Status = "Active", CreatedDate = DateTime.UtcNow,
        });
        db.Set<LegacySemesterDispositionJournal>().Add(new LegacySemesterDispositionJournal
        {
            TenantId = 1,
            SemesterId = 11,
            DispositionCode = SectionSemesterRemediationService.JournalDispositionCode,
            PromptCode = SectionSemesterRemediationService.PromptCode,
            Evidence = "SectionIds=[5]; legacy=3; actor=1",
            SemesterRowMutated = false,
            FinalizedUtc = DateTime.UtcNow,
            CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var report = await svc.BuildAuditAsync();

        Assert.Equal(0, report.SemesterInventory.NullGroupIdCount);
        Assert.True(report.CanMakeGroupIdNotNull);
        Assert.True(report.CanAddGroupSemesterUniqueConstraint);
        Assert.True(report.CanRemoveLegacyWildcardSemantics);
        Assert.True(report.SchemaHardening.SchemaHardeningPromptSafeToBegin);
    }

    [Fact]
    public async Task Legacy_Classification_Uses_Allowed_Codes_Only()
    {
        var (db, svc) = CreateSut(() => new LegacySemesterFinalizationAuditDto
        {
            IsReadOnly = true,
            NoMutationsPerformed = true,
            Summary = new LegacySemesterFinalizationSummaryDto
            {
                LegacyNullGroupCount = 3,
                NotNullReady = false,
                UniqueConstraintReady = false,
            },
            LegacySemesters =
            [
                new LegacySemesterInventoryRowDto
                {
                    SemesterId = 1, CourseId = 1, Number = 1, Name = "I",
                    TeachingGroupReferenceCount = 1,
                    Disposition = LegacySemesterFinalizationDisposition.BlockedByTeachingGroupReference,
                    DispositionCode = "BLOCKED_BY_TEACHING_GROUP_REFERENCE",
                    DispositionEvidence = "TG residual",
                },
                new LegacySemesterInventoryRowDto
                {
                    SemesterId = 2, CourseId = 1, Number = 2, Name = "II",
                    Disposition = LegacySemesterFinalizationDisposition.HistoricalRetain,
                    DispositionCode = "HISTORICAL_RETAIN",
                    DispositionEvidence = "retain",
                },
                new LegacySemesterInventoryRowDto
                {
                    SemesterId = 4, CourseId = 1, Number = 4, Name = "IV",
                    Disposition = LegacySemesterFinalizationDisposition.DuplicateReview,
                    DispositionCode = "DUPLICATE_REVIEW",
                    DispositionEvidence = "dup",
                },
            ],
            NullWildcardDependencies =
            [
                new NullWildcardDependencyDto
                {
                    Path = "AcademicTreeService",
                    Location = "filter",
                    Action = NullWildcardDependencyAction.ReplaceWithGroupScope,
                    ActionCode = "REPLACE_WITH_GROUP_SCOPE",
                },
            ],
        });
        await SeedAsync(db, financeOnLegacy: false);

        var report = await svc.BuildAuditAsync();
        var codes = report.LegacyClassifications.Select(c => c.ClassificationCode).ToHashSet(StringComparer.Ordinal);
        Assert.Subset(new HashSet<string>(StringComparer.Ordinal)
        {
            "RETAIN_HISTORICAL",
            "MANUAL_MAPPING_REQUIRED",
            "DUPLICATE_REVIEW",
            "BLOCKED_BY_TEACHING_GROUP_REFERENCE",
            "BLOCKED_BY_DOWNSTREAM_REFERENCE",
            "SAFE_FOR_GROUP_MAPPING",
            "OBSOLETE_CANDIDATE",
            "READY_FOR_RETIREMENT",
            "READY_FOR_GROUP_ASSIGNMENT",
        }, codes);
        Assert.Contains(report.LegacyClassifications, c => c.ClassificationCode == "BLOCKED_BY_TEACHING_GROUP_REFERENCE");
        Assert.Contains(report.LegacyClassifications, c => c.ClassificationCode == "RETAIN_HISTORICAL");
        Assert.Contains(report.LegacyClassifications, c => c.ClassificationCode == "DUPLICATE_REVIEW");
    }

    [Fact]
    public async Task Hardening_Decision_Exposes_Contract_Flags()
    {
        var (db, svc) = CreateSut();
        await SeedAsync(db);
        var report = await svc.BuildAuditAsync();
        Assert.True(report.IsReadOnly);
        Assert.False(report.SaveChangesInvoked);
        Assert.NotNull(report.SemesterHardeningReadyCode);
        Assert.Contains(report.SemesterHardeningReadyCode, new[] { "READY", "NOT_READY", "BLOCKED" });
        Assert.NotNull(report.ProgramOptionality);
        Assert.True(report.ProgramOptionality.ProgramRemainsOptional);
        Assert.True(report.ProgramOptionality.CourseDepartmentIdMandatory);
        Assert.NotNull(report.TenantIsolation);
        Assert.NotNull(report.DepartmentSsot);
        Assert.NotNull(report.TeachingGroups.Residuals);
    }
}

public sealed class AiSchedCatalogTimetableP14Prompt3HPostSectionIntegrityArchitectureGuardTests
{
    [Fact]
    public void Service_Is_ReadOnly_Contract()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "Prompt3HPostSectionIntegrityAuditService.cs"));

        Assert.Contains("P1-4-3H", src, StringComparison.Ordinal);
        Assert.Contains("AsNoTracking", src, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChangesAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChanges(", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteUpdate", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteDelete", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteSql", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ExecuteInTransactionAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("section.SemesterId =", src, StringComparison.Ordinal);
        Assert.DoesNotContain("tg.SemesterId =", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("new TeachingGroup(", src, StringComparison.Ordinal);
        Assert.DoesNotContain("new TeachingGroupSection", src, StringComparison.Ordinal);
        Assert.DoesNotContain("new TimetableSection", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AlterColumn", src, StringComparison.Ordinal);
        Assert.DoesNotContain("IsRequired()", src, StringComparison.Ordinal);
        Assert.DoesNotContain("HasIndex", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ConflictEngine", src, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishAsync", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_Endpoint_Exists()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(root, "Abhyanvaya.API", "Controllers", "SemesterController.cs"));
        Assert.Contains("post-section-remediation-integrity-audit", src, StringComparison.Ordinal);
        Assert.Contains("post-section-integrity-schema-readiness", src, StringComparison.Ordinal);
        Assert.Contains("IPrompt3HPostSectionIntegrityAuditService", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Documentation_Exists()
    {
        var root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(
            root, "docs", "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3H_POST_SECTION_REMEDIATION_INTEGRITY_AUDIT.md")));
        Assert.True(File.Exists(Path.Combine(
            root, "docs", "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3H_POST_SECTION_INTEGRITY_AUDIT.md")));
        Assert.True(File.Exists(Path.Combine(
            root, "docs", "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3H_POST_SECTION_INTEGRITY_AND_SCHEMA_READINESS.md")));
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
