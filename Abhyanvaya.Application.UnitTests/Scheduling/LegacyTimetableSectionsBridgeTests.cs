using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-TG.4A Prompt 5 — Legacy PUT /sections through TeachingGroup boundary.</summary>
public sealed class LegacyTimetableSectionsBridgeTests
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

    private static (ApplicationDbContext Db, SectionManagementService Service, AmbientCurrentUser User) CreateSut(int tenantId = 1)
    {
        var user = new AmbientCurrentUser { TenantId = tenantId };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("tg4a-p5-" + Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);
        var projector = new TimetableSectionProjector(db, user);
        var tgSections = new TeachingGroupSectionApplicationService(db, db, user, projector);
        var capacity = new Mock<ISectionCapacityEngine>();
        var versions = new Mock<ISectionVersioningService>();
        var service = new SectionManagementService(db, user, capacity.Object, versions.Object, tgSections);
        return (db, service, user);
    }

    private static async Task<(Timetable Tt, TimetableEntry Entry, TeachingGroup Tg, Section SecA, Section SecB)> SeedAsync(
        ApplicationDbContext db,
        int tenantId = 1,
        bool assignTg = true)
    {
        var tg = new TeachingGroup
        {
            TenantId = tenantId,
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
            Code = "TG-P5",
            Name = "Bridge TG",
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedDate = DateTime.UtcNow,
        };
        db.Set<TeachingGroup>().Add(tg);

        var secA = NewSection(tenantId, "A");
        var secB = NewSection(tenantId, "B");
        db.Set<Section>().AddRange(secA, secB);

        var tt = new Timetable
        {
            TenantId = tenantId,
            Name = "Draft",
            AcademicYearId = 1,
            Status = TimetableStatus.Draft,
            CreatedDate = DateTime.UtcNow,
        };
        db.Set<Timetable>().Add(tt);
        await db.SaveChangesAsync();

        var entry = new TimetableEntry
        {
            TenantId = tenantId,
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

    [Fact]
    public async Task Legacy_PUT_updates_TeachingGroupSection_and_projects_TimetableSection()
    {
        var (db, service, _) = CreateSut();
        var (tt, entry, tg, secA, secB) = await SeedAsync(db);

        var result = await service.SetTimetableSectionsAsync(tt.Id, new SetTimetableSectionsRequest
        {
            TimetableEntryId = entry.Id,
            SectionIds = [secA.Id, secB.Id],
        });

        Assert.Equal(2, await db.Set<TeachingGroupSection>().CountAsync(x => x.TeachingGroupId == tg.Id));
        Assert.Equal(2, await db.Set<TimetableSection>().CountAsync(x => x.TimetableEntryId == entry.Id));
        Assert.Contains(result, x => x.SectionId == secA.Id && x.TimetableEntryId == entry.Id);
        Assert.Contains(result, x => x.SectionId == secB.Id && x.TimetableEntryId == entry.Id);
    }

    [Fact]
    public async Task Legacy_request_shape_remains_compatible()
    {
        var (db, service, _) = CreateSut();
        var (tt, entry, _, secA, _) = await SeedAsync(db);

        var dto = await service.SetTimetableSectionsAsync(tt.Id, new SetTimetableSectionsRequest
        {
            TimetableEntryId = entry.Id,
            SectionIds = [secA.Id],
        });

        Assert.All(dto, row =>
        {
            Assert.True(row.Id > 0);
            Assert.Equal(tt.Id, row.TimetableId);
            Assert.NotNull(row.SectionCode);
        });
    }

    [Fact]
    public async Task No_TeachingGroupId_rejects_without_creating_TeachingGroup()
    {
        var (db, service, _) = CreateSut();
        var (tt, entry, _, secA, _) = await SeedAsync(db, assignTg: false);
        var before = await db.Set<TeachingGroup>().IgnoreQueryFilters().CountAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.SetTimetableSectionsAsync(tt.Id, new SetTimetableSectionsRequest
        {
            TimetableEntryId = entry.Id,
            SectionIds = [secA.Id],
        }));
        Assert.Contains("Teaching Group", ex.Message);
        Assert.Equal(before, await db.Set<TeachingGroup>().IgnoreQueryFilters().CountAsync());
        Assert.Empty(await db.Set<TimetableSection>().Where(x => x.TimetableEntryId == entry.Id).ToListAsync());
    }

    [Fact]
    public async Task Missing_TimetableEntryId_is_rejected()
    {
        var (db, service, _) = CreateSut();
        var (tt, _, _, secA, _) = await SeedAsync(db);
        await Assert.ThrowsAsync<DomainException>(() => service.SetTimetableSectionsAsync(tt.Id, new SetTimetableSectionsRequest
        {
            TimetableEntryId = null,
            SectionIds = [secA.Id],
        }));
    }

    [Fact]
    public async Task Locked_timetable_is_rejected()
    {
        var (db, service, _) = CreateSut();
        var (tt, entry, _, secA, _) = await SeedAsync(db);
        tt.Status = TimetableStatus.Locked;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() => service.SetTimetableSectionsAsync(tt.Id, new SetTimetableSectionsRequest
        {
            TimetableEntryId = entry.Id,
            SectionIds = [secA.Id],
        }));
    }

    [Fact]
    public async Task No_automatic_TeachingGroup_creation_on_success_path()
    {
        var (db, service, _) = CreateSut();
        var (tt, entry, tg, secA, _) = await SeedAsync(db);
        var before = await db.Set<TeachingGroup>().IgnoreQueryFilters().CountAsync();
        await service.SetTimetableSectionsAsync(tt.Id, new SetTimetableSectionsRequest
        {
            TimetableEntryId = entry.Id,
            SectionIds = [secA.Id],
        });
        Assert.Equal(before, await db.Set<TeachingGroup>().IgnoreQueryFilters().CountAsync());
        Assert.Equal(tg.Id, (await db.Set<TimetableEntry>().AsNoTracking().SingleAsync(e => e.Id == entry.Id)).TeachingGroupId);
    }

    [Fact]
    public void SetTimetableSections_no_longer_constructs_TimetableSection_directly()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Academic", "SectionManagementService.cs"));
        var start = src.IndexOf("SetTimetableSectionsAsync", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = src.IndexOf("AutoAllocateAsync", start, StringComparison.Ordinal);
        var method = src.Substring(start, end - start);
        Assert.DoesNotContain("new TimetableSection", method);
        Assert.Contains("ReplaceSectionsAndProjectAsync", method);
        Assert.DoesNotContain("CreateTeachingGroup", method);
        Assert.DoesNotContain("SubjectAllocation", method);
        Assert.DoesNotContain(".IgnoreQueryFilters", method);
    }

    [Fact]
    public void Api_contract_and_authorization_unchanged()
    {
        var controller = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.API", "Controllers", "SectionsController.cs"));
        Assert.Contains("[Route(\"api/timetable/{timetableId:int}/sections\")]", controller);
        Assert.Contains("CanManageSchedulingTimetable", controller);
        Assert.Contains("CanViewSchedulingTimetable", controller);
        Assert.Contains("SetTimetableSectionsRequest", controller);
        Assert.Contains("DomainException", controller);
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
