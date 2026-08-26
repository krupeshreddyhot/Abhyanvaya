using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-TG.4A Prompt 7 — Explicit disposable TimetableEntry → TeachingGroup conversion.</summary>
public sealed class LegacyTimetableTeachingGroupConversionTests
{
    private sealed class AmbientCurrentUser : ICurrentUserService
    {
        public int UserId { get; set; } = 1;
        public string Role { get; set; } = "Admin";
        public int TenantId { get; set; } = 1;
        public int StaffId { get; set; }
        public int CourseId { get; set; }
        public int GroupId { get; set; }
    }

    private static (ApplicationDbContext Db, LegacyTimetableTeachingGroupConversionService Svc) CreateSut(int tenantId = 1)
    {
        var user = new AmbientCurrentUser { TenantId = tenantId };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("tg4a-p7-" + Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);
        var projector = new TimetableSectionProjector(db, user);
        var tgSections = new TeachingGroupSectionApplicationService(db, db, user, projector);
        var svc = new LegacyTimetableTeachingGroupConversionService(db, db, user, tgSections);
        return (db, svc);
    }

    private static Section NewSection(int tenantId, string code) => new()
    {
        TenantId = tenantId,
        CollegeId = 1,
        AcademicYearId = 1,
        CourseId = 1,
        GroupId = 2,
        SemesterId = 3,
        SectionCode = code,
        SectionName = "Section " + code,
        Status = "Active",
        CreatedDate = DateTime.UtcNow,
    };

    private static async Task<(Timetable Tt, TimetableEntry Entry, TeachingGroup Tg, Section SecA, Section SecB)> SeedAsync(
        ApplicationDbContext db,
        TimetableStatus status = TimetableStatus.Draft,
        bool assignTg = false,
        bool frozen = false)
    {
        var tg = new TeachingGroup
        {
            TenantId = 1,
            AcademicYearId = 1,
            CourseId = 1,
            GroupId = 2,
            SemesterId = 3,
            SubjectId = 17,
            SubjectAllocationId = 10,
            Type = TeachingGroupType.Custom,
            MembershipSource = TeachingGroupMembershipSource.ExplicitStudents,
            Status = TeachingGroupStatus.Active,
            ActivityKind = TeachingGroupActivityKind.Lecture,
            Code = "TG-P7",
            Name = "Conversion TG",
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedDate = DateTime.UtcNow,
        };
        db.Set<TeachingGroup>().Add(tg);
        var secA = NewSection(1, "A");
        var secB = NewSection(1, "B");
        db.Set<Section>().AddRange(secA, secB);

        var tt = new Timetable
        {
            TenantId = 1,
            Name = "TT",
            AcademicYearId = 1,
            Status = status,
            IsFrozen = frozen,
            CreatedDate = DateTime.UtcNow,
        };
        db.Set<Timetable>().Add(tt);
        await db.SaveChangesAsync();

        var entry = new TimetableEntry
        {
            TenantId = 1,
            TimetableId = tt.Id,
            DayOfWeek = 1,
            TimeSlotId = 1,
            SubjectAllocationId = 10,
            TeachingGroupId = assignTg ? tg.Id : null,
            StaffId = 1,
            RoomId = 1,
            DepartmentId = 1,
            CourseId = 1,
            GroupId = 2,
            SemesterId = 3,
            SubjectId = 17,
            CreatedDate = DateTime.UtcNow,
        };
        db.Set<TimetableEntry>().Add(entry);
        await db.SaveChangesAsync();
        return (tt, entry, tg, secA, secB);
    }

    [Fact]
    public async Task List_identifies_entries_with_null_TeachingGroupId()
    {
        var (db, svc) = CreateSut();
        var (tt, entry, _, _, _) = await SeedAsync(db);
        var listed = await svc.ListEntriesWithoutTeachingGroupAsync(tt.Id);
        Assert.Contains(listed, x => x.TimetableEntryId == entry.Id);
    }

    [Fact]
    public async Task Convert_assigns_TG_sections_and_projects()
    {
        var (db, svc) = CreateSut();
        var (_, entry, tg, secA, secB) = await SeedAsync(db);
        var tgCount = await db.Set<TeachingGroup>().CountAsync();

        var report = await svc.ConvertAsync(new ConvertLegacyTimetableEntriesRequest
        {
            DryRun = false,
            Items =
            [
                new LegacyTimetableEntryConversionItem
                {
                    TimetableEntryId = entry.Id,
                    TeachingGroupId = tg.Id,
                    SectionIds = [secA.Id, secB.Id],
                },
            ],
        });

        Assert.True(report.ConvertedCount == 1, report.Results.FirstOrDefault()?.Reason);
        Assert.Equal(0, report.RejectedCount);
        Assert.Equal(tg.Id, (await db.Set<TimetableEntry>().SingleAsync(e => e.Id == entry.Id)).TeachingGroupId);
        Assert.Equal(2, await db.Set<TeachingGroupSection>().CountAsync(x => x.TeachingGroupId == tg.Id));
        Assert.Equal(2, await db.Set<TimetableSection>().CountAsync(x => x.TimetableEntryId == entry.Id));
        Assert.Equal(tgCount, await db.Set<TeachingGroup>().CountAsync());
    }

