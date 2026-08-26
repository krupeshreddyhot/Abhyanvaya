using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-TG.4A Prompt 6 — Legacy read compatibility (projection reads; no GET mutation).</summary>
public sealed class LegacyTimetableSectionsReadCompatibilityTests
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
            .UseInMemoryDatabase("tg4a-p6-" + Guid.NewGuid().ToString("N"))
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
        DisplayOrder = code[0],
        CreatedDate = DateTime.UtcNow,
    };

    private static async Task<(Timetable Tt, TimetableEntry Entry, TeachingGroup Tg, Section SecA, Section SecB)> SeedBaseAsync(
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
            Code = "TG-P6",
            Name = "Read TG",
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

    private static TimetableSection ProjectionRow(int tenantId, int timetableId, int entryId, int sectionId) => new()
    {
        TenantId = tenantId,
        TimetableId = timetableId,
        TimetableEntryId = entryId,
        SectionId = sectionId,
        CreatedDate = DateTime.UtcNow,
    };

    private static TeachingGroupSection SoTRow(int tenantId, int tgId, int sectionId, bool primary = false) => new()
    {
        TenantId = tenantId,
        TeachingGroupId = tgId,
        SectionId = sectionId,
        IsPrimary = primary,
        CreatedDate = DateTime.UtcNow,
    };

    [Fact]
    public async Task GET_returns_single_section_when_TG_has_one()
    {
        var (db, service, _) = CreateSectionsSut();
        var (tt, entry, tg, secA, _) = await SeedBaseAsync(db);
        db.Set<TeachingGroupSection>().Add(SoTRow(1, tg.Id, secA.Id, primary: true));
        db.Set<TimetableSection>().Add(ProjectionRow(1, tt.Id, entry.Id, secA.Id));
        await db.SaveChangesAsync();

        var dto = await service.GetTimetableSectionsAsync(tt.Id);
        Assert.Single(dto);
        Assert.Equal(secA.Id, dto[0].SectionId);
        Assert.Equal(entry.Id, dto[0].TimetableEntryId);
    }

    [Fact]
    public async Task GET_returns_multiple_sections_when_TG_has_many()
    {
        var (db, service, _) = CreateSectionsSut();
        var (tt, entry, tg, secA, secB) = await SeedBaseAsync(db);
        db.Set<TeachingGroupSection>().AddRange(SoTRow(1, tg.Id, secA.Id, true), SoTRow(1, tg.Id, secB.Id));
        db.Set<TimetableSection>().AddRange(
            ProjectionRow(1, tt.Id, entry.Id, secA.Id),
            ProjectionRow(1, tt.Id, entry.Id, secB.Id));
        await db.SaveChangesAsync();

        var dto = await service.GetTimetableSectionsAsync(tt.Id);
        Assert.Equal(2, dto.Count);
        Assert.Equal(new[] { secA.Id, secB.Id }.OrderBy(x => x), dto.Select(x => x.SectionId).OrderBy(x => x));
    }

    [Fact]
    public async Task GET_returns_empty_when_TG_has_zero_sections()
    {
        var (db, service, _) = CreateSectionsSut();
        var (tt, _, _, _, _) = await SeedBaseAsync(db);
        // Custom TG with no TeachingGroupSection / TimetableSection rows.
        var dto = await service.GetTimetableSectionsAsync(tt.Id);
        Assert.Empty(dto);
    }

    [Fact]
    public async Task GET_legacy_entry_without_TG_still_returns_existing_projection_rows()
    {
        var (db, service, _) = CreateSectionsSut();
        var (tt, entry, _, secA, secB) = await SeedBaseAsync(db, assignTg: false);
        db.Set<TimetableSection>().AddRange(
            ProjectionRow(1, tt.Id, entry.Id, secA.Id),
            ProjectionRow(1, tt.Id, entry.Id, secB.Id));
        await db.SaveChangesAsync();

        var tgCountBefore = await db.Set<TeachingGroup>().IgnoreQueryFilters().CountAsync();
        var sotBefore = await db.Set<TeachingGroupSection>().CountAsync();

        var dto = await service.GetTimetableSectionsAsync(tt.Id);

        Assert.Equal(2, dto.Count);
        Assert.Equal(tgCountBefore, await db.Set<TeachingGroup>().IgnoreQueryFilters().CountAsync());
        Assert.Equal(sotBefore, await db.Set<TeachingGroupSection>().CountAsync());
        Assert.Null((await db.Set<TimetableEntry>().AsNoTracking().SingleAsync(e => e.Id == entry.Id)).TeachingGroupId);
    }

    [Fact]
    public async Task GET_inconsistent_projection_returns_projection_as_is_without_repair()
    {
        var (db, service, _) = CreateSectionsSut();
        var (tt, entry, tg, secA, secB) = await SeedBaseAsync(db);
        // SoT has A+B; projection only has A (drift).
        db.Set<TeachingGroupSection>().AddRange(SoTRow(1, tg.Id, secA.Id, true), SoTRow(1, tg.Id, secB.Id));
        db.Set<TimetableSection>().Add(ProjectionRow(1, tt.Id, entry.Id, secA.Id));
        await db.SaveChangesAsync();

        var dto = await service.GetTimetableSectionsAsync(tt.Id);
        Assert.Single(dto);
        Assert.Equal(secA.Id, dto[0].SectionId);

        Assert.Equal(2, await db.Set<TeachingGroupSection>().CountAsync(x => x.TeachingGroupId == tg.Id));
        Assert.Equal(1, await db.Set<TimetableSection>().CountAsync(x => x.TimetableEntryId == entry.Id));
    }

    [Fact]
    public async Task CombinedSessions_reads_projection_only_and_does_not_mutate()
    {
        var (db, service, _) = CreateSectionsSut();
        var (tt, entry, tg, secA, secB) = await SeedBaseAsync(db);
        db.Set<TeachingGroupSection>().AddRange(SoTRow(1, tg.Id, secA.Id, true), SoTRow(1, tg.Id, secB.Id));
        db.Set<TimetableSection>().AddRange(
            ProjectionRow(1, tt.Id, entry.Id, secA.Id),
            ProjectionRow(1, tt.Id, entry.Id, secB.Id));
        await db.SaveChangesAsync();

        var beforeTs = await db.Set<TimetableSection>().CountAsync();
        var beforeSot = await db.Set<TeachingGroupSection>().CountAsync();

        var combined = await service.GetCombinedSessionsAsync(tt.Id);
        Assert.Equal(2, combined.Count);

        Assert.Equal(beforeTs, await db.Set<TimetableSection>().CountAsync());
        Assert.Equal(beforeSot, await db.Set<TeachingGroupSection>().CountAsync());
    }

    [Fact]
    public async Task Attendance_Timetable_mode_reads_TimetableSection_projection()
    {
        var user = new AmbientCurrentUser { TenantId = 1, StaffId = 42 };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("tg4a-p6-att-" + Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.Set<AcademicYear>().Add(new AcademicYear
        {
            TenantId = 1,
            IsCurrent = true,
            Name = "Y",
            Code = "Y",
            CreatedDate = DateTime.UtcNow,
        });
        var tt = new Timetable
        {
            TenantId = 1,
            AcademicYearId = 1,
            Status = TimetableStatus.Published,
            Name = "Pub",
            CreatedDate = DateTime.UtcNow,
        };
        db.Set<Timetable>().Add(tt);
        await db.SaveChangesAsync();

        var slot = new TimeSlot
        {
            TenantId = 1,
            Name = "P1",
            PeriodNumber = 1,
            SlotKind = SlotKind.Period,
            StartTime = TimeSpan.Zero,
            EndTime = TimeSpan.FromHours(23),
            CreatedDate = DateTime.UtcNow,
        };
        db.Set<TimeSlot>().Add(slot);
        await db.SaveChangesAsync();

        var entry = new TimetableEntry
        {
            TenantId = 1,
            TimetableId = tt.Id,
            StaffId = 42,
            DayOfWeek = (byte)today.DayOfWeek,
            TimeSlotId = slot.Id,
            CourseId = 1,
            GroupId = 2,
            SemesterId = 3,
            SubjectId = 17,
            RoomId = 7,
            DepartmentId = 1,
            SubjectAllocationId = 10,
            TeachingGroupId = null,
            CreatedDate = DateTime.UtcNow,
        };
        db.Set<TimetableEntry>().Add(entry);

        var sec = NewSection(1, "A");
        db.Set<Section>().Add(sec);
        await db.SaveChangesAsync();

        db.Set<TimetableSection>().Add(ProjectionRow(1, tt.Id, entry.Id, sec.Id));
        await db.SaveChangesAsync();

        var resolver = new AttendanceSessionResolver(db, user);
        var result = await resolver.ResolveAsync(42, today);

        Assert.Equal("Timetable", result.Mode);
        Assert.Contains(sec.Id, result.SectionIds);
        Assert.Equal(0, await db.Set<TeachingGroup>().CountAsync());
        Assert.Equal(0, await db.Set<TeachingGroupSection>().CountAsync());
    }

    [Fact]
    public async Task Attendance_Legacy_fallback_preserved_when_no_staff()
    {
        var (db, _, user) = CreateSectionsSut();
        user.StaffId = 0;
        var resolver = new AttendanceSessionResolver(db, user);
        var result = await resolver.ResolveAsync(null, DateOnly.FromDateTime(DateTime.UtcNow));
        Assert.Equal("Legacy", result.Mode);
        Assert.False(result.HasTimetable);
    }

    [Fact]
    public void GetTimetableSections_is_read_only_projection_source()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Academic", "SectionManagementService.cs"));
        var start = src.IndexOf("GetTimetableSectionsAsync", StringComparison.Ordinal);
        var end = src.IndexOf("SetTimetableSectionsAsync", start, StringComparison.Ordinal);
        var method = src.Substring(start, end - start);
        Assert.Contains("TimetableSections.AsNoTracking", method);
        Assert.DoesNotContain("SaveChanges", method);
        Assert.DoesNotContain("ReplaceSectionsAndProjectAsync", method);
        Assert.DoesNotContain("CreateTeachingGroup", method);
        Assert.DoesNotContain("new TeachingGroup", method);
        Assert.DoesNotContain("new TimetableSection", method);
        Assert.DoesNotContain("SubjectAllocation", method);
    }

    [Fact]
    public void AttendanceResolver_read_path_does_not_mutate_or_infer()
    {
        var resolver = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "Conflicts", "AttendanceSessionResolver.cs"));
        Assert.Contains("TimetableSections.AsNoTracking", resolver);
        Assert.Contains("Mode = \"Legacy\"", resolver);
        Assert.DoesNotContain("SaveChanges", resolver);
        Assert.DoesNotContain("TeachingGroupSection", resolver);
        Assert.DoesNotContain("CreateTeachingGroup", resolver);
        Assert.DoesNotContain("ReplaceSectionsAndProjectAsync", resolver);
        Assert.DoesNotContain("SubjectAllocation", resolver);
    }

    [Fact]
    public void Readiness_and_health_still_count_TimetableSection_projection()
    {
        var readiness = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Academic", "SectionReadinessService.cs"));
        var health = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Academic", "SectionHealthService.cs"));
        Assert.Contains("TimetableSections", readiness);
        Assert.Contains("TimetableSections", health);
        Assert.DoesNotContain("ReplaceSectionsAndProjectAsync", readiness);
        Assert.DoesNotContain("ReplaceSectionsAndProjectAsync", health);
        Assert.DoesNotContain("CreateTeachingGroup", readiness);
        Assert.DoesNotContain("CreateTeachingGroup", health);
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
