using System.Reflection;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Abhyanvaya.Infrastructure.Persistence;
using Abhyanvaya.Infrastructure.Persistence.Repositories.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-TG.4 Prompt 3 — Explicit TeachingGroup assignment application boundary.</summary>
public sealed class TeachingGroupApplicationBoundaryTests
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

    private static (ApplicationDbContext Db, TeachingGroupApplicationService Service, AmbientCurrentUser User) CreateSut(int tenantId = 1)
    {
        var user = new AmbientCurrentUser { TenantId = tenantId };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("tg4-p3-" + Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);
        var repo = new TimetableRepository(db);
        var projector = new TimetableSectionProjector(db, user);
        var service = new TeachingGroupApplicationService(repo, db, db, user, projector);
        return (db, service, user);
    }

    private static async Task<(Timetable Timetable, TimetableEntry Entry, TeachingGroup TgA, TeachingGroup TgB)> SeedAsync(
        ApplicationDbContext db,
        int tenantId = 1,
        TeachingGroupStatus tgStatus = TeachingGroupStatus.Active)
    {
        var timetable = new Timetable
        {
            TenantId = tenantId,
            Name = "Draft TT",
            AcademicYearId = 1,
            Status = TimetableStatus.Draft,
            CreatedDate = DateTime.UtcNow,
        };
        db.Set<Timetable>().Add(timetable);
        await db.SaveChangesAsync();

        var entry = new TimetableEntry
        {
            TenantId = tenantId,
            TimetableId = timetable.Id,
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

        var tgA = NewTg(tenantId, 10, "TG-A", tgStatus, code: "TGA");
        var tgB = NewTg(tenantId, 10, "TG-B", TeachingGroupStatus.Active, code: "TGB");
        db.Set<TeachingGroup>().AddRange(tgA, tgB);
        await db.SaveChangesAsync();
        return (timetable, entry, tgA, tgB);
    }

    private static TeachingGroup NewTg(
        int tenantId,
        int subjectAllocationId,
        string name,
        TeachingGroupStatus status,
        string code,
        int courseId = 1,
        int groupId = 2,
        int semesterId = 3,
        int subjectId = 17)
        => new()
        {
            TenantId = tenantId,
            AcademicYearId = 1,
            CourseId = courseId,
            GroupId = groupId,
            SemesterId = semesterId,
            SubjectId = subjectId,
            SubjectAllocationId = subjectAllocationId,
            Type = TeachingGroupType.SectionDerived,
            MembershipSource = TeachingGroupMembershipSource.Section,
            Status = status,
            ActivityKind = TeachingGroupActivityKind.Lecture,
            Code = code,
            Name = name,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedDate = DateTime.UtcNow,
        };

    [Fact]
    public async Task Explicit_valid_assignment_succeeds()
    {
        var (db, service, _) = CreateSut();
        var (_, entry, tgA, _) = await SeedAsync(db);

        var result = await service.AssignToTimetableEntryAsync(entry.Id, tgA.Id);
        Assert.Equal(tgA.Id, result.TeachingGroupId);

        var reloaded = await db.Set<TimetableEntry>().AsNoTracking().SingleAsync(e => e.Id == entry.Id);
        Assert.Equal(tgA.Id, reloaded.TeachingGroupId);
    }

    [Fact]
    public async Task Null_TeachingGroupId_remains_supported_before_assignment()
    {
        var (db, _, _) = CreateSut();
        var (_, entry, _, _) = await SeedAsync(db);
        Assert.Null(entry.TeachingGroupId);
        Assert.Null((await db.Set<TimetableEntry>().AsNoTracking().SingleAsync(e => e.Id == entry.Id)).TeachingGroupId);
    }

    [Fact]
    public async Task TeachingGroup_not_found_is_rejected()
    {
        var (db, service, _) = CreateSut();
        var (_, entry, _, _) = await SeedAsync(db);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AssignToTimetableEntryAsync(entry.Id, 99999));
    }

    [Fact]
    public async Task Cross_tenant_TeachingGroup_is_rejected_as_not_found()
    {
        var (db, service, _) = CreateSut(tenantId: 1);
        var (_, entry, _, _) = await SeedAsync(db, tenantId: 1);

        var foreign = NewTg(tenantId: 2, subjectAllocationId: 10, name: "OtherTenant", status: TeachingGroupStatus.Active, code: "X");
        db.Set<TeachingGroup>().Add(foreign);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AssignToTimetableEntryAsync(entry.Id, foreign.Id));
        var reloaded = await db.Set<TimetableEntry>().AsNoTracking().SingleAsync(e => e.Id == entry.Id);
        Assert.Null(reloaded.TeachingGroupId);
    }

    [Fact]
    public async Task Wrong_SubjectAllocation_or_scope_is_rejected()
    {
        var (db, service, _) = CreateSut();
        var (_, entry, _, _) = await SeedAsync(db);
        var wrong = NewTg(1, subjectAllocationId: 99, name: "WrongSA", status: TeachingGroupStatus.Active, code: "W");
        db.Set<TeachingGroup>().Add(wrong);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() => service.AssignToTimetableEntryAsync(entry.Id, wrong.Id));
    }

    [Fact]
    public async Task Multiple_TGs_same_allocation_selects_exact_supplied_TG()
    {
        var (db, service, _) = CreateSut();
        var (_, entry, tgA, tgB) = await SeedAsync(db);

        var first = await service.AssignToTimetableEntryAsync(entry.Id, tgB.Id);
        Assert.Equal(tgB.Id, first.TeachingGroupId);
        Assert.NotEqual(tgA.Id, first.TeachingGroupId);

        var second = await service.AssignToTimetableEntryAsync(entry.Id, tgA.Id);
        Assert.Equal(tgA.Id, second.TeachingGroupId);
    }

    [Fact]
    public void No_implicit_SubjectAllocation_resolver_methods_exist()
    {
        var type = typeof(TeachingGroupApplicationService);
        Assert.Null(type.GetMethod("FindTeachingGroup", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
        Assert.Null(type.GetMethod("ResolveTeachingGroupFromSubjectAllocation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
        Assert.Null(type.GetMethod("CreateTeachingGroupIfMissing", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(), "Abhyanvaya.Application", "Scheduling", "TeachingGroupApplicationService.cs"));
        Assert.DoesNotContain(".IgnoreQueryFilters", src);
        Assert.DoesNotContain("CreateTeachingGroup", src);
        Assert.DoesNotContain("FirstOrDefaultAsync(x => x.SubjectAllocationId", src);
    }

    [Fact]
    public async Task Missing_TG_does_not_create_TeachingGroup()
    {
        var (db, service, _) = CreateSut();
        var (_, entry, _, _) = await SeedAsync(db);
        var before = await db.Set<TeachingGroup>().IgnoreQueryFilters().CountAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AssignToTimetableEntryAsync(entry.Id, 424242));
        var after = await db.Set<TeachingGroup>().IgnoreQueryFilters().CountAsync();
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Archived_TeachingGroup_is_rejected()
    {
        var (db, service, _) = CreateSut();
        var (_, entry, _, _) = await SeedAsync(db, tgStatus: TeachingGroupStatus.Archived);
        var archived = await db.Set<TeachingGroup>().SingleAsync(x => x.Code == "TGA");
        await Assert.ThrowsAsync<DomainException>(() => service.AssignToTimetableEntryAsync(entry.Id, archived.Id));
    }

    [Fact]
    public async Task Unrelated_entry_update_request_shape_does_not_include_TeachingGroupId()
    {
        // Guards accidental nulling via UpdateTimetableEntryRequest mapping.
        Assert.Null(typeof(Abhyanvaya.Application.DTOs.Scheduling.UpdateTimetableEntryRequest).GetProperty("TeachingGroupId"));
        Assert.Null(typeof(Abhyanvaya.Application.DTOs.Scheduling.CreateTimetableEntryRequest).GetProperty("TeachingGroupId"));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Explicit_reassignment_TG_A_to_TG_B_succeeds_on_Draft()
    {
        var (db, service, _) = CreateSut();
        var (_, entry, tgA, tgB) = await SeedAsync(db);
        await service.AssignToTimetableEntryAsync(entry.Id, tgA.Id);
        var result = await service.AssignToTimetableEntryAsync(entry.Id, tgB.Id);
        Assert.Equal(tgB.Id, result.TeachingGroupId);
    }

    [Fact]
    public async Task Published_timetable_assignment_is_rejected_by_lifecycle()
    {
        var (db, service, _) = CreateSut();
        var (timetable, entry, tgA, _) = await SeedAsync(db);
        timetable.Status = TimetableStatus.Published;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() => service.AssignToTimetableEntryAsync(entry.Id, tgA.Id));
    }

    [Fact]
    public async Task Locked_timetable_assignment_is_rejected_by_lifecycle()
    {
        var (db, service, _) = CreateSut();
        var (timetable, entry, tgA, _) = await SeedAsync(db);
        timetable.Status = TimetableStatus.Locked;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() => service.AssignToTimetableEntryAsync(entry.Id, tgA.Id));
    }

    [Fact]
    public async Task Assignment_does_not_mutate_membership_or_TeachingGroupSection()
    {
        var (db, service, _) = CreateSut();
        var (_, entry, tgA, _) = await SeedAsync(db);
        db.Set<TeachingGroupSection>().Add(new TeachingGroupSection
        {
            TenantId = 1,
            TeachingGroupId = tgA.Id,
            SectionId = 5,
            IsPrimary = true,
            CreatedDate = DateTime.UtcNow,
        });
        db.Set<TeachingGroupMembership>().Add(new TeachingGroupMembership
        {
            TenantId = 1,
            TeachingGroupId = tgA.Id,
            StudentId = 1,
            Inclusion = TeachingGroupMembershipInclusion.Include,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            IsCurrent = true,
            CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var sectionsBefore = await db.Set<TeachingGroupSection>().CountAsync();
        var membersBefore = await db.Set<TeachingGroupMembership>().CountAsync();
        await service.AssignToTimetableEntryAsync(entry.Id, tgA.Id);
        Assert.Equal(sectionsBefore, await db.Set<TeachingGroupSection>().CountAsync());
        Assert.Equal(membersBefore, await db.Set<TeachingGroupMembership>().CountAsync());
    }

    [Fact]
    public async Task Explicit_clear_sets_TeachingGroupId_null()
    {
        var (db, service, _) = CreateSut();
        var (_, entry, tgA, _) = await SeedAsync(db);
        await service.AssignToTimetableEntryAsync(entry.Id, tgA.Id);
        var cleared = await service.ClearFromTimetableEntryAsync(entry.Id);
        Assert.Null(cleared.TeachingGroupId);
    }

    [Fact]
    public void Assign_API_requires_Manage_timetable_authorization()
    {
        // Application unit tests must not reference the API project; guard via source contract.
        var controller = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.API", "Controllers", "Scheduling", "TimetableControllers.cs"));
        Assert.Contains("entries/{entryId:int}/teaching-group", controller);
        Assert.Contains("AssignTeachingGroup", controller);
        Assert.Contains("ClearTeachingGroup", controller);
        Assert.Contains("CanManageSchedulingTimetable", controller);
        Assert.DoesNotContain(".IgnoreQueryFilters", controller);
    }

    [Fact]
    public void AttendanceSessionResolver_and_SetTimetableSections_untouched_by_this_service()
    {
        var root = FindRepoRoot();
        var serviceSrc = File.ReadAllText(Path.Combine(root, "Abhyanvaya.Application", "Scheduling", "TeachingGroupApplicationService.cs"));
        // Prompt 21 orchestrates ITimetableSectionProjector but must not construct TimetableSection or call legacy SetTimetableSections.
        Assert.DoesNotContain("new TimetableSection", serviceSrc);
        Assert.DoesNotContain("SetTimetableSections", serviceSrc);
        Assert.DoesNotContain("AttendanceSessionResolver", serviceSrc);
        Assert.Contains("ITimetableSectionProjector", serviceSrc);

        var resolver = File.ReadAllText(Path.Combine(root, "Abhyanvaya.Application", "Scheduling", "Conflicts", "AttendanceSessionResolver.cs"));
        Assert.DoesNotContain("TeachingGroup", resolver);
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
