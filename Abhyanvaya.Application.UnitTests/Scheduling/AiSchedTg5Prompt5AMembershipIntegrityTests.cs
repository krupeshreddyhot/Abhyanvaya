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

/// <summary>AI-SCHED-TG.5 Prompt 5A — Membership integrity hardening tests.</summary>
public sealed class AiSchedTg5Prompt5AMembershipIntegrityTests
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

    private static (ApplicationDbContext Db, TeachingGroupMembershipResolver Resolver, TeachingGroupMembershipApplicationService Memberships)
        CreateSut(int tenantId = 1)
    {
        var user = new AmbientCurrentUser { TenantId = tenantId };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("tg5-p5a-" + Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);
        var resolver = new TeachingGroupMembershipResolver(db);
        var memberships = new TeachingGroupMembershipApplicationService(db, db, user, resolver);
        return (db, resolver, memberships);
    }

    private static TeachingGroup NewTg(
        int tenantId,
        TeachingGroupMembershipSource source,
        TeachingGroupStatus status = TeachingGroupStatus.Active,
        int? max = null,
        string? exclusionKey = null,
        int subjectAllocationId = 10)
        => new()
        {
            TenantId = tenantId,
            AcademicYearId = 1,
            CourseId = 1,
            GroupId = 2,
            SemesterId = 3,
            SubjectId = 17,
            SubjectAllocationId = subjectAllocationId,
            Type = source == TeachingGroupMembershipSource.Hybrid
                ? TeachingGroupType.Laboratory
                : source == TeachingGroupMembershipSource.Section
                    ? TeachingGroupType.SectionDerived
                    : TeachingGroupType.Custom,
            MembershipSource = source,
            Status = status,
            ActivityKind = TeachingGroupActivityKind.Lecture,
            Code = "TG",
            Name = "TG",
            MaxTeachingCapacity = max,
            ExclusionGroupKey = exclusionKey,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            CreatedDate = DateTime.UtcNow,
        };

    private static Student NewStudent(int tenantId, int hint, int courseId = 1, int groupId = 2, int semesterId = 3)
        => new()
        {
            TenantId = tenantId,
            StudentNumber = $"S{hint}-{Guid.NewGuid():N}"[..12],
            Name = $"Student {hint}",
            CourseId = courseId,
            GroupId = groupId,
            SemesterId = semesterId,
            GenderId = 1,
            MediumId = 1,
            FirstLanguageId = 1,
            LanguageId = 1,
            CreatedDate = DateTime.UtcNow,
        };

    private static Section NewSection(int tenantId, string code) => new()
    {
        TenantId = tenantId,
        CollegeId = 1,
        AcademicYearId = 1,
        CourseId = 1,
        GroupId = 2,
        SemesterId = 3,
        SectionCode = code,
        SectionName = code,
        CreatedDate = DateTime.UtcNow,
    };

    [Fact]
    public async Task ExclusionGroupKey_rejects_overlap_with_section_resolved_peer()
    {
        var (db, _, memberships) = CreateSut();
        const string key = "LAB-SPLIT";
        var sectionTg = NewTg(1, TeachingGroupMembershipSource.Section, exclusionKey: key);
        var explicitTg = NewTg(1, TeachingGroupMembershipSource.ExplicitStudents, exclusionKey: key);
        db.Set<TeachingGroup>().AddRange(sectionTg, explicitTg);
        var sec = NewSection(1, "A");
        db.Set<Section>().Add(sec);
        var s = NewStudent(1, 101);
        db.Set<Student>().Add(s);
        await db.SaveChangesAsync();

        db.Set<TeachingGroupSection>().Add(new TeachingGroupSection
        {
            TenantId = 1, TeachingGroupId = sectionTg.Id, SectionId = sec.Id, IsPrimary = true, CreatedDate = DateTime.UtcNow,
        });
        db.Set<StudentSection>().Add(new StudentSection
        {
            TenantId = 1, StudentId = s.Id, SectionId = sec.Id,
            EffectiveFrom = new DateOnly(2026, 1, 1), IsCurrent = true, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            memberships.AddMembersAsync(explicitTg.Id, new AddTeachingGroupMembersRequest { StudentIds = [s.Id] }));
    }

    [Fact]
    public async Task ExclusionGroupKey_allows_when_peer_excludes_student_from_hybrid_base()
    {
        var (db, _, memberships) = CreateSut();
        const string key = "LAB-SPLIT";
        var hybrid = NewTg(1, TeachingGroupMembershipSource.Hybrid, exclusionKey: key);
        var explicitTg = NewTg(1, TeachingGroupMembershipSource.ExplicitStudents, exclusionKey: key);
        db.Set<TeachingGroup>().AddRange(hybrid, explicitTg);
        var sec = NewSection(1, "H");
        db.Set<Section>().Add(sec);
        var s = NewStudent(1, 7);
        db.Set<Student>().Add(s);
        await db.SaveChangesAsync();
        db.Set<TeachingGroupSection>().Add(new TeachingGroupSection
        {
            TenantId = 1, TeachingGroupId = hybrid.Id, SectionId = sec.Id, IsPrimary = true, CreatedDate = DateTime.UtcNow,
        });
        db.Set<StudentSection>().Add(new StudentSection
        {
            TenantId = 1, StudentId = s.Id, SectionId = sec.Id,
            EffectiveFrom = new DateOnly(2026, 1, 1), IsCurrent = true, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await memberships.ReplaceMembershipsAsync(hybrid.Id, new ReplaceTeachingGroupMembershipsRequest
        {
            ExcludeStudentIds = [s.Id],
        });

        await memberships.AddMembersAsync(explicitTg.Id, new AddTeachingGroupMembersRequest { StudentIds = [s.Id] });
        Assert.Equal(1, await db.Set<TeachingGroupMembership>().CountAsync(m =>
            m.TeachingGroupId == explicitTg.Id && m.IsCurrent));
    }

    [Fact]
    public async Task ExclusionGroupKey_archived_peer_ignored()
    {
        var (db, _, memberships) = CreateSut();
        const string key = "LAB-SPLIT";
        var archived = NewTg(1, TeachingGroupMembershipSource.ExplicitStudents, TeachingGroupStatus.Archived, exclusionKey: key);
        var active = NewTg(1, TeachingGroupMembershipSource.ExplicitStudents, exclusionKey: key);
        db.Set<TeachingGroup>().AddRange(archived, active);
        var s = NewStudent(1, 1);
        db.Set<Student>().Add(s);
        await db.SaveChangesAsync();
        db.Set<TeachingGroupMembership>().Add(new TeachingGroupMembership
        {
            TenantId = 1, TeachingGroupId = archived.Id, StudentId = s.Id,
            Inclusion = TeachingGroupMembershipInclusion.Include,
            EffectiveFrom = new DateOnly(2026, 1, 1), IsCurrent = true, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await memberships.AddMembersAsync(active.Id, new AddTeachingGroupMembersRequest { StudentIds = [s.Id] });
        Assert.Equal(1, await db.Set<TeachingGroupMembership>().CountAsync(m =>
            m.TeachingGroupId == active.Id && m.IsCurrent));
    }

    [Fact]
    public async Task Capacity_boundary_exact_max_pass_above_max_reject_null_max_pass()
    {
        var (db, _, memberships) = CreateSut();
        var atMax = NewTg(1, TeachingGroupMembershipSource.ExplicitStudents, max: 2);
        var unlimited = NewTg(1, TeachingGroupMembershipSource.ExplicitStudents, max: null, subjectAllocationId: 11);
        db.Set<TeachingGroup>().AddRange(atMax, unlimited);
        var s1 = NewStudent(1, 1);
        var s2 = NewStudent(1, 2);
        var s3 = NewStudent(1, 3);
        db.Set<Student>().AddRange(s1, s2, s3);
        await db.SaveChangesAsync();

        await memberships.AddMembersAsync(atMax.Id, new AddTeachingGroupMembersRequest { StudentIds = [s1.Id, s2.Id] });
        await Assert.ThrowsAsync<DomainException>(() =>
            memberships.AddMembersAsync(atMax.Id, new AddTeachingGroupMembersRequest { StudentIds = [s3.Id] }));

        await memberships.AddMembersAsync(unlimited.Id, new AddTeachingGroupMembersRequest
        {
            StudentIds = [s1.Id, s2.Id, s3.Id],
        });
    }

    [Fact]
    public void Capacity_domain_rejects_max_zero_and_expected_above_max_allows_expected_zero()
    {
        var tg = NewTg(1, TeachingGroupMembershipSource.ExplicitStudents);
        Assert.Throws<InvalidOperationException>(() => tg.SetCapacity(expectedStudentCount: 0, maxTeachingCapacity: 0));
        tg.SetCapacity(expectedStudentCount: 0, maxTeachingCapacity: 5);
        Assert.Equal(0, tg.ExpectedStudentCount);
        Assert.Throws<InvalidOperationException>(() => tg.SetCapacity(expectedStudentCount: 6, maxTeachingCapacity: 5));
        tg.SetCapacity(expectedStudentCount: 5, maxTeachingCapacity: 5);
        Assert.Equal(5, tg.ExpectedStudentCount);
    }

    [Fact]
    public async Task Max_capacity_zero_on_entity_rejects_mutation()
    {
        var (db, _, memberships) = CreateSut();
        var tg = NewTg(1, TeachingGroupMembershipSource.ExplicitStudents, max: 0);
        db.Set<TeachingGroup>().Add(tg);
        var s = NewStudent(1, 1);
        db.Set<Student>().Add(s);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            memberships.AddMembersAsync(tg.Id, new AddTeachingGroupMembersRequest { StudentIds = [s.Id] }));
    }

    [Fact]
    public async Task Draft_and_Active_mutation_allowed_Locked_Archived_rejected()
    {
        var (db, _, memberships) = CreateSut();
        var draft = NewTg(1, TeachingGroupMembershipSource.ExplicitStudents, TeachingGroupStatus.Draft);
        var active = NewTg(1, TeachingGroupMembershipSource.ExplicitStudents, TeachingGroupStatus.Active, subjectAllocationId: 11);
        var locked = NewTg(1, TeachingGroupMembershipSource.ExplicitStudents, TeachingGroupStatus.Locked, subjectAllocationId: 12);
        var archived = NewTg(1, TeachingGroupMembershipSource.ExplicitStudents, TeachingGroupStatus.Archived, subjectAllocationId: 13);
        db.Set<TeachingGroup>().AddRange(draft, active, locked, archived);
        var s = NewStudent(1, 1);
        db.Set<Student>().Add(s);
        await db.SaveChangesAsync();

        await memberships.AddMembersAsync(draft.Id, new AddTeachingGroupMembersRequest { StudentIds = [s.Id] });
        await memberships.AddMembersAsync(active.Id, new AddTeachingGroupMembersRequest { StudentIds = [s.Id] });
        await Assert.ThrowsAsync<DomainException>(() =>
            memberships.AddMembersAsync(locked.Id, new AddTeachingGroupMembersRequest { StudentIds = [s.Id] }));
        await Assert.ThrowsAsync<DomainException>(() =>
            memberships.AddMembersAsync(archived.Id, new AddTeachingGroupMembersRequest { StudentIds = [s.Id] }));
    }

    [Fact]
    public async Task Resolver_repeated_execution_is_deterministic()
    {
        var (db, resolver, memberships) = CreateSut();
        var tg = NewTg(1, TeachingGroupMembershipSource.ExplicitStudents);
        db.Set<TeachingGroup>().Add(tg);
        var s1 = NewStudent(1, 30);
        var s2 = NewStudent(1, 10);
        var s3 = NewStudent(1, 20);
        db.Set<Student>().AddRange(s1, s2, s3);
        await db.SaveChangesAsync();
        await memberships.AddMembersAsync(tg.Id, new AddTeachingGroupMembersRequest
        {
            StudentIds = [s1.Id, s2.Id, s3.Id],
        });

        var a = await resolver.ResolveAsync(tg.Id);
        var b = await resolver.ResolveAsync(tg.Id);
        Assert.Equal(a.Select(x => x.StudentId), b.Select(x => x.StudentId));
        Assert.Equal(a.Select(x => x.StudentId).OrderBy(x => x), a.Select(x => x.StudentId));
        Assert.Equal(a.Count, await resolver.ResolveCountAsync(tg.Id));
        Assert.DoesNotContain(typeof(ResolvedTeachingGroupMemberDto).GetProperties(),
            p => p.Name == "IsExcludedFromBase");
    }

    [Fact]
    public async Task Replace_idempotent_same_payload()
    {
        var (db, resolver, memberships) = CreateSut();
        var tg = NewTg(1, TeachingGroupMembershipSource.ExplicitStudents);
        db.Set<TeachingGroup>().Add(tg);
        var s1 = NewStudent(1, 1);
        var s2 = NewStudent(1, 2);
        db.Set<Student>().AddRange(s1, s2);
        await db.SaveChangesAsync();

        var payload = new ReplaceTeachingGroupMembershipsRequest { IncludeStudentIds = [s1.Id, s2.Id] };
        await memberships.ReplaceMembershipsAsync(tg.Id, payload);
        await memberships.ReplaceMembershipsAsync(tg.Id, payload);
        Assert.Equal(2, await db.Set<TeachingGroupMembership>().CountAsync(m => m.TeachingGroupId == tg.Id && m.IsCurrent));
        Assert.Equal(2, (await resolver.ResolveAsync(tg.Id)).Count);
    }

    [Fact]
    public async Task Combined_sections_union_and_student_subject_base()
    {
        var (db, resolver, _) = CreateSut();
        var combined = NewTg(1, TeachingGroupMembershipSource.CombinedSections);
        var elective = NewTg(1, TeachingGroupMembershipSource.StudentSubject, subjectAllocationId: 11);
        db.Set<TeachingGroup>().AddRange(combined, elective);
        var a = NewSection(1, "A");
        var b = NewSection(1, "B");
        db.Set<Section>().AddRange(a, b);
        var s1 = NewStudent(1, 1);
        var s2 = NewStudent(1, 2);
        var s3 = NewStudent(1, 3);
        db.Set<Student>().AddRange(s1, s2, s3);
        await db.SaveChangesAsync();

        db.Set<TeachingGroupSection>().AddRange(
            new TeachingGroupSection { TenantId = 1, TeachingGroupId = combined.Id, SectionId = a.Id, IsPrimary = true, CreatedDate = DateTime.UtcNow },
            new TeachingGroupSection { TenantId = 1, TeachingGroupId = combined.Id, SectionId = b.Id, IsPrimary = false, CreatedDate = DateTime.UtcNow });
        db.Set<StudentSection>().AddRange(
            new StudentSection { TenantId = 1, StudentId = s1.Id, SectionId = a.Id, EffectiveFrom = new DateOnly(2026, 1, 1), IsCurrent = true, CreatedDate = DateTime.UtcNow },
            new StudentSection { TenantId = 1, StudentId = s2.Id, SectionId = b.Id, EffectiveFrom = new DateOnly(2026, 1, 1), IsCurrent = true, CreatedDate = DateTime.UtcNow });
        db.Set<StudentSubject>().Add(new StudentSubject
        {
            TenantId = 1, StudentId = s3.Id, SubjectId = elective.SubjectId, CreatedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var combinedIds = (await resolver.ResolveAsync(combined.Id)).Select(x => x.StudentId).OrderBy(x => x).ToList();
        Assert.Equal(new[] { s1.Id, s2.Id }.OrderBy(x => x), combinedIds);
        Assert.Equal(new[] { s3.Id }, (await resolver.ResolveAsync(elective.Id)).Select(x => x.StudentId));
    }
}