    [Fact]
    public async Task DryRun_does_not_persist()
    {
        var (db, svc) = CreateSut();
        var (_, entry, tg, secA, _) = await SeedAsync(db);

        var report = await svc.ConvertAsync(new ConvertLegacyTimetableEntriesRequest
        {
            DryRun = true,
            Items =
            [
                new LegacyTimetableEntryConversionItem
                {
                    TimetableEntryId = entry.Id,
                    TeachingGroupId = tg.Id,
                    SectionIds = [secA.Id],
                },
            ],
        });

        Assert.True(report.DryRun);
        Assert.Equal(1, report.ConvertedCount);
        Assert.Null((await db.Set<TimetableEntry>().AsNoTracking().SingleAsync(e => e.Id == entry.Id)).TeachingGroupId);
        Assert.Equal(0, await db.Set<TeachingGroupSection>().CountAsync());
        Assert.Equal(0, await db.Set<TimetableSection>().CountAsync());
    }

    [Fact]
    public async Task Idempotent_second_run_skips()
    {
        var (db, svc) = CreateSut();
        var (_, entry, tg, secA, _) = await SeedAsync(db);
        var item = new LegacyTimetableEntryConversionItem
        {
            TimetableEntryId = entry.Id,
            TeachingGroupId = tg.Id,
            SectionIds = [secA.Id],
        };
        await svc.ConvertAsync(new ConvertLegacyTimetableEntriesRequest { Items = [item] });
        var report = await svc.ConvertAsync(new ConvertLegacyTimetableEntriesRequest { Items = [item] });
        Assert.Equal(1, report.SkippedCount);
        Assert.Equal(0, report.ConvertedCount);
        Assert.Equal(1, await db.Set<TimetableSection>().CountAsync(x => x.TimetableEntryId == entry.Id));
    }

    [Fact]
    public async Task Rejects_published_timetable()
    {
        var (db, svc) = CreateSut();
        var (_, entry, tg, secA, _) = await SeedAsync(db, status: TimetableStatus.Published);
        var report = await svc.ConvertAsync(new ConvertLegacyTimetableEntriesRequest
        {
            Items =
            [
                new LegacyTimetableEntryConversionItem
                {
                    TimetableEntryId = entry.Id,
                    TeachingGroupId = tg.Id,
                    SectionIds = [secA.Id],
                },
            ],
        });
        Assert.Equal(1, report.RejectedCount);
        Assert.Contains("read-only", report.Results[0].Reason!, StringComparison.OrdinalIgnoreCase);
        Assert.Null((await db.Set<TimetableEntry>().SingleAsync(e => e.Id == entry.Id)).TeachingGroupId);
    }

    [Fact]
    public async Task Rejects_missing_TeachingGroupId_without_inference()
    {
        var (db, svc) = CreateSut();
        var (_, entry, _, secA, _) = await SeedAsync(db);
        var report = await svc.ConvertAsync(new ConvertLegacyTimetableEntriesRequest
        {
            Items =
            [
                new LegacyTimetableEntryConversionItem
                {
                    TimetableEntryId = entry.Id,
                    TeachingGroupId = 0,
                    SectionIds = [secA.Id],
                },
            ],
        });
        Assert.Equal(1, report.RejectedCount);
        Assert.Contains("never inferred", report.Results[0].Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_incompatible_TeachingGroup()
    {
        var (db, svc) = CreateSut();
        var (_, entry, _, secA, _) = await SeedAsync(db);
        var other = new TeachingGroup
        {
            TenantId = 1,
            AcademicYearId = 1,
            CourseId = 99,
            GroupId = 2,
            SemesterId = 3,
            SubjectId = 17,
            SubjectAllocationId = 10,
            Type = TeachingGroupType.Custom,
            MembershipSource = TeachingGroupMembershipSource.ExplicitStudents,
            Status = TeachingGroupStatus.Active,
            ActivityKind = TeachingGroupActivityKind.Lecture,
            Code = "TG-BAD",
            Name = "Bad",
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedDate = DateTime.UtcNow,
        };
        db.Set<TeachingGroup>().Add(other);
        await db.SaveChangesAsync();

        var report = await svc.ConvertAsync(new ConvertLegacyTimetableEntriesRequest
        {
            Items =
            [
                new LegacyTimetableEntryConversionItem
                {
                    TimetableEntryId = entry.Id,
                    TeachingGroupId = other.Id,
                    SectionIds = [secA.Id],
                },
            ],
        });
        Assert.Equal(1, report.RejectedCount);
        Assert.Null((await db.Set<TimetableEntry>().SingleAsync(e => e.Id == entry.Id)).TeachingGroupId);
    }

    [Fact]
    public void Does_not_touch_StudentSection_or_Attendance()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "LegacyTimetableTeachingGroupConversionService.cs"));
        Assert.DoesNotContain("StudentSection", src);
        Assert.DoesNotContain("Attendances", src);
        Assert.DoesNotContain("AttendanceSession", src);
        Assert.DoesNotContain("new TeachingGroup", src);
        Assert.DoesNotContain("SchedulingSubjectAllocations", src);
        Assert.DoesNotContain("FirstOrDefaultAsync(x => x.SubjectAllocationId", src);
        Assert.Contains("ReplaceSectionsAndProjectAsync", src);
        Assert.Contains("EnsureDraft", src);
        Assert.Contains("EnsureCompatibleWithTimetableEntry", src);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Abhyanvaya.Infrastructure", "Abhyanvaya.Infrastructure.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found.");
    }
}
