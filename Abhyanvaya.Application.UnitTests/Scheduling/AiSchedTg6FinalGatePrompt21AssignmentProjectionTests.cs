using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Infrastructure.Persistence;
using Abhyanvaya.Infrastructure.Persistence.Repositories.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>
/// AI-SCHED-TG.6 Final Gate Prompt 21 — Assign/clear → TimetableSection projection consistency
/// and Attendance timetable-mode consumption of projected sections.
/// </summary>
public sealed class AiSchedTg6FinalGatePrompt21AssignmentProjectionTests
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

    private static (ApplicationDbContext Db, TeachingGroupApplicationService Assign, TeachingGroupSectionApplicationService Sections, TimetableSectionProjector Projector, AmbientCurrentUser User)
        CreateSut(int tenantId = 1)
    {
        var user = new AmbientCurrentUser { TenantId = tenantId };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("tg6-p21-" + Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);
        var projector = new TimetableSectionProjector(db, user);
        var sections = new TeachingGroupSectionApplicationService(db, db, user, projector);
        var assign = new TeachingGroupApplicationService(new TimetableRepository(db), db, db, user, projector);
        return (db, assign, sections, projector, user);
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
        int tenantId = 1,
        bool withSections = true)
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
            Code = "TG-01",
            Name = "TG-01",
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedDate = DateTime.UtcNow,
        };
        db.Set<TeachingGroup>().Add(tg);

        var secA = NewSection(tenantId, "5");
        var secB = NewSection(tenantId, "6");
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
            TeachingGroupId = null,
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

        if (withSections)
        {
            db.Set<TeachingGroupSection>().AddRange(
                new TeachingGroupSection
                {
                    TenantId = tenantId,
                    TeachingGroupId = tg.Id,
                    SectionId = secA.Id,
                    IsPrimary = true,
                    CreatedDate = DateTime.UtcNow,
                },
                new TeachingGroupSection
                {
                    TenantId = tenantId,
                    TeachingGroupId = tg.Id,
                    SectionId = secB.Id,
                    IsPrimary = false,
                    CreatedDate = DateTime.UtcNow,
                });
            await db.SaveChangesAsync();
        }

        return (tt, entry, tg, secA, secB);
    }

    private static async Task<List<int>> ActiveProjectedSectionIdsAsync(ApplicationDbContext db, int entryId)
        => await db.Set<TimetableSection>()
            .Where(x => x.TimetableEntryId == entryId && !x.IsDeleted)
            .Select(x => x.SectionId)
            .OrderBy(x => x)
            .ToListAsync();

    [Fact]
    public async Task Assign_projects_TeachingGroupSection_onto_entry_TimetableSection()
    {
        var (db, assign, _, _, _) = CreateSut();
        var (_, entry, tg, secA, secB) = await SeedAsync(db);

        Assert.Empty(await ActiveProjectedSectionIdsAsync(db, entry.Id));

        var result = await assign.AssignToTimetableEntryAsync(entry.Id, tg.Id);

        Assert.Equal(tg.Id, result.TeachingGroupId);
        var projected = await ActiveProjectedSectionIdsAsync(db, entry.Id);
        Assert.Equal(new[] { secA.Id, secB.Id }.OrderBy(x => x), projected);
        // SoT unchanged
        Assert.Equal(2, await db.Set<TeachingGroupSection>().CountAsync(x => x.TeachingGroupId == tg.Id));
    }

    [Fact]
    public async Task Clear_soft_deletes_entry_projection_without_mutating_TeachingGroupSection()
    {
        var (db, assign, _, _, _) = CreateSut();
        var (_, entry, tg, secA, secB) = await SeedAsync(db);
        await assign.AssignToTimetableEntryAsync(entry.Id, tg.Id);
        Assert.Equal(2, (await ActiveProjectedSectionIdsAsync(db, entry.Id)).Count);

        var cleared = await assign.ClearFromTimetableEntryAsync(entry.Id);

        Assert.Null(cleared.TeachingGroupId);
        Assert.Empty(await ActiveProjectedSectionIdsAsync(db, entry.Id));
        Assert.Equal(2, await db.Set<TimetableSection>().IgnoreQueryFilters()
            .CountAsync(x => x.TimetableEntryId == entry.Id && x.IsDeleted));
        Assert.Equal(2, await db.Set<TeachingGroupSection>().CountAsync(x => x.TeachingGroupId == tg.Id));
        Assert.Contains(secA.Id, await db.Set<TeachingGroupSection>().Where(x => x.TeachingGroupId == tg.Id).Select(x => x.SectionId).ToListAsync());
        Assert.Contains(secB.Id, await db.Set<TeachingGroupSection>().Where(x => x.TeachingGroupId == tg.Id).Select(x => x.SectionId).ToListAsync());
    }

    [Fact]
    public async Task Shared_TeachingGroup_section_change_projects_all_bound_entries()
    {
        var (db, assign, sections, _, _) = CreateSut();
        var (tt, e100, tg, secA, secB) = await SeedAsync(db);

        var e101 = new TimetableEntry
        {
            TenantId = 1,
            TimetableId = tt.Id,
            DayOfWeek = 2,
            TimeSlotId = 1,
            SubjectAllocationId = 10,
            TeachingGroupId = null,
            StaffId = 1,
            RoomId = 1,
            DepartmentId = 1,
            CourseId = 1,
            GroupId = 2,
            SemesterId = 3,
            SubjectId = 17,
            CreatedDate = DateTime.UtcNow,
        };
        var e102 = new TimetableEntry
        {
            TenantId = 1,
            TimetableId = tt.Id,
            DayOfWeek = 3,
            TimeSlotId = 1,
            SubjectAllocationId = 10,
            TeachingGroupId = null,
            StaffId = 1,
            RoomId = 1,
            DepartmentId = 1,
            CourseId = 1,
            GroupId = 2,
            SemesterId = 3,
            SubjectId = 17,
            CreatedDate = DateTime.UtcNow,
        };
        db.Set<TimetableEntry>().AddRange(e101, e102);
        await db.SaveChangesAsync();

        await assign.AssignToTimetableEntryAsync(e100.Id, tg.Id);
        await assign.AssignToTimetableEntryAsync(e101.Id, tg.Id);
        await assign.AssignToTimetableEntryAsync(e102.Id, tg.Id);

        await sections.ReplaceSectionsAndProjectAsync(tg.Id, [secA.Id]);

        foreach (var id in new[] { e100.Id, e101.Id, e102.Id })
            Assert.Equal(new[] { secA.Id }, await ActiveProjectedSectionIdsAsync(db, id));

        await sections.ReplaceSectionsAndProjectAsync(tg.Id, [secB.Id]);
        foreach (var id in new[] { e100.Id, e101.Id, e102.Id })
            Assert.Equal(new[] { secB.Id }, await ActiveProjectedSectionIdsAsync(db, id));
    }

    [Fact]
    public async Task Assign_projection_is_idempotent()
    {
        var (db, assign, _, projector, _) = CreateSut();
        var (_, entry, tg, secA, secB) = await SeedAsync(db);

        await assign.AssignToTimetableEntryAsync(entry.Id, tg.Id);
        await assign.AssignToTimetableEntryAsync(entry.Id, tg.Id);
        await projector.SyncTeachingGroupSectionsToTimetableEntryAsync(entry.Id);
        await db.SaveChangesAsync();

        var active = await ActiveProjectedSectionIdsAsync(db, entry.Id);
        Assert.Equal(new[] { secA.Id, secB.Id }.OrderBy(x => x), active);
        Assert.Equal(2, await db.Set<TimetableSection>().CountAsync(x => x.TimetableEntryId == entry.Id && !x.IsDeleted));
    }

    [Fact]
    public async Task Cross_tenant_assign_is_rejected_and_creates_no_projection()
    {
        var (db, assign, _, _, _) = CreateSut(tenantId: 1);
        var (_, entry, _, _, _) = await SeedAsync(db, tenantId: 1);

        var foreignTg = new TeachingGroup
        {
            TenantId = 2,
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
            Code = "TG-B",
            Name = "Foreign",
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedDate = DateTime.UtcNow,
        };
        db.Set<TeachingGroup>().Add(foreignTg);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            assign.AssignToTimetableEntryAsync(entry.Id, foreignTg.Id));

        Assert.Null((await db.Set<TimetableEntry>().AsNoTracking().SingleAsync(e => e.Id == entry.Id)).TeachingGroupId);
        Assert.Empty(await ActiveProjectedSectionIdsAsync(db, entry.Id));
    }

    [Fact]
    public async Task Attendance_timetable_mode_reads_projected_sections_after_assign_and_replacement()
    {
        var (db, assign, sections, _, _) = CreateSut();
        var (_, entry, tg, secA, secB) = await SeedAsync(db);

        // Assign/clear require Draft — perform mutations first, then assert Attendance read of projection.
        await assign.AssignToTimetableEntryAsync(entry.Id, tg.Id);

        // Scenario A — both sections projected (Attendance Timetable mode reads TimetableSection).
        var sectionIds = await (
            from ts in db.TimetableSections.AsNoTracking()
            where ts.TimetableEntryId == entry.Id
            select ts.SectionId).OrderBy(x => x).ToListAsync();
        Assert.Equal(new[] { secA.Id, secB.Id }.OrderBy(x => x), sectionIds);

        // Scenario B — replace SoT → Section 6 only
        await sections.ReplaceSectionsAndProjectAsync(tg.Id, [secB.Id]);
        sectionIds = await (
            from ts in db.TimetableSections.AsNoTracking()
            where ts.TimetableEntryId == entry.Id
            select ts.SectionId).ToListAsync();
        Assert.Equal(new[] { secB.Id }, sectionIds);

        // Scenario C already covered by multi-section assign above.

        // Scenario D — clear TG → no active projection for Attendance timetable-mode enrichment
        await assign.ClearFromTimetableEntryAsync(entry.Id);
        Assert.Empty(await ActiveProjectedSectionIdsAsync(db, entry.Id));

        // Scenario E — Attendance resolve must not create/infer TG
        var resolverSrc = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "Conflicts", "AttendanceSessionResolver.cs"));
        Assert.DoesNotContain("new TeachingGroup", resolverSrc);
        Assert.DoesNotContain("CreateTeachingGroup", resolverSrc);
        Assert.DoesNotContain("AssignToTimetableEntry", resolverSrc);
        Assert.DoesNotContain("SaveChanges", resolverSrc);
    }

    [Fact]
    public void Projector_remains_sole_writer_and_persistence_agnostic()
    {
        var projector = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TimetableSectionProjector.cs"));
        Assert.Contains("ClearTimetableEntryProjectionAsync", projector);
        Assert.DoesNotContain("SaveChangesAsync", projector);
        Assert.DoesNotContain("SaveChanges(", projector);

        var assign = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TeachingGroupApplicationService.cs"));
        Assert.DoesNotContain("new TimetableSection", assign);
        Assert.Contains("SyncTeachingGroupSectionsToTimetableEntryAsync", assign);
        Assert.Contains("ClearTimetableEntryProjectionAsync", assign);
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
