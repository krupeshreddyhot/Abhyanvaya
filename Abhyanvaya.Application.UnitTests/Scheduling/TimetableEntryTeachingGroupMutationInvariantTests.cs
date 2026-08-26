using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Abhyanvaya.Infrastructure.Persistence;
using Abhyanvaya.Infrastructure.Persistence.Repositories.Scheduling;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-TG.4 Prompt 4 — TimetableEntry mutation invariants &amp; TeachingGroup compatibility.</summary>
public sealed class TimetableEntryTeachingGroupMutationInvariantTests
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

    private static Mock<IValidator<T>> PassValidator<T>()
    {
        var mock = new Mock<IValidator<T>>();
        mock.Setup(v => v.ValidateAsync(It.IsAny<T>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        return mock;
    }

    private static (ApplicationDbContext Db, TimetableService Service, AmbientCurrentUser User) CreateSut(int tenantId = 1)
    {
        var user = new AmbientCurrentUser { TenantId = tenantId };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("tg4-p4-" + Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);
        var service = new TimetableService(
            new TimetableRepository(db),
            new SubjectAllocationRepository(db),
            new TimeSlotRepository(db),
            db,
            db,
            user,
            PassValidator<CreateTimetableRequest>().Object,
            PassValidator<UpdateTimetableRequest>().Object,
            PassValidator<CreateTimetableEntryRequest>().Object,
            PassValidator<UpdateTimetableEntryRequest>().Object,
            PassValidator<BulkPasteEntriesRequest>().Object,
            PassValidator<MoveTimetableEntryRequest>().Object,
            PassValidator<CopyTimetableEntryRequest>().Object);
        return (db, service, user);
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
        int subjectId = 17) => new()
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

    private static SubjectAllocation NewSa(
        int tenantId,
        int id,
        int courseId = 1,
        int groupId = 2,
        int semesterId = 3,
        int subjectId = 17,
        int preferredRoomId = 1) => new()
    {
        Id = id,
        TenantId = tenantId,
        AcademicYearId = 1,
        SubjectId = subjectId,
        StaffId = 1,
        CourseId = courseId,
        GroupId = groupId,
        SemesterId = semesterId,
        DepartmentId = 1,
        PreferredRoomId = preferredRoomId,
        WeeklyHours = 3,
        EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
        CreatedDate = DateTime.UtcNow,
    };

    private static async Task<(Timetable Timetable, TimetableEntry Entry, TeachingGroup Tg, SubjectAllocation Sa10, SubjectAllocation Sa20)> SeedAsync(
        ApplicationDbContext db,
        int tenantId = 1,
        bool assignTg = true)
    {
        db.Set<Room>().Add(new Room
        {
            Id = 1,
            TenantId = tenantId,
            FloorId = 1,
            Name = "R1",
            Code = "R1",
            Capacity = 40,
            CreatedDate = DateTime.UtcNow,
        });
        db.Set<TimeSlot>().Add(new TimeSlot
        {
            Id = 1,
            TenantId = tenantId,
            TimeSlotSetId = 1,
            Name = "P1",
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(10),
            DurationMinutes = 60,
            CreatedDate = DateTime.UtcNow,
        });

        var sa10 = NewSa(tenantId, 10, subjectId: 17);
        var sa20 = NewSa(tenantId, 20, courseId: 9, groupId: 9, semesterId: 9, subjectId: 99);
        db.Set<SubjectAllocation>().AddRange(sa10, sa20);

        // AI-SCHED-CATALOG/TIMETABLE P1-3 Prompt 4 — Course.DepartmentId is Catalog SSOT for entry denorm.
        db.Set<Abhyanvaya.Domain.Entities.Course>().AddRange(
            new Abhyanvaya.Domain.Entities.Course
            {
                Id = 1,
                TenantId = tenantId,
                Code = "C1",
                Name = "Course 1",
                DepartmentId = 1,
                CreatedDate = DateTime.UtcNow,
            },
            new Abhyanvaya.Domain.Entities.Course
            {
                Id = 9,
                TenantId = tenantId,
                Code = "C9",
                Name = "Course 9",
                DepartmentId = 1,
                CreatedDate = DateTime.UtcNow,
            });

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

        var tg = NewTg(tenantId, 10, "TG-A", TeachingGroupStatus.Active, "TGA");
        db.Set<TeachingGroup>().Add(tg);
        await db.SaveChangesAsync();

        var entry = new TimetableEntry
        {
            TenantId = tenantId,
            TimetableId = timetable.Id,
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
        return (timetable, entry, tg, sa10, sa20);
    }

    [Fact]
    public void Compatible_TeachingGroup_passes_domain_rule()
    {
        var tg = NewTg(1, 10, "TG", TeachingGroupStatus.Active, "A");
        tg.Id = 5;
        var entry = new TimetableEntry
        {
            TenantId = 1,
            SubjectAllocationId = 10,
            TeachingGroupId = 5,
            CourseId = 1,
            GroupId = 2,
            SemesterId = 3,
            SubjectId = 17,
        };
        TeachingGroupRules.EnsureCompatibleWithTimetableEntry(tg, entry);
    }

    [Fact]
    public void Incompatible_TeachingGroup_fails_domain_rule_with_actionable_message()
    {
        var tg = NewTg(1, 10, "TG", TeachingGroupStatus.Active, "A");
        tg.Id = 5;
        var entry = new TimetableEntry
        {
            TenantId = 1,
            SubjectAllocationId = 99,
            TeachingGroupId = 5,
            CourseId = 1,
            GroupId = 2,
            SemesterId = 3,
            SubjectId = 17,
        };
        var ex = Assert.Throws<DomainException>(() => TeachingGroupRules.EnsureCompatibleWithTimetableEntry(tg, entry));
        Assert.Equal(TeachingGroupRules.TimetableEntryTeachingGroupIncompatibleMessage, ex.Message);
    }

    [Fact]
    public void Cross_tenant_TeachingGroup_fails_domain_rule_without_leaking_tenant_ids()
    {
        var tg = NewTg(2, 10, "TG", TeachingGroupStatus.Active, "A");
        tg.Id = 5;
        var entry = new TimetableEntry
        {
            TenantId = 1,
            SubjectAllocationId = 10,
            TeachingGroupId = 5,
            CourseId = 1,
            GroupId = 2,
            SemesterId = 3,
            SubjectId = 17,
        };
        var ex = Assert.Throws<DomainException>(() => TeachingGroupRules.EnsureCompatibleWithTimetableEntry(tg, entry));
        Assert.Equal(TeachingGroupRules.TimetableEntryTeachingGroupIncompatibleMessage, ex.Message);
        Assert.DoesNotContain("Tenant", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("2", ex.Message);
    }

    [Fact]
    public async Task Update_unchanged_allocation_with_compatible_TG_succeeds()
    {
        var (db, service, _) = CreateSut();
        var (_, entry, tg, _, _) = await SeedAsync(db);

        var result = await service.UpdateEntryAsync(entry.Id, new UpdateTimetableEntryRequest
        {
            Id = entry.Id,
            DayOfWeek = 2,
            TimeSlotId = 1,
            SubjectAllocationId = 10,
            RoomId = 1,
            Remarks = "kept",
        });

        Assert.Equal(tg.Id, result.TeachingGroupId);
        Assert.Equal(2, result.DayOfWeek);
    }

    [Fact]
    public async Task Update_allocation_changed_incompatible_TG_is_rejected_and_not_persisted()
    {
        var (db, service, _) = CreateSut();
        var (_, entry, tg, _, _) = await SeedAsync(db);

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.UpdateEntryAsync(entry.Id, new UpdateTimetableEntryRequest
        {
            Id = entry.Id,
            DayOfWeek = 1,
            TimeSlotId = 1,
            SubjectAllocationId = 20,
            RoomId = 1,
        }));
        Assert.Equal(TeachingGroupRules.TimetableEntryTeachingGroupIncompatibleMessage, ex.Message);

        var reloaded = await db.Set<TimetableEntry>().AsNoTracking().SingleAsync(e => e.Id == entry.Id);
        Assert.Equal(10, reloaded.SubjectAllocationId);
        Assert.Equal(tg.Id, reloaded.TeachingGroupId);
    }

    [Fact]
    public async Task Update_allocation_changed_to_match_existing_TG_succeeds()
    {
        // Seeded incompatible persisted state (legacy/corrupt) can be repaired by aligning SA to TG.
        var (db, service, _) = CreateSut();
        var (_, entry, tg, _, _) = await SeedAsync(db, assignTg: true);
        entry.SubjectAllocationId = 20;
        entry.CourseId = 9;
        entry.GroupId = 9;
        entry.SemesterId = 9;
        entry.SubjectId = 99;
        await db.SaveChangesAsync();

        // TG remains SA=10; update SA back to 10 → compatible proposed state.
        var result = await service.UpdateEntryAsync(entry.Id, new UpdateTimetableEntryRequest
        {
            Id = entry.Id,
            DayOfWeek = 1,
            TimeSlotId = 1,
            SubjectAllocationId = 10,
            RoomId = 1,
        });
        Assert.Equal(tg.Id, result.TeachingGroupId);
        Assert.Equal(10, result.SubjectAllocationId);
    }

    [Fact]
    public async Task Update_without_TG_allows_SubjectAllocation_change()
    {
        var (db, service, _) = CreateSut();
        var (_, entry, _, _, _) = await SeedAsync(db, assignTg: false);

        var result = await service.UpdateEntryAsync(entry.Id, new UpdateTimetableEntryRequest
        {
            Id = entry.Id,
            DayOfWeek = 1,
            TimeSlotId = 1,
            SubjectAllocationId = 20,
            RoomId = 1,
        });
        Assert.Null(result.TeachingGroupId);
        Assert.Equal(20, result.SubjectAllocationId);
        Assert.Equal(99, result.SubjectId);
    }

    [Fact]
    public async Task Create_with_null_TeachingGroupId_succeeds()
    {
        var (db, service, _) = CreateSut();
        var (timetable, _, _, _, _) = await SeedAsync(db, assignTg: false);

        var result = await service.CreateEntryAsync(timetable.Id, new CreateTimetableEntryRequest
        {
            DayOfWeek = 3,
            TimeSlotId = 1,
            SubjectAllocationId = 10,
            RoomId = 1,
        });
        Assert.Null(result.TeachingGroupId);
        Assert.Equal(10, result.SubjectAllocationId);
    }

    [Fact]
    public async Task Clear_TeachingGroup_then_change_allocation_succeeds()
    {
        var (db, _, user) = CreateSut();
        var (_, entry, tg, _, _) = await SeedAsync(db);
        var tgService = new TeachingGroupApplicationService(
            new TimetableRepository(db), db, db, user, new TimetableSectionProjector(db, user));
        await tgService.ClearFromTimetableEntryAsync(entry.Id);

        var (_, service, _) = CreateSut();
        // Re-attach to same in-memory db via fresh service sharing db is hard; use same db instance.
        var service2 = new TimetableService(
            new TimetableRepository(db),
            new SubjectAllocationRepository(db),
            new TimeSlotRepository(db),
            db,
            db,
            user,
            PassValidator<CreateTimetableRequest>().Object,
            PassValidator<UpdateTimetableRequest>().Object,
            PassValidator<CreateTimetableEntryRequest>().Object,
            PassValidator<UpdateTimetableEntryRequest>().Object,
            PassValidator<BulkPasteEntriesRequest>().Object,
            PassValidator<MoveTimetableEntryRequest>().Object,
            PassValidator<CopyTimetableEntryRequest>().Object);

        var result = await service2.UpdateEntryAsync(entry.Id, new UpdateTimetableEntryRequest
        {
            Id = entry.Id,
            DayOfWeek = 1,
            TimeSlotId = 1,
            SubjectAllocationId = 20,
            RoomId = 1,
        });
        Assert.Null(result.TeachingGroupId);
        Assert.Equal(20, result.SubjectAllocationId);
        Assert.NotEqual(tg.Id, result.TeachingGroupId);
    }

    [Fact]
    public async Task Clone_same_scope_preserves_compatible_TeachingGroup()
    {
        var (db, _, _) = CreateSut();
        var (_, entry, tg, _, _) = await SeedAsync(db);
        var clone = TimetableService.CloneEntry(entry, entry.TimetableId);
        Assert.Equal(tg.Id, clone.TeachingGroupId);
        Assert.Equal(entry.TenantId, clone.TenantId);
        await TimetableService.EnsureProposedTeachingGroupCompatibleAsync(db, clone);
    }

    [Fact]
    public async Task Clone_changed_scope_with_incompatible_TG_is_rejected()
    {
        var (db, _, _) = CreateSut();
        var (_, entry, _, _, _) = await SeedAsync(db);
        var clone = TimetableService.CloneEntry(entry, entry.TimetableId);
        clone.SubjectAllocationId = 20;
        clone.CourseId = 9;
        clone.GroupId = 9;
        clone.SemesterId = 9;
        clone.SubjectId = 99;

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            TimetableService.EnsureProposedTeachingGroupCompatibleAsync(db, clone));
        Assert.Equal(TeachingGroupRules.TimetableEntryTeachingGroupIncompatibleMessage, ex.Message);
    }

    [Fact]
    public async Task Clone_without_TG_passes()
    {
        var (db, _, _) = CreateSut();
        var (_, entry, _, _, _) = await SeedAsync(db, assignTg: false);
        var clone = TimetableService.CloneEntry(entry, entry.TimetableId);
        Assert.Null(clone.TeachingGroupId);
        await TimetableService.EnsureProposedTeachingGroupCompatibleAsync(db, clone);
    }

    [Fact]
    public async Task Copy_entry_preserves_compatible_TG()
    {
        var (db, service, _) = CreateSut();
        var (_, entry, tg, _, _) = await SeedAsync(db);

        var copy = await service.CopyEntryAsync(entry.Id, new CopyTimetableEntryRequest
        {
            TargetDayOfWeek = 4,
            TargetTimeSlotId = 1,
            RoomId = 1,
        });
        Assert.Equal(tg.Id, copy.TeachingGroupId);
        Assert.Equal(4, copy.DayOfWeek);
    }

    [Fact]
    public async Task Lifecycle_locked_timetable_still_blocks_update_before_invariant()
    {
        var (db, service, _) = CreateSut();
        var (timetable, entry, _, _, _) = await SeedAsync(db);
        timetable.Status = TimetableStatus.Locked;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() => service.UpdateEntryAsync(entry.Id, new UpdateTimetableEntryRequest
        {
            Id = entry.Id,
            DayOfWeek = 1,
            TimeSlotId = 1,
            SubjectAllocationId = 20,
            RoomId = 1,
        }));
    }

    [Fact]
    public async Task EnsureProposed_does_not_create_or_clear_TeachingGroup()
    {
        var (db, _, _) = CreateSut();
        var (_, entry, tg, _, _) = await SeedAsync(db);
        entry.SubjectAllocationId = 20;
        entry.SubjectId = 99;
        var before = await db.Set<TeachingGroup>().IgnoreQueryFilters().CountAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            TimetableService.EnsureProposedTeachingGroupCompatibleAsync(db, entry));

        Assert.Equal(before, await db.Set<TeachingGroup>().IgnoreQueryFilters().CountAsync());
        Assert.Equal(tg.Id, entry.TeachingGroupId); // not cleared
    }

    [Fact]
    public void Mutation_paths_invoke_proposed_state_compatibility_check()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(), "Abhyanvaya.Application", "Scheduling", "TimetableService.cs"));
        Assert.Contains("EnsureProposedTeachingGroupCompatibleAsync", src);
        Assert.DoesNotContain("TeachingGroupId = null;", src);
        Assert.DoesNotContain("FindFirstTeachingGroup", src);
        Assert.DoesNotContain("CreateTeachingGroupIfMissing", src);

        var cloneSrc = File.ReadAllText(Path.Combine(FindRepoRoot(), "Abhyanvaya.Application", "Scheduling", "TimetableCloneService.cs"));
        Assert.Contains("EnsureProposedTeachingGroupCompatibleAsync", cloneSrc);

        var versionSrc = File.ReadAllText(Path.Combine(FindRepoRoot(), "Abhyanvaya.Application", "Scheduling", "ScheduleVersionService.cs"));
        Assert.Contains("EnsureProposedTeachingGroupCompatibleAsync", versionSrc);
    }

    [Fact]
    public void UpdateEntry_API_still_requires_manage_policy_and_maps_DomainException()
    {
        var controller = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.API", "Controllers", "Scheduling", "TimetableControllers.cs"));
        Assert.Contains("UpdateEntry", controller);
        Assert.Contains("CanManageSchedulingTimetable", controller);
        Assert.Contains("catch (DomainException ex)", controller);
        Assert.DoesNotContain(".IgnoreQueryFilters", controller);
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
