using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Infrastructure.Persistence;
using Abhyanvaya.Infrastructure.Persistence.Repositories.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-TG.6 Prompt 4 / Prompt 2A — Compatible Teaching Group query application tests.</summary>
public sealed class CompatibleTeachingGroupQueryServiceTests
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

    private static (ApplicationDbContext Db, CompatibleTeachingGroupQueryService Service, AmbientCurrentUser User) CreateSut(
        int tenantId = 1)
    {
        var user = new AmbientCurrentUser { TenantId = tenantId };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("tg6-p4-p2a-" + Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);
        var repo = new TimetableRepository(db);
        var resolver = new TeachingGroupMembershipResolver(db);
        var service = new CompatibleTeachingGroupQueryService(repo, db, user, resolver);
        return (db, service, user);
    }

    private static TeachingGroup NewTg(
        int tenantId,
        int subjectAllocationId,
        string name,
        TeachingGroupStatus status,
        int courseId = 1,
        int groupId = 2,
        int semesterId = 3,
        int subjectId = 17,
        string? code = null) =>
        new()
        {
            TenantId = tenantId,
            AcademicYearId = 1,
            CourseId = courseId,
            GroupId = groupId,
            SemesterId = semesterId,
            SubjectId = subjectId,
            SubjectAllocationId = subjectAllocationId,
            Type = TeachingGroupType.Custom,
            MembershipSource = TeachingGroupMembershipSource.ExplicitStudents,
            Status = status,
            ActivityKind = TeachingGroupActivityKind.Lecture,
            Code = code,
            Name = name,
            ExpectedStudentCount = 30,
            MaxTeachingCapacity = 40,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedDate = DateTime.UtcNow,
        };

    private static async Task<(Timetable Timetable, TimetableEntry Entry)> SeedEntryAsync(
        ApplicationDbContext db,
        int tenantId = 1,
        int subjectAllocationId = 10,
        int? teachingGroupId = null,
        int courseId = 1,
        int groupId = 2,
        int semesterId = 3,
        int subjectId = 17)
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
            SubjectAllocationId = subjectAllocationId,
            TeachingGroupId = teachingGroupId,
            StaffId = 1,
            RoomId = 99,
            DepartmentId = 1,
            CourseId = courseId,
            GroupId = groupId,
            SemesterId = semesterId,
            SubjectId = subjectId,
            CreatedDate = DateTime.UtcNow,
        };
        db.Set<TimetableEntry>().Add(entry);
        await db.SaveChangesAsync();
        return (timetable, entry);
    }

    [Fact]
    public async Task Entry_with_no_TG_returns_compatible_TGs()
    {
        var (db, service, _) = CreateSut();
        var (_, entry) = await SeedEntryAsync(db);
        db.Set<TeachingGroup>().Add(NewTg(1, 10, "TG-A", TeachingGroupStatus.Active, code: "A"));
        db.Set<TeachingGroup>().Add(NewTg(1, 10, "TG-B", TeachingGroupStatus.Draft, code: "B"));
        await db.SaveChangesAsync();

        var result = await service.GetCompatibleTeachingGroupsForTimetableEntryAsync(entry.Id);
        Assert.Equal(2, result.Count);
        Assert.All(result, x => Assert.False(x.IsAssignedToEntry));
    }

    [Fact]
    public async Task Entry_with_assigned_TG_marks_IsAssignedToEntry()
    {
        var (db, service, _) = CreateSut();
        var tg = NewTg(1, 10, "TG-A", TeachingGroupStatus.Active, code: "A");
        db.Set<TeachingGroup>().Add(tg);
        await db.SaveChangesAsync();
        var (_, entry) = await SeedEntryAsync(db, teachingGroupId: tg.Id);

        var result = await service.GetCompatibleTeachingGroupsForTimetableEntryAsync(entry.Id);
        Assert.Contains(result, x => x.Id == tg.Id && x.IsAssignedToEntry);
        Assert.DoesNotContain(result, x => x.Id != tg.Id && x.IsAssignedToEntry);
    }

    [Fact]
    public async Task Zero_compatible_returns_empty_list()
    {
        var (db, service, _) = CreateSut();
        var (_, entry) = await SeedEntryAsync(db);
        var result = await service.GetCompatibleTeachingGroupsForTimetableEntryAsync(entry.Id);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Multiple_TGs_same_SA_returned_other_SA_excluded()
    {
        var (db, service, _) = CreateSut();
        var (_, entry) = await SeedEntryAsync(db, subjectAllocationId: 10);
        db.Set<TeachingGroup>().AddRange(
            NewTg(1, 10, "Same-SA-1", TeachingGroupStatus.Active),
            NewTg(1, 10, "Same-SA-2", TeachingGroupStatus.Active),
            NewTg(1, 99, "Other-SA", TeachingGroupStatus.Active));
        await db.SaveChangesAsync();

        var result = await service.GetCompatibleTeachingGroupsForTimetableEntryAsync(entry.Id);
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, x => x.Name == "Other-SA");
    }

    [Fact]
    public async Task Wrong_Course_Group_Semester_Subject_excluded()
    {
        var (db, service, _) = CreateSut();
        var (_, entry) = await SeedEntryAsync(db);
        db.Set<TeachingGroup>().AddRange(
            NewTg(1, 10, "OK", TeachingGroupStatus.Active),
            NewTg(1, 10, "BadCourse", TeachingGroupStatus.Active, courseId: 9),
            NewTg(1, 10, "BadGroup", TeachingGroupStatus.Active, groupId: 9),
            NewTg(1, 10, "BadSem", TeachingGroupStatus.Active, semesterId: 9),
            NewTg(1, 10, "BadSubject", TeachingGroupStatus.Active, subjectId: 9));
        await db.SaveChangesAsync();

        var result = await service.GetCompatibleTeachingGroupsForTimetableEntryAsync(entry.Id);
        Assert.Single(result);
        Assert.Equal("OK", result[0].Name);
    }

    [Fact]
    public async Task Cross_tenant_TG_excluded_and_missing_entry_is_not_found()
    {
        var (db, service, user) = CreateSut(tenantId: 1);
        var (_, entry) = await SeedEntryAsync(db, tenantId: 1);
        db.Set<TeachingGroup>().Add(NewTg(1, 10, "Tenant1", TeachingGroupStatus.Active));
        db.Set<TeachingGroup>().Add(NewTg(2, 10, "Tenant2", TeachingGroupStatus.Active));
        await db.SaveChangesAsync();

        var result = await service.GetCompatibleTeachingGroupsForTimetableEntryAsync(entry.Id);
        Assert.Single(result);
        Assert.Equal("Tenant1", result[0].Name);

        user.TenantId = 2;
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.GetCompatibleTeachingGroupsForTimetableEntryAsync(entry.Id));
    }

    [Fact]
    public async Task Archived_TG_excluded_unless_currently_assigned()
    {
        var (db, service, _) = CreateSut();
        var archived = NewTg(1, 10, "Archived", TeachingGroupStatus.Archived);
        var active = NewTg(1, 10, "Active", TeachingGroupStatus.Active);
        db.Set<TeachingGroup>().AddRange(archived, active);
        await db.SaveChangesAsync();
        var (_, entry) = await SeedEntryAsync(db);

        var withoutAssign = await service.GetCompatibleTeachingGroupsForTimetableEntryAsync(entry.Id);
        Assert.Single(withoutAssign);
        Assert.Equal("Active", withoutAssign[0].Name);

        entry.TeachingGroupId = archived.Id;
        await db.SaveChangesAsync();
        var withAssign = await service.GetCompatibleTeachingGroupsForTimetableEntryAsync(entry.Id);
        Assert.Equal(2, withAssign.Count);
        var assigned = Assert.Single(withAssign, x => x.Id == archived.Id);
        Assert.True(assigned.IsAssignedToEntry);
        Assert.Equal(TeachingGroupStatus.Archived, assigned.Status);

        var reloaded = await db.Set<TimetableEntry>().AsNoTracking().SingleAsync(e => e.Id == entry.Id);
        Assert.Equal(archived.Id, reloaded.TeachingGroupId);
    }

    [Fact]
    public async Task Locked_and_Draft_are_selectable()
    {
        var (db, service, _) = CreateSut();
        var (_, entry) = await SeedEntryAsync(db);
        db.Set<TeachingGroup>().AddRange(
            NewTg(1, 10, "Draft", TeachingGroupStatus.Draft),
            NewTg(1, 10, "Locked", TeachingGroupStatus.Locked));
        await db.SaveChangesAsync();

        var result = await service.GetCompatibleTeachingGroupsForTimetableEntryAsync(entry.Id);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task Capacity_fields_returned_and_room_capacity_ignored()
    {
        var (db, service, _) = CreateSut();
        var (_, entry) = await SeedEntryAsync(db);
        entry.RoomId = 1; // room capacity must never filter
        var tg = NewTg(1, 10, "Cap", TeachingGroupStatus.Active);
        tg.ExpectedStudentCount = 30;
        tg.MaxTeachingCapacity = 40;
        db.Set<TeachingGroup>().Add(tg);
        db.Set<Student>().Add(new Student
        {
            TenantId = 1,
            StudentNumber = "S501",
            Name = "Test",
            CourseId = 1,
            GroupId = 2,
            SemesterId = 3,
            GenderId = 1,
            MediumId = 1,
            FirstLanguageId = 1,
            LanguageId = 1,
            CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var studentId = await db.Set<Student>().Select(s => s.Id).SingleAsync();
        db.Set<TeachingGroupMembership>().Add(new TeachingGroupMembership
        {
            TenantId = 1,
            TeachingGroupId = tg.Id,
            StudentId = studentId,
            Inclusion = TeachingGroupMembershipInclusion.Include,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            IsCurrent = true,
            CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await service.GetCompatibleTeachingGroupsForTimetableEntryAsync(entry.Id);
        var option = Assert.Single(result);
        Assert.Equal(30, option.ExpectedStudentCount);
        Assert.Equal(40, option.MaxTeachingCapacity);
        Assert.Equal(1, option.ResolvedStudentCount);
    }

    [Fact]
    public async Task Missing_entry_throws_not_found()
    {
        var (_, service, _) = CreateSut();
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.GetCompatibleTeachingGroupsForTimetableEntryAsync(99999));
    }

    [Fact]
    public async Task Query_does_not_mutate_entry_or_create_teaching_groups()
    {
        var (db, service, _) = CreateSut();
        var (_, entry) = await SeedEntryAsync(db);
        db.Set<TeachingGroup>().Add(NewTg(1, 10, "TG", TeachingGroupStatus.Active));
        await db.SaveChangesAsync();
        var tgCountBefore = await db.Set<TeachingGroup>().CountAsync();

        _ = await service.GetCompatibleTeachingGroupsForTimetableEntryAsync(entry.Id);

        Assert.Null((await db.Set<TimetableEntry>().AsNoTracking().SingleAsync(e => e.Id == entry.Id)).TeachingGroupId);
        Assert.Equal(tgCountBefore, await db.Set<TeachingGroup>().CountAsync());
    }
}
