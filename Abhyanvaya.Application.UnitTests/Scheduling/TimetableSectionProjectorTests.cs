using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-TG.4A Prompt 4 — TimetableSection projection from TeachingGroupSection.</summary>
public sealed class TimetableSectionProjectorTests
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

    private static (ApplicationDbContext Db, TimetableSectionProjector Projector, TeachingGroupSectionApplicationService Sections, AmbientCurrentUser User)
        CreateSut(int tenantId = 1)
    {
        var user = new AmbientCurrentUser { TenantId = tenantId };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("tg4a-p4-" + Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);
        var projector = new TimetableSectionProjector(db, user);
        var sections = new TeachingGroupSectionApplicationService(db, db, user, projector);
        return (db, projector, sections, user);
    }

    private static async Task<(TeachingGroup Tg, Timetable Tt, TimetableEntry Entry, Section SecA, Section SecB)> SeedAsync(
        ApplicationDbContext db,
        int tenantId = 1,
        TeachingGroupStatus status = TeachingGroupStatus.Active)
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
            Status = status,
            ActivityKind = TeachingGroupActivityKind.Lecture,
            Code = "TG-P4",
            Name = "Projection TG",
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
            TeachingGroupId = tg.Id,
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
        return (tg, tt, entry, secA, secB);
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
    public async Task One_TG_one_section_projects()
    {
        var (db, _, sections, _) = CreateSut();
        var (tg, _, entry, secA, _) = await SeedAsync(db);

        await sections.ReplaceSectionsAndProjectAsync(tg.Id, [secA.Id]);

        var rows = await db.Set<TimetableSection>().Where(x => x.TimetableEntryId == entry.Id).ToListAsync();
        Assert.Single(rows);
        Assert.Equal(secA.Id, rows[0].SectionId);
        Assert.Equal(entry.TimetableId, rows[0].TimetableId);
    }

    [Fact]
    public async Task One_TG_multiple_sections_projects()
    {
        var (db, _, sections, _) = CreateSut();
        var (tg, _, entry, secA, secB) = await SeedAsync(db);

        await sections.ReplaceSectionsAndProjectAsync(tg.Id, [secA.Id, secB.Id]);
        var rows = await db.Set<TimetableSection>().Where(x => x.TimetableEntryId == entry.Id).ToListAsync();
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public async Task Remove_one_section_soft_deletes_projection_row()
    {
        var (db, _, sections, _) = CreateSut();
        var (tg, _, entry, secA, secB) = await SeedAsync(db);
        await sections.ReplaceSectionsAndProjectAsync(tg.Id, [secA.Id, secB.Id]);
        await sections.ReplaceSectionsAndProjectAsync(tg.Id, [secA.Id]);

        var active = await db.Set<TimetableSection>().Where(x => x.TimetableEntryId == entry.Id).ToListAsync();
        Assert.Single(active);
        Assert.Equal(secA.Id, active[0].SectionId);

        var deleted = await db.Set<TimetableSection>().IgnoreQueryFilters()
            .Where(x => x.TimetableEntryId == entry.Id && x.IsDeleted)
            .ToListAsync();
        Assert.Contains(deleted, x => x.SectionId == secB.Id);
    }

    [Fact]
    public async Task Remove_all_sections_clears_projection()
    {
        var (db, _, sections, _) = CreateSut();
        var (tg, _, entry, secA, _) = await SeedAsync(db);
        await sections.ReplaceSectionsAndProjectAsync(tg.Id, [secA.Id]);
        await sections.ReplaceSectionsAndProjectAsync(tg.Id, []);
        Assert.Empty(await db.Set<TimetableSection>().Where(x => x.TimetableEntryId == entry.Id).ToListAsync());
    }

    [Fact]
    public async Task Repeat_projection_is_idempotent()
    {
        var (db, projector, sections, _) = CreateSut();
        var (tg, _, entry, secA, secB) = await SeedAsync(db);
        await sections.ReplaceSectionsAndProjectAsync(tg.Id, [secA.Id, secB.Id]);

        await projector.SyncTeachingGroupSectionsToTimetableEntriesAsync(tg.Id, [secA.Id, secB.Id]);
        await db.SaveChangesAsync();
        await projector.SyncTeachingGroupSectionsToTimetableEntriesAsync(tg.Id, [secA.Id, secB.Id]);
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.Set<TimetableSection>().CountAsync(x => x.TimetableEntryId == entry.Id));
    }

    [Fact]
    public async Task No_duplicate_active_TimetableSection_rows()
    {
        var (db, projector, sections, _) = CreateSut();
        var (tg, _, entry, secA, _) = await SeedAsync(db);
        await sections.ReplaceSectionsAndProjectAsync(tg.Id, [secA.Id]);
        await projector.SyncTeachingGroupSectionsToTimetableEntriesAsync(tg.Id, [secA.Id]);
        await db.SaveChangesAsync();

        var active = await db.Set<TimetableSection>()
            .Where(x => x.TimetableEntryId == entry.Id && x.SectionId == secA.Id)
            .ToListAsync();
        Assert.Single(active);
    }

    [Fact]
    public async Task Projects_all_entries_sharing_TeachingGroup()
    {
        var (db, _, sections, _) = CreateSut();
        var (tg, tt, entry1, secA, _) = await SeedAsync(db);
        var entry2 = new TimetableEntry
        {
            TenantId = 1,
            TimetableId = tt.Id,
            DayOfWeek = 2,
            TimeSlotId = 1,
            SubjectAllocationId = 10,
            TeachingGroupId = tg.Id,
            StaffId = 1,
            RoomId = 1,
            DepartmentId = 1,
            CourseId = 1,
            GroupId = 2,
            SemesterId = 3,
            SubjectId = 17,
            CreatedDate = DateTime.UtcNow,
        };
        db.Set<TimetableEntry>().Add(entry2);
        await db.SaveChangesAsync();

        await sections.ReplaceSectionsAndProjectAsync(tg.Id, [secA.Id]);
        Assert.Single(await db.Set<TimetableSection>().Where(x => x.TimetableEntryId == entry1.Id).ToListAsync());
        Assert.Single(await db.Set<TimetableSection>().Where(x => x.TimetableEntryId == entry2.Id).ToListAsync());
    }

    [Fact]
    public async Task Does_not_change_TeachingGroupId_or_create_TeachingGroup()
    {
        var (db, _, sections, _) = CreateSut();
        var (tg, _, entry, secA, _) = await SeedAsync(db);
        var tgCount = await db.Set<TeachingGroup>().IgnoreQueryFilters().CountAsync();
        await sections.ReplaceSectionsAndProjectAsync(tg.Id, [secA.Id]);

        var reloaded = await db.Set<TimetableEntry>().AsNoTracking().SingleAsync(e => e.Id == entry.Id);
        Assert.Equal(tg.Id, reloaded.TeachingGroupId);
        Assert.Equal(tgCount, await db.Set<TeachingGroup>().IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Entry_without_TeachingGroupId_rejects_single_entry_sync()
    {
        var (db, projector, _, _) = CreateSut();
        var (_, tt, entry, _, _) = await SeedAsync(db);
        entry.TeachingGroupId = null;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            projector.SyncTeachingGroupSectionsToTimetableEntryAsync(entry.Id));
    }

    [Fact]
    public async Task Cross_tenant_TeachingGroup_projection_is_not_found()
    {
        var (db, projector, _, _) = CreateSut(tenantId: 1);
        var (tg, _, _, secA, _) = await SeedAsync(db, tenantId: 2);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            projector.SyncTeachingGroupSectionsToTimetableEntriesAsync(tg.Id, [secA.Id]));
    }

    [Fact]
    public async Task Single_commit_path_does_not_leave_orphan_SoT_without_projection_on_success()
    {
        var (db, _, sections, _) = CreateSut();
        var (tg, _, entry, secA, secB) = await SeedAsync(db);
        await sections.ReplaceSectionsAndProjectAsync(tg.Id, [secA.Id, secB.Id]);

        Assert.Equal(2, await db.Set<TeachingGroupSection>().CountAsync(x => x.TeachingGroupId == tg.Id));
        Assert.Equal(2, await db.Set<TimetableSection>().CountAsync(x => x.TimetableEntryId == entry.Id));
    }

    [Fact]
    public async Task Projector_does_not_write_StudentSection()
    {
        var (db, _, sections, _) = CreateSut();
        var (tg, _, _, secA, _) = await SeedAsync(db);
        await sections.ReplaceSectionsAndProjectAsync(tg.Id, [secA.Id]);
        Assert.Empty(await db.Set<StudentSection>().IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public void Projector_source_has_no_SaveChanges_or_inference()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TimetableSectionProjector.cs"));
        Assert.DoesNotContain("SaveChangesAsync", src);
        Assert.DoesNotContain("CreateTeachingGroup", src);
        Assert.DoesNotContain("FindTeachingGroup", src);
        Assert.DoesNotContain("entry.TeachingGroupId =", src);
        Assert.DoesNotContain(".IgnoreQueryFilters", src);
        Assert.DoesNotContain("new StudentSection", src);
        Assert.DoesNotContain("AttendanceSession", src);
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
