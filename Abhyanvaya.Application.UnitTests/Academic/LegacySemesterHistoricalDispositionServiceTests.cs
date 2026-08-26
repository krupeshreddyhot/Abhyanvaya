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

/// <summary>AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3J-A — historical disposition.</summary>
public sealed class LegacySemesterHistoricalDispositionServiceTests
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

    private static (ApplicationDbContext Db, LegacySemesterHistoricalDispositionService Svc, AmbientUser User)
        CreateSut(LegacySemesterFinalizationAuditDto? fin = null)
    {
        var user = new AmbientUser();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("p143ja-" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);

        var finalization = new Mock<ILegacySemesterFinalizationAuditService>();
        finalization.Setup(f => f.BuildAuditAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(fin ?? DefaultFin());

        var svc = new LegacySemesterHistoricalDispositionService(
            db, user, finalization.Object, NullLogger<LegacySemesterHistoricalDispositionService>.Instance);
        return (db, svc, user);
    }

    private static LegacySemesterFinalizationAuditDto DefaultFin() => new()
    {
        IsReadOnly = true,
        LegacySemesters =
        [
            new LegacySemesterInventoryRowDto
            {
                SemesterId = 1, CourseId = 1, Number = 1, Name = "I",
                SubjectReferenceCount = 1,
                Disposition = LegacySemesterFinalizationDisposition.ManualMappingRequired,
                DispositionCode = "MANUAL_MAPPING_REQUIRED",
            },
            new LegacySemesterInventoryRowDto
            {
                SemesterId = 2, CourseId = 1, Number = 2, Name = "II",
                Disposition = LegacySemesterFinalizationDisposition.HistoricalRetain,
                DispositionCode = "HISTORICAL_RETAIN",
            },
            new LegacySemesterInventoryRowDto
            {
                SemesterId = 3, CourseId = 1, Number = 3, Name = "III",
                Disposition = LegacySemesterFinalizationDisposition.HistoricalRetain,
                DispositionCode = "HISTORICAL_RETAIN",
            },
            new LegacySemesterInventoryRowDto
            {
                SemesterId = 4, CourseId = 1, Number = 4, Name = "IV-a",
                Disposition = LegacySemesterFinalizationDisposition.DuplicateReview,
                DispositionCode = "DUPLICATE_REVIEW",
            },
            new LegacySemesterInventoryRowDto
            {
                SemesterId = 5, CourseId = 1, Number = 4, Name = "IV-b",
                Disposition = LegacySemesterFinalizationDisposition.DuplicateReview,
                DispositionCode = "DUPLICATE_REVIEW",
            },
        ],
    };

    private static async Task SeedLegacyAsync(ApplicationDbContext db)
    {
        foreach (var id in new[] { 1, 2, 3, 4, 5 })
        {
            var entity = new Semester
            {
                Id = id,
                TenantId = 1,
                CourseId = 1,
                Number = id == 5 ? 4 : id,
                Name = $"Sem {id}",
                GroupId = null,
                CreatedDate = DateTime.UtcNow,
            };
            db.Set<Semester>().Add(entity);
            db.Entry(entity).Property(x => x.Id).IsTemporary = false;
        }
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Preview_Is_ReadOnly()
    {
        var (db, svc, _) = CreateSut();
        await SeedLegacyAsync(db);
        var journals = await db.Set<LegacySemesterDispositionJournal>().CountAsync();

        var preview = await svc.PreviewAsync();
        Assert.True(preview.IsReadOnly);
        Assert.True(preview.NoMutationsPerformed);
        Assert.False(preview.Prompt3JAuthorized);
        Assert.False(preview.SchemaHardeningReady);
        Assert.NotEmpty(preview.DependencyMatrix);
        Assert.Equal(journals, await db.Set<LegacySemesterDispositionJournal>().CountAsync());
        Assert.All(db.Set<Semester>(), s => Assert.False(s.IsHistoricalArchive));
    }

    [Fact]
    public async Task Historical_Disposition_Requires_Explicit_Approval_No_Archive_All()
    {
        var (db, svc, _) = CreateSut();
        await SeedLegacyAsync(db);

        var empty = await svc.ExecuteAsync(new LegacySemesterHistoricalDispositionExecuteRequest());
        Assert.False(empty.IsSuccessful);
        Assert.Equal("Aborted", empty.ExecutionStatus);
        Assert.Contains("Explicit", empty.AbortReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task No_Group_Guessing_AssignedGroupId_Always_Null()
    {
        var (db, svc, _) = CreateSut();
        await SeedLegacyAsync(db);

        var result = await svc.ExecuteAsync(new LegacySemesterHistoricalDispositionExecuteRequest
        {
            Items =
            [
                new() { SemesterId = 2, Disposition = LegacySemesterHistoricalDispositionCodes.HistoricalArchive },
            ],
            Reason = "test archive",
        });
        Assert.True(result.IsSuccessful);
        Assert.All(result.Findings, f => Assert.False(f.GroupIdMutated));
        Assert.Null(await db.Set<Semester>().Where(s => s.Id == 2).Select(s => s.GroupId).FirstAsync());
        Assert.All(await db.Set<LegacySemesterDispositionJournal>().ToListAsync(),
            j => Assert.Null(j.AssignedGroupId));
    }

    [Fact]
    public async Task Semester_1_Remains_Unresolved_Manual()
    {
        var (db, svc, _) = CreateSut();
        await SeedLegacyAsync(db);
        var preview = await svc.PreviewAsync();
        var sem1 = Assert.Single(preview.Candidates, c => c.SemesterId == 1);
        Assert.Equal(LegacySemesterHistoricalDispositionCodes.ManualMappingRequired, sem1.RecommendedDisposition);
        Assert.False(sem1.EligibleForHistoricalArchive);

        var blocked = await svc.ExecuteAsync(new LegacySemesterHistoricalDispositionExecuteRequest
        {
            Items =
            [
                new() { SemesterId = 1, Disposition = LegacySemesterHistoricalDispositionCodes.HistoricalArchive },
            ],
        });
        Assert.False(blocked.IsSuccessful);
        Assert.False(await db.Set<Semester>().Where(s => s.Id == 1).Select(s => s.IsHistoricalArchive).FirstAsync());
    }

    [Fact]
    public async Task Duplicate_Review_Does_Not_Delete_Or_Merge()
    {
        var (db, svc, _) = CreateSut();
        await SeedLegacyAsync(db);
        var preview = await svc.PreviewAsync();
        Assert.Contains(preview.Candidates, c => c.SemesterId == 4 && c.RecommendedDisposition == LegacySemesterHistoricalDispositionCodes.DuplicateReview);
        Assert.Contains(preview.Candidates, c => c.SemesterId == 5 && c.RecommendedDisposition == LegacySemesterHistoricalDispositionCodes.DuplicateReview);

        var result = await svc.ExecuteAsync(new LegacySemesterHistoricalDispositionExecuteRequest
        {
            Items =
            [
                new() { SemesterId = 4, Disposition = LegacySemesterHistoricalDispositionCodes.DuplicateReview },
                new() { SemesterId = 5, Disposition = LegacySemesterHistoricalDispositionCodes.DuplicateReview },
            ],
        });
        Assert.True(result.IsSuccessful);
        Assert.Equal(2, await db.Set<Semester>().CountAsync(s => s.Id == 4 || s.Id == 5));
        Assert.All(await db.Set<Semester>().Where(s => s.Id == 4 || s.Id == 5).ToListAsync(),
            s => Assert.False(s.IsHistoricalArchive));
        Assert.Equal(2, result.DuplicateReviewCount);
    }

    [Fact]
    public async Task Approved_Historical_Disposition_Succeeds()
    {
        var (db, svc, _) = CreateSut();
        await SeedLegacyAsync(db);

        var result = await svc.ExecuteAsync(new LegacySemesterHistoricalDispositionExecuteRequest
        {
            Items =
            [
                new() { SemesterId = 2, Disposition = LegacySemesterHistoricalDispositionCodes.HistoricalArchive },
                new() { SemesterId = 3, Disposition = LegacySemesterHistoricalDispositionCodes.HistoricalArchive },
            ],
        });
        Assert.True(result.IsSuccessful);
        Assert.Equal("Completed", result.ExecutionStatus);
        Assert.True(await db.Set<Semester>().Where(s => s.Id == 2).Select(s => s.IsHistoricalArchive).FirstAsync());
        Assert.True(await db.Set<Semester>().Where(s => s.Id == 3).Select(s => s.IsHistoricalArchive).FirstAsync());
        Assert.NotNull(result.PostDispositionIntegrity);
        Assert.True(result.PostDispositionIntegrity!.Passed);
    }

    [Fact]
    public async Task Historical_Excluded_From_Operational_Selection_Rules()
    {
        Assert.False(OperationalSemesterRules.IsOperational(false, 1, true));
        Assert.True(OperationalSemesterRules.IsOperational(false, 1, false));
        Assert.False(OperationalSemesterRules.IsOperational(false, null, false));
    }

    [Fact]
    public async Task Idempotency_Second_Execution_AlreadyComplete()
    {
        var (db, svc, _) = CreateSut();
        await SeedLegacyAsync(db);
        var first = await svc.ExecuteAsync(new LegacySemesterHistoricalDispositionExecuteRequest
        {
            Items = [new() { SemesterId = 2, Disposition = LegacySemesterHistoricalDispositionCodes.HistoricalArchive }],
        });
        Assert.True(first.IsSuccessful);
        var journals = await db.Set<LegacySemesterDispositionJournal>().CountAsync();

        var second = await svc.ExecuteAsync(new LegacySemesterHistoricalDispositionExecuteRequest
        {
            Items = [new() { SemesterId = 2, Disposition = LegacySemesterHistoricalDispositionCodes.HistoricalArchive }],
        });
        Assert.Equal("AlreadyComplete", second.ExecutionStatus);
        Assert.Equal(0, second.ChangedCount);
        Assert.Equal(journals, await db.Set<LegacySemesterDispositionJournal>().CountAsync());
    }

    [Fact]
    public async Task Tenant_Isolation_Blocks_Foreign_Semester()
    {
        var (db, svc, user) = CreateSut();
        db.Set<Semester>().Add(new Semester
        {
            Id = 99, TenantId = 2, CourseId = 1, Number = 9, Name = "X", GroupId = null, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await svc.ExecuteAsync(new LegacySemesterHistoricalDispositionExecuteRequest
        {
            Items = [new() { SemesterId = 99, Disposition = LegacySemesterHistoricalDispositionCodes.HistoricalArchive }],
        });
        Assert.False(result.IsSuccessful);
        Assert.Contains(result.Findings, f => f.Result == "Blocked");
    }

    [Fact]
    public async Task Ops_Refs_Block_Archive_And_Rollback_Batch()
    {
        var fin = new LegacySemesterFinalizationAuditDto
        {
            IsReadOnly = true,
            LegacySemesters =
            [
                new LegacySemesterInventoryRowDto
                {
                    SemesterId = 2, CourseId = 1, Number = 2, Name = "II",
                    StudentReferenceCount = 3,
                    Disposition = LegacySemesterFinalizationDisposition.HistoricalRetain,
                    DispositionCode = "HISTORICAL_RETAIN",
                },
            ],
        };
        var (db, svc, _) = CreateSut(fin);
        db.Set<Semester>().Add(new Semester
        {
            Id = 2, TenantId = 1, CourseId = 1, Number = 2, Name = "II", GroupId = null, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await svc.ExecuteAsync(new LegacySemesterHistoricalDispositionExecuteRequest
        {
            Items =
            [
                new() { SemesterId = 2, Disposition = LegacySemesterHistoricalDispositionCodes.HistoricalArchive },
            ],
        });
        Assert.False(result.IsSuccessful);
        Assert.True(result.RolledBack);
        Assert.False(await db.Set<Semester>().Where(s => s.Id == 2).Select(s => s.IsHistoricalArchive).FirstAsync());
    }

    [Fact]
    public void Student_Cannot_Select_Historical_Semester()
    {
        var d = StudentSemesterOwnershipRules.EvaluateWrite(
            1, 1, 2, 11,
            new StudentSemesterOwnershipRules.GroupSnapshot(2, 1, 1, false),
            new StudentSemesterOwnershipRules.SemesterSnapshot(11, 1, 1, 2, false, true));
        Assert.False(d.Accepted);
        Assert.Contains("Historical", d.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Api_Routes_And_PromptCode_Present()
    {
        var root = FindRepoRoot();
        var api = File.ReadAllText(Path.Combine(root, "Abhyanvaya.API", "Controllers", "SemesterController.cs"));
        Assert.Contains("legacy-historical-disposition-preview", api, StringComparison.Ordinal);
        Assert.Contains("legacy-historical-disposition/execute", api, StringComparison.Ordinal);
        Assert.Contains("P1-4-3JA", api, StringComparison.Ordinal);
        Assert.DoesNotContain("archive all legacy", api, StringComparison.OrdinalIgnoreCase);
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
