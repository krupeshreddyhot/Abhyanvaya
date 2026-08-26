using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-TG.4A Prompt 9 — End-to-end acceptance scenarios for the legacy bridge + SoT.</summary>
public sealed class AiSchedTg4APrompt9EndToEndAcceptanceTests
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

    private static (ApplicationDbContext Db, SectionManagementService Sections, AmbientCurrentUser User) CreateSectionsSut(int tenantId = 1)
    {
        var user = new AmbientCurrentUser { TenantId = tenantId };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("tg4a-p9-" + Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);
        var projector = new TimetableSectionProjector(db, user);
        var tgSections = new TeachingGroupSectionApplicationService(db, db, user, projector);
        var capacity = new Mock<ISectionCapacityEngine>();
        var versions = new Mock<ISectionVersioningService>();
        var service = new SectionManagementService(db, user, capacity.Object, versions.Object, tgSections);
        return (db, service, user);
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
        bool assignTg = true,
        TimetableStatus status = TimetableStatus.Draft)
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
            Code = "TG-P9",
            Name = "E2E TG",
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
            Name = "E2E",
            AcademicYearId = 1,
            Status = status,
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

    // --- Scenarios 1–5: happy paths + no duplicates ---

    [Fact]
    public async Task S1_single_section_bridge_projects_once()
    {
        var (db, service, _) = CreateSectionsSut();
        var (tt, entry, tg, secA, _) = await SeedAsync(db);
        await service.SetTimetableSectionsAsync(tt.Id, new SetTimetableSectionsRequest
        {
            TimetableEntryId = entry.Id,
            SectionIds = [secA.Id],
        });
        Assert.Equal(1, await db.Set<TeachingGroupSection>().CountAsync(x => x.TeachingGroupId == tg.Id));
        Assert.Equal(1, await db.Set<TimetableSection>().CountAsync(x => x.TimetableEntryId == entry.Id));
    }

    [Fact]
    public async Task S2_combined_sections_no_duplicate_SoT_or_projection()
    {
        var (db, service, _) = CreateSectionsSut();
        var (tt, entry, tg, secA, secB) = await SeedAsync(db);
        await service.SetTimetableSectionsAsync(tt.Id, new SetTimetableSectionsRequest
        {
            TimetableEntryId = entry.Id,
            SectionIds = [secA.Id, secB.Id, secA.Id],
        });
        Assert.Equal(2, await db.Set<TeachingGroupSection>().CountAsync(x => x.TeachingGroupId == tg.Id));
        Assert.Equal(2, await db.Set<TimetableSection>().CountAsync(x => x.TimetableEntryId == entry.Id));
    }

    [Fact]
    public async Task S3_zero_sections_clears_projection()
    {
        var (db, service, _) = CreateSectionsSut();
        var (tt, entry, tg, secA, _) = await SeedAsync(db);
        await service.SetTimetableSectionsAsync(tt.Id, new SetTimetableSectionsRequest
        {
            TimetableEntryId = entry.Id,
            SectionIds = [secA.Id],
        });
        await service.SetTimetableSectionsAsync(tt.Id, new SetTimetableSectionsRequest
        {
            TimetableEntryId = entry.Id,
            SectionIds = [],
        });
        Assert.Empty(await db.Set<TeachingGroupSection>().Where(x => x.TeachingGroupId == tg.Id).ToListAsync());
        Assert.Empty(await db.Set<TimetableSection>().Where(x => x.TimetableEntryId == entry.Id).ToListAsync());
    }

    [Fact]
    public async Task S4_idempotent_replace_does_not_duplicate()
    {
        var (db, service, _) = CreateSectionsSut();
        var (tt, entry, tg, secA, secB) = await SeedAsync(db);
        var req = new SetTimetableSectionsRequest
        {
            TimetableEntryId = entry.Id,
            SectionIds = [secA.Id, secB.Id],
        };
        await service.SetTimetableSectionsAsync(tt.Id, req);
        await service.SetTimetableSectionsAsync(tt.Id, req);
        Assert.Equal(2, await db.Set<TeachingGroupSection>().CountAsync(x => x.TeachingGroupId == tg.Id));
        Assert.Equal(2, await db.Set<TimetableSection>().CountAsync(x => x.TimetableEntryId == entry.Id));
    }

    [Fact]
    public async Task S5_shared_TG_projects_all_bound_entries()
    {
        var (db, service, _) = CreateSectionsSut();
        var (tt, entry1, tg, secA, _) = await SeedAsync(db);
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

        await service.SetTimetableSectionsAsync(tt.Id, new SetTimetableSectionsRequest
        {
            TimetableEntryId = entry1.Id,
            SectionIds = [secA.Id],
        });

        Assert.Single(await db.Set<TimetableSection>().Where(x => x.TimetableEntryId == entry1.Id).ToListAsync());
        Assert.Single(await db.Set<TimetableSection>().Where(x => x.TimetableEntryId == entry2.Id).ToListAsync());
    }

    // --- Scenarios 6–9: rejection / isolation ---

    [Fact]
    public async Task S6_wrong_tenant_entry_is_not_found_no_mutation()
    {
        var (db, service, _) = CreateSectionsSut(tenantId: 1);
        var (tt, entry, tg, secA, _) = await SeedAsync(db, tenantId: 2, assignTg: true);
        var beforeSot = await db.Set<TeachingGroupSection>().IgnoreQueryFilters().CountAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.SetTimetableSectionsAsync(tt.Id, new SetTimetableSectionsRequest
            {
                TimetableEntryId = entry.Id,
                SectionIds = [secA.Id],
            }));
        Assert.Equal(beforeSot, await db.Set<TeachingGroupSection>().IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.Set<TimetableSection>().IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task S7_incompatible_section_scope_rejects_without_mutation()
    {
        var (db, service, _) = CreateSectionsSut();
        var (tt, entry, tg, _, _) = await SeedAsync(db);
        var bad = NewSection(1, "X");
        bad.CourseId = 999;
        db.Set<Section>().Add(bad);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.SetTimetableSectionsAsync(tt.Id, new SetTimetableSectionsRequest
            {
                TimetableEntryId = entry.Id,
                SectionIds = [bad.Id],
            }));
        Assert.Contains("compatible", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await db.Set<TeachingGroupSection>().Where(x => x.TeachingGroupId == tg.Id).ToListAsync());
        Assert.Empty(await db.Set<TimetableSection>().Where(x => x.TimetableEntryId == entry.Id).ToListAsync());
    }

    [Fact]
    public async Task S8_missing_TeachingGroup_is_not_auto_created()
    {
        var (db, service, _) = CreateSectionsSut();
        var (tt, entry, _, secA, _) = await SeedAsync(db, assignTg: true);
        entry.TeachingGroupId = 99999;
        await db.SaveChangesAsync();
        var tgCount = await db.Set<TeachingGroup>().IgnoreQueryFilters().CountAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.SetTimetableSectionsAsync(tt.Id, new SetTimetableSectionsRequest
            {
                TimetableEntryId = entry.Id,
                SectionIds = [secA.Id],
            }));
        Assert.Equal(tgCount, await db.Set<TeachingGroup>().IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task S9_null_TeachingGroupId_rejects_without_inference_or_create()
    {
        var (db, service, _) = CreateSectionsSut();
        var (tt, entry, _, secA, _) = await SeedAsync(db, assignTg: false);
        var tgCount = await db.Set<TeachingGroup>().IgnoreQueryFilters().CountAsync();
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.SetTimetableSectionsAsync(tt.Id, new SetTimetableSectionsRequest
            {
                TimetableEntryId = entry.Id,
                SectionIds = [secA.Id],
            }));
        Assert.Contains("Teaching Group", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(tgCount, await db.Set<TeachingGroup>().IgnoreQueryFilters().CountAsync());
        Assert.Null((await db.Set<TimetableEntry>().SingleAsync(e => e.Id == entry.Id)).TeachingGroupId);
    }

    // --- Scenario 10: Attendance ---

    [Fact]
    public async Task S10_Attendance_Timetable_and_Legacy_paths_preserved()
    {
        var (db, _, user) = CreateSectionsSut();
        user.StaffId = 0;
        var legacy = await new AttendanceSessionResolver(db, user).ResolveAsync(null, DateOnly.FromDateTime(DateTime.UtcNow));
        Assert.Equal("Legacy", legacy.Mode);

        var resolverSrc = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "Conflicts", "AttendanceSessionResolver.cs"));
        Assert.Contains("TimetableSections.AsNoTracking", resolverSrc);
        Assert.Contains("Mode = \"Legacy\"", resolverSrc);
        Assert.DoesNotContain("new TeachingGroup", resolverSrc);
        Assert.DoesNotContain("ReplaceSectionsAndProjectAsync", resolverSrc);
    }

    // --- Scenarios 11–12: clone / version coherence ---

    [Fact]
    public void S11_CloneEntry_preserves_TeachingGroupId_and_does_not_write_TimetableSection()
    {
        var source = new TimetableEntry
        {
            Id = 7,
            TenantId = 1,
            TimetableId = 3,
            TeachingGroupId = 42,
            SubjectAllocationId = 10,
            DayOfWeek = 1,
            TimeSlotId = 1,
            StaffId = 1,
            RoomId = 1,
            DepartmentId = 1,
            CourseId = 1,
            GroupId = 2,
            SemesterId = 3,
            SubjectId = 17,
        };
        var clone = TimetableService.CloneEntry(source, timetableId: 99);
        Assert.Equal(42, clone.TeachingGroupId);
        Assert.Equal(99, clone.TimetableId);

        var cloneSvc = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TimetableCloneService.cs"));
        Assert.DoesNotContain("TimetableSections", cloneSvc);
        Assert.DoesNotContain("new TimetableSection", cloneSvc);
        Assert.Contains("CloneEntry", cloneSvc);
        Assert.Contains("EnsureProposedTeachingGroupCompatibleAsync", cloneSvc);
    }

    [Fact]
    public void S12_ScheduleVersion_preserves_TeachingGroup_via_CloneEntry()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "ScheduleVersionService.cs"));
        Assert.Contains("CloneEntry", src);
        Assert.Contains("EnsureProposedTeachingGroupCompatibleAsync", src);
        Assert.DoesNotContain("TimetableSections", src);
        Assert.DoesNotContain("new TimetableSection", src);
    }

    // --- Scenarios 13–14: auth / admin ---

    [Fact]
    public void S13_Faculty_and_scheduling_RBAC_policies_unchanged()
    {
        var policies = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.API", "Common", "AuthorizationPolicies.cs"));
        Assert.Contains("CanViewSchedulingTimetable", policies);
        Assert.Contains("CanManageSchedulingTimetable", policies);
        Assert.Contains("CanAssignSectionFaculty", policies);
        Assert.Contains("CanViewSchedulingFacultyPreferences", policies);
    }

    [Fact]
    public void S14_Admin_sections_and_TG_assign_routes_remain_authorized()
    {
        var sections = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.API", "Controllers", "SectionsController.cs"));
        Assert.Contains("CanManageSchedulingTimetable", sections);
        Assert.Contains("CanViewSchedulingTimetable", sections);
        Assert.Contains("SetTimetableSectionsAsync", sections);

        var timetables = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.API", "Controllers", "Scheduling", "TimetableControllers.cs"));
        Assert.Contains("entries/{entryId:int}/teaching-group", timetables);
        Assert.Contains("ITeachingGroupApplicationService", timetables);
        Assert.Contains("CanManageSchedulingTimetable", timetables);
    }

    [Fact]
    public void Acceptance_document_exists()
    {
        var path = Path.Combine(FindRepoRoot(), "docs", "AI_SCHED_TG_4A_FINAL_ACCEPTANCE.md");
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("AI-SCHED-TG.4A", text);
        Assert.Contains("SCENARIO", text);
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
