using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-TG.5 Prompt 2 — Teaching Group management application boundary.</summary>
public sealed class AiSchedTg5Prompt2TeachingGroupManagementTests
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

    private static (ApplicationDbContext Db, TeachingGroupManagementApplicationService Mgmt, TeachingGroupSectionApplicationService Sections, AmbientCurrentUser User)
        CreateSut(int tenantId = 1)
    {
        var user = new AmbientCurrentUser { TenantId = tenantId };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("tg5-p2-" + Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);
        var projector = new TimetableSectionProjector(db, user);
        var sections = new TeachingGroupSectionApplicationService(db, db, user, projector);
        var resolver = new TeachingGroupMembershipResolver(db);
        var mgmt = new TeachingGroupManagementApplicationService(db, db, user, sections, resolver);
        return (db, mgmt, sections, user);
    }

    private static async Task<SubjectAllocation> SeedAllocationAsync(
        ApplicationDbContext db,
        int tenantId = 1,
        int allocationIdHint = 0)
    {
        var semester = await db.Set<Semester>()
            .FirstOrDefaultAsync(s =>
                s.TenantId == tenantId && s.CourseId == 1 && s.GroupId == 2 && s.Number == 3 && !s.IsDeleted);
        if (semester is null)
        {
            semester = new Semester
            {
                TenantId = tenantId,
                CourseId = 1,
                GroupId = 2,
                Number = 3,
                Name = "Semester III",
                IsHistoricalArchive = false,
                CreatedDate = DateTime.UtcNow,
            };
            db.Set<Semester>().Add(semester);
            await db.SaveChangesAsync();
        }

        var sa = new SubjectAllocation
        {
            TenantId = tenantId,
            AcademicYearId = 1,
            CourseId = 1,
            GroupId = 2,
            SemesterId = semester.Id,
            SubjectId = 17,
            StaffId = 1,
            DepartmentId = 1,
            WeeklyHours = 3,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            CreatedDate = DateTime.UtcNow,
        };
        db.Set<SubjectAllocation>().Add(sa);
        await db.SaveChangesAsync();
        return sa;
    }

    private static Section NewSection(int tenantId, string code, int semesterId = 0) => new()
    {
        TenantId = tenantId,
        CollegeId = 1,
        AcademicYearId = 1,
        CourseId = 1,
        GroupId = 2,
        SemesterId = semesterId > 0 ? semesterId : 3,
        SectionCode = code,
        SectionName = "Section " + code,
        CreatedDate = DateTime.UtcNow,
    };

    private static CreateTeachingGroupRequest CreateReq(
        int subjectAllocationId,
        string name,
        string? code = null,
        TeachingGroupType type = TeachingGroupType.Custom,
        int? expected = null,
        int? max = null,
        string? exclusionKey = null) => new()
    {
        SubjectAllocationId = subjectAllocationId,
        Name = name,
        Code = code,
        Type = type,
        MembershipSource = TeachingGroupMembershipSource.ExplicitStudents,
        ActivityKind = TeachingGroupActivityKind.Lecture,
        ExpectedStudentCount = expected,
        MaxTeachingCapacity = max,
        ExclusionGroupKey = exclusionKey,
        DisplayOrder = 0,
    };

    [Fact]
    public async Task Create_get_list_update_archive_happy_path()
    {
        var (db, mgmt, _, _) = CreateSut();
        var sa = await SeedAllocationAsync(db);

        var created = await mgmt.CreateAsync(CreateReq(sa.Id, "CA Lecture", "CA-LECTURE"));
        Assert.True(created.Id > 0);
        Assert.Equal(TeachingGroupStatus.Draft, created.Status);
        Assert.Equal(sa.Id, created.SubjectAllocationId);
        Assert.Equal(1, created.AcademicYearId);
        Assert.Equal(0, created.ResolvedStudentCount);

        var listed = await mgmt.ListBySubjectAllocationAsync(sa.Id);
        Assert.Single(listed);
        Assert.Equal(created.Id, listed[0].Id);

        var got = await mgmt.GetByIdAsync(created.Id);
        Assert.Equal("CA Lecture", got.Name);

        var updated = await mgmt.UpdateAsync(created.Id, new UpdateTeachingGroupRequest
        {
            Name = "CA Lecture Updated",
            Code = "CA-LECTURE",
            ActivityKind = TeachingGroupActivityKind.Lecture,
            ExpectedStudentCount = 40,
            MaxTeachingCapacity = 50,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            DisplayOrder = 1,
        });
        Assert.Equal("CA Lecture Updated", updated.Name);
        Assert.Equal(40, updated.ExpectedStudentCount);
        Assert.Equal(50, updated.MaxTeachingCapacity);

        var archived = await mgmt.ArchiveAsync(created.Id);
        Assert.Equal(TeachingGroupStatus.Archived, archived.Status);

        await Assert.ThrowsAsync<DomainException>(() => mgmt.UpdateAsync(created.Id, new UpdateTeachingGroupRequest
        {
            Name = "Nope",
            Code = "CA-LECTURE",
            ActivityKind = TeachingGroupActivityKind.Lecture,
            EffectiveFrom = new DateOnly(2026, 1, 1),
        }));
    }

    [Fact]
    public async Task One_SubjectAllocation_supports_multiple_TeachingGroups()
    {
        var (db, mgmt, _, _) = CreateSut();
        var sa = await SeedAllocationAsync(db);

        await mgmt.CreateAsync(CreateReq(sa.Id, "CA Lecture", "CA-LECTURE"));
        await mgmt.CreateAsync(CreateReq(sa.Id, "CA Lab A", "CA-LAB-A", TeachingGroupType.Laboratory));
        await mgmt.CreateAsync(CreateReq(sa.Id, "CA Lab B", "CA-LAB-B", TeachingGroupType.Laboratory));

        var listed = await mgmt.ListBySubjectAllocationAsync(sa.Id);
        Assert.Equal(3, listed.Count);
        Assert.DoesNotContain(listed, x => x.Code == null && listed.Count == 1);
    }

    [Fact]
    public async Task Create_does_not_auto_create_on_list_or_get()
    {
        var (db, mgmt, _, _) = CreateSut();
        var sa = await SeedAllocationAsync(db);

        var listed = await mgmt.ListBySubjectAllocationAsync(sa.Id);
        Assert.Empty(listed);
        Assert.Empty(db.Set<TeachingGroup>().ToList());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => mgmt.GetByIdAsync(99999));
        Assert.Empty(db.Set<TeachingGroup>().ToList());
    }

    [Fact]
    public async Task Duplicate_code_within_SubjectAllocation_rejected()
    {
        var (db, mgmt, _, _) = CreateSut();
        var sa = await SeedAllocationAsync(db);
        await mgmt.CreateAsync(CreateReq(sa.Id, "A", "DUP"));
        await Assert.ThrowsAsync<DomainException>(() => mgmt.CreateAsync(CreateReq(sa.Id, "B", "DUP")));
    }

    [Fact]
    public async Task Capacity_rules_expected_only_max_only_both_and_rejections()
    {
        var (db, mgmt, _, _) = CreateSut();
        var sa = await SeedAllocationAsync(db);

        var expectedOnly = await mgmt.CreateAsync(CreateReq(sa.Id, "E", "E1", expected: 30));
        Assert.Equal(30, expectedOnly.ExpectedStudentCount);
        Assert.Null(expectedOnly.MaxTeachingCapacity);

        var maxOnly = await mgmt.CreateAsync(CreateReq(sa.Id, "M", "M1", max: 40));
        Assert.Null(maxOnly.ExpectedStudentCount);
        Assert.Equal(40, maxOnly.MaxTeachingCapacity);

        var both = await mgmt.CreateAsync(CreateReq(sa.Id, "B", "B1", expected: 20, max: 40));
        Assert.Equal(20, both.ExpectedStudentCount);
        Assert.Equal(40, both.MaxTeachingCapacity);

        var zeroExpected = await mgmt.CreateAsync(CreateReq(sa.Id, "Z", "Z1", expected: 0));
        Assert.Equal(0, zeroExpected.ExpectedStudentCount);

        await Assert.ThrowsAsync<DomainException>(() =>
            mgmt.CreateAsync(CreateReq(sa.Id, "BadMax", "BM", max: 0)));
        await Assert.ThrowsAsync<DomainException>(() =>
            mgmt.CreateAsync(CreateReq(sa.Id, "BadBoth", "BB", expected: 50, max: 40)));
    }

    [Fact]
    public async Task Sections_add_remove_replace_project_and_duplicate_rejected()
    {
        var (db, mgmt, sections, _) = CreateSut();
        var sa = await SeedAllocationAsync(db);
        var tg = await mgmt.CreateAsync(CreateReq(sa.Id, "Custom", "C1"));
        // Activate so mutations match operational TG; Draft is already mutable.
        var tracked = await db.Set<TeachingGroup>().FirstAsync(x => x.Id == tg.Id);
        tracked.TransitionTo(TeachingGroupStatus.Active);
        await db.SaveChangesAsync();

        var secA = NewSection(1, "A", sa.SemesterId);
        var secB = NewSection(1, "B", sa.SemesterId);
        db.Set<Section>().AddRange(secA, secB);
        await db.SaveChangesAsync();

        var entry = new TimetableEntry
        {
            TenantId = 1,
            TimetableId = 1,
            DayOfWeek = 1,
            TimeSlotId = 1,
            SubjectAllocationId = sa.Id,
            TeachingGroupId = tg.Id,
            StaffId = 1,
            RoomId = 1,
            DepartmentId = 1,
            CourseId = 1,
            GroupId = 2,
            SemesterId = sa.SemesterId,
            SubjectId = 17,
            CreatedDate = DateTime.UtcNow,
        };
        db.Set<TimetableEntry>().Add(entry);
        await db.SaveChangesAsync();

        await sections.AddSectionAndProjectAsync(tg.Id, secA.Id);
        Assert.Single(await sections.GetSectionsAsync(tg.Id));
        Assert.Single(db.Set<TimetableSection>().Where(x => x.TimetableEntryId == entry.Id && !x.IsDeleted).ToList());

        await Assert.ThrowsAsync<DomainException>(() => sections.AddSectionAndProjectAsync(tg.Id, secA.Id));

        await sections.ReplaceSectionsAndProjectAsync(tg.Id, [secA.Id, secB.Id]);
        Assert.Equal(2, (await sections.GetSectionsAsync(tg.Id)).Count);
        Assert.Equal(2, db.Set<TimetableSection>().Count(x => x.TimetableEntryId == entry.Id && !x.IsDeleted));

        await sections.RemoveSectionAndProjectAsync(tg.Id, secB.Id);
        Assert.Single(await sections.GetSectionsAsync(tg.Id));
        Assert.Single(db.Set<TimetableSection>().Where(x => x.TimetableEntryId == entry.Id && !x.IsDeleted).ToList());
    }

    [Fact]
    public async Task Section_outside_academic_scope_rejected()
    {
        var (db, mgmt, sections, _) = CreateSut();
        var sa = await SeedAllocationAsync(db);
        var tg = await mgmt.CreateAsync(CreateReq(sa.Id, "Custom", "C2"));
        var wrong = NewSection(1, "X", sa.SemesterId);
        wrong.CourseId = 99;
        db.Set<Section>().Add(wrong);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            sections.AddSectionAndProjectAsync(tg.Id, wrong.Id));
    }

    [Fact]
    public async Task Tenant_isolation_hides_other_tenant_TeachingGroup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("tg5-p2-tenant-" + Guid.NewGuid().ToString("N"))
            .Options;
        var user1 = new AmbientCurrentUser { TenantId = 1 };
        var user2 = new AmbientCurrentUser { TenantId = 2 };
        var shared = new ApplicationDbContext(options, user1, NullLogger<ApplicationDbContext>.Instance);
        var sections1 = new TeachingGroupSectionApplicationService(shared, shared, user1, new TimetableSectionProjector(shared, user1));
        var mgmtUser1 = new TeachingGroupManagementApplicationService(shared, shared, user1, sections1, new TeachingGroupMembershipResolver(shared));
        var saTenant1 = await SeedAllocationAsync(shared, tenantId: 1);
        var tg = await mgmtUser1.CreateAsync(CreateReq(saTenant1.Id, "OnlyT1", "OT1"));

        var sharedAsT2 = new ApplicationDbContext(options, user2, NullLogger<ApplicationDbContext>.Instance);
        var sections2 = new TeachingGroupSectionApplicationService(sharedAsT2, sharedAsT2, user2, new TimetableSectionProjector(sharedAsT2, user2));
        var mgmtUser2 = new TeachingGroupManagementApplicationService(sharedAsT2, sharedAsT2, user2, sections2, new TeachingGroupMembershipResolver(sharedAsT2));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => mgmtUser2.GetByIdAsync(tg.Id));
        // Cross-tenant SubjectAllocation is not visible → list rejects rather than leaking TGs.
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            mgmtUser2.ListBySubjectAllocationAsync(saTenant1.Id));

        var saTenant2 = await SeedAllocationAsync(sharedAsT2, tenantId: 2);
        Assert.Empty(await mgmtUser2.ListBySubjectAllocationAsync(saTenant2.Id));
    }

    [Fact]
    public async Task Membership_get_returns_rows_without_mutation_api()
    {
        var (db, mgmt, _, _) = CreateSut();
        var sa = await SeedAllocationAsync(db);
        var tg = await mgmt.CreateAsync(CreateReq(sa.Id, "M", "M2"));
        // Eligible student required — Prompt 5 resolver filters by academic scope.
        var student = new Domain.Entities.Student
        {
            TenantId = 1,
            StudentNumber = "S42-P2",
            Name = "Member",
            CourseId = 1,
            GroupId = 2,
            SemesterId = sa.SemesterId,
            GenderId = 1,
            MediumId = 1,
            FirstLanguageId = 1,
            LanguageId = 1,
            CreatedDate = DateTime.UtcNow,
        };
        db.Set<Domain.Entities.Student>().Add(student);
        await db.SaveChangesAsync();
        db.Set<TeachingGroupMembership>().Add(new TeachingGroupMembership
        {
            TenantId = 1,
            TeachingGroupId = tg.Id,
            StudentId = student.Id,
            Inclusion = TeachingGroupMembershipInclusion.Include,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            IsCurrent = true,
            CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var rows = await mgmt.GetMembershipsAsync(tg.Id);
        Assert.Single(rows);
        Assert.Equal(student.Id, rows[0].StudentId);

        var detail = await mgmt.GetByIdAsync(tg.Id);
        Assert.Equal(1, detail.ResolvedStudentCount);
        Assert.Equal(1, detail.MembershipCount);
    }

    [Fact]
    public async Task Missing_SubjectAllocation_rejected_on_create_and_list()
    {
        var (_, mgmt, _, _) = CreateSut();
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            mgmt.CreateAsync(CreateReq(999, "X", "X")));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            mgmt.ListBySubjectAllocationAsync(999));
    }
}
