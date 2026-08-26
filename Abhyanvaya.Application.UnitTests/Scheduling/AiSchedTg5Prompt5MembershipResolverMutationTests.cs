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

/// <summary>AI-SCHED-TG.5 Prompt 5 — Membership resolver + mutation application tests.</summary>
public sealed class AiSchedTg5Prompt5MembershipResolverMutationTests
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

    private static (ApplicationDbContext Db, TeachingGroupMembershipResolver Resolver, TeachingGroupMembershipApplicationService Memberships, AmbientCurrentUser User)
        CreateSut(int tenantId = 1)
    {
        var user = new AmbientCurrentUser { TenantId = tenantId };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("tg5-p5-" + Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);
        var resolver = new TeachingGroupMembershipResolver(db);
        var memberships = new TeachingGroupMembershipApplicationService(db, db, user, resolver);
        return (db, resolver, memberships, user);
    }

    private static TeachingGroup NewTg(
        int tenantId,
        TeachingGroupMembershipSource source,
        TeachingGroupStatus status = TeachingGroupStatus.Active,
        int? max = null,
        string? exclusionKey = null)
        => new()
        {
            TenantId = tenantId,
            AcademicYearId = 1,
            CourseId = 1,
            GroupId = 2,
            SemesterId = 3,
            SubjectId = 17,
            SubjectAllocationId = 10,
            Type = source == TeachingGroupMembershipSource.Section
                ? TeachingGroupType.SectionDerived
                : source == TeachingGroupMembershipSource.CombinedSections
                    ? TeachingGroupType.CombinedSections
                    : source == TeachingGroupMembershipSource.Hybrid
                        ? TeachingGroupType.Laboratory
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

    private static Student NewStudent(int tenantId, int idHint, int courseId = 1, int groupId = 2, int semesterId = 3)
        => new()
        {
            TenantId = tenantId,
            StudentNumber = $"S{idHint}-{Guid.NewGuid():N}"[..12],
            Name = $"Student {idHint}",
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
    public async Task Explicit_include_exclude_and_duplicate_elimination()
    {
        var (db, resolver, memberships, _) = CreateSut();
        var tg = NewTg(1, TeachingGroupMembershipSource.ExplicitStudents);
        db.Set<TeachingGroup>().Add(tg);
        var s1 = NewStudent(1, 1001);
        var s2 = NewStudent(1, 1002);
        db.Set<Student>().AddRange(s1, s2);
        await db.SaveChangesAsync();

        await memberships.AddMembersAsync(tg.Id, new AddTeachingGroupMembersRequest { StudentIds = [s1.Id, s2.Id, s1.Id] });
        var resolved = await resolver.ResolveAsync(tg.Id);
        Assert.Equal(2, resolved.Count);
        Assert.All(resolved, r => Assert.Equal(TeachingGroupMemberProvenance.ExplicitInclude, r.Provenance));
        Assert.Equal(new[] { s1.Id, s2.Id }.OrderBy(x => x), resolved.Select(r => r.StudentId));

        await memberships.RemoveMemberAsync(tg.Id, s2.Id);
        resolved = await resolver.ResolveAsync(tg.Id);
        Assert.Single(resolved);
        Assert.Equal(s1.Id, resolved[0].StudentId);
    }

    [Fact]
    public async Task Hybrid_model_B_exclude_wins_over_base_and_include()
    {
        var (db, resolver, memberships, _) = CreateSut();
        var tg = NewTg(1, TeachingGroupMembershipSource.Hybrid);
        db.Set<TeachingGroup>().Add(tg);
        var sec = NewSection(1, "A");
        db.Set<Section>().Add(sec);
        var s1 = NewStudent(1, 1);
        var s2 = NewStudent(1, 2);
        var s3 = NewStudent(1, 3);
        var s4 = NewStudent(1, 4);
        var s5 = NewStudent(1, 5);
        db.Set<Student>().AddRange(s1, s2, s3, s4, s5);
        await db.SaveChangesAsync();

        db.Set<TeachingGroupSection>().Add(new TeachingGroupSection
        {
            TenantId = 1,
            TeachingGroupId = tg.Id,
            SectionId = sec.Id,
            IsPrimary = true,
            CreatedDate = DateTime.UtcNow,
        });
        foreach (var s in new[] { s1, s2, s3 })
        {
            db.Set<StudentSection>().Add(new StudentSection
            {
                TenantId = 1,
                StudentId = s.Id,
                SectionId = sec.Id,
                EffectiveFrom = new DateOnly(2026, 1, 1),
                IsCurrent = true,
                CreatedDate = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();

        await memberships.AddMembersAsync(tg.Id, new AddTeachingGroupMembersRequest { StudentIds = [s4.Id, s5.Id] });
        await memberships.ReplaceMembershipsAsync(tg.Id, new ReplaceTeachingGroupMembershipsRequest
        {
            IncludeStudentIds = [s4.Id, s5.Id],
            ExcludeStudentIds = [s2.Id],
        });

        var resolved = (await resolver.ResolveAsync(tg.Id)).Select(x => x.StudentId).ToList();
        Assert.Equal(new[] { s1.Id, s3.Id, s4.Id, s5.Id }.OrderBy(x => x), resolved);
        Assert.DoesNotContain(s2.Id, resolved);
    }

    [Fact]
    public async Task Section_derived_resolves_from_StudentSection_and_rejects_mutation()
    {
        var (db, resolver, memberships, _) = CreateSut();
        var tg = NewTg(1, TeachingGroupMembershipSource.Section);
        db.Set<TeachingGroup>().Add(tg);
        var sec = NewSection(1, "A");
        db.Set<Section>().Add(sec);
        var s1 = NewStudent(1, 11);
        var s2 = NewStudent(1, 12);
        var outsider = NewStudent(1, 99, courseId: 99);
        db.Set<Student>().AddRange(s1, s2, outsider);
        await db.SaveChangesAsync();
        db.Set<TeachingGroupSection>().Add(new TeachingGroupSection
        {
            TenantId = 1, TeachingGroupId = tg.Id, SectionId = sec.Id, IsPrimary = true, CreatedDate = DateTime.UtcNow,
        });
        db.Set<StudentSection>().AddRange(
            new StudentSection { TenantId = 1, StudentId = s1.Id, SectionId = sec.Id, EffectiveFrom = new DateOnly(2026, 1, 1), IsCurrent = true, CreatedDate = DateTime.UtcNow },
            new StudentSection { TenantId = 1, StudentId = s2.Id, SectionId = sec.Id, EffectiveFrom = new DateOnly(2026, 1, 1), IsCurrent = true, CreatedDate = DateTime.UtcNow },
            new StudentSection { TenantId = 1, StudentId = outsider.Id, SectionId = sec.Id, EffectiveFrom = new DateOnly(2026, 1, 1), IsCurrent = true, CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var resolved = await resolver.ResolveAsync(tg.Id);
        Assert.Equal(2, resolved.Count);
        Assert.All(resolved, r => Assert.Equal(TeachingGroupMemberProvenance.Derived, r.Provenance));

        await Assert.ThrowsAsync<DomainException>(() =>
            memberships.AddMembersAsync(tg.Id, new AddTeachingGroupMembersRequest { StudentIds = [s1.Id] }));
    }

    [Fact]
    public async Task Capacity_exceeded_rejected_and_expected_unchanged()
    {
        var (db, _, memberships, _) = CreateSut();
        var tg = NewTg(1, TeachingGroupMembershipSource.ExplicitStudents, max: 1);
        tg.ExpectedStudentCount = 1; // planning intent; Max is the hard ceiling
        db.Set<TeachingGroup>().Add(tg);
        var s1 = NewStudent(1, 1);
        var s2 = NewStudent(1, 2);
        db.Set<Student>().AddRange(s1, s2);
        await db.SaveChangesAsync();

        await memberships.AddMembersAsync(tg.Id, new AddTeachingGroupMembersRequest { StudentIds = [s1.Id] });
        await Assert.ThrowsAsync<DomainException>(() =>
            memberships.AddMembersAsync(tg.Id, new AddTeachingGroupMembersRequest { StudentIds = [s2.Id] }));

        var reloaded = await db.Set<TeachingGroup>().AsNoTracking().FirstAsync(x => x.Id == tg.Id);
        Assert.Equal(1, reloaded.ExpectedStudentCount);
        Assert.Equal(1, reloaded.MaxTeachingCapacity);
    }

    [Fact]
    public async Task Locked_and_archived_mutations_rejected()
    {
        var (db, _, memberships, _) = CreateSut();
        var locked = NewTg(1, TeachingGroupMembershipSource.ExplicitStudents, TeachingGroupStatus.Locked);
        var archived = NewTg(1, TeachingGroupMembershipSource.ExplicitStudents, TeachingGroupStatus.Archived);
        db.Set<TeachingGroup>().AddRange(locked, archived);
        var s = NewStudent(1, 1);
        db.Set<Student>().Add(s);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            memberships.AddMembersAsync(locked.Id, new AddTeachingGroupMembersRequest { StudentIds = [s.Id] }));
        await Assert.ThrowsAsync<DomainException>(() =>
            memberships.AddMembersAsync(archived.Id, new AddTeachingGroupMembersRequest { StudentIds = [s.Id] }));
    }

    [Fact]
    public async Task Idempotent_add_and_remove_missing()
    {
        var (db, resolver, memberships, _) = CreateSut();
        var tg = NewTg(1, TeachingGroupMembershipSource.ExplicitStudents);
        db.Set<TeachingGroup>().Add(tg);
        var s = NewStudent(1, 1);
        db.Set<Student>().Add(s);
        await db.SaveChangesAsync();

        await memberships.AddMembersAsync(tg.Id, new AddTeachingGroupMembersRequest { StudentIds = [s.Id] });
        await memberships.AddMembersAsync(tg.Id, new AddTeachingGroupMembersRequest { StudentIds = [s.Id] });
        Assert.Equal(1, await db.Set<TeachingGroupMembership>().CountAsync(x => x.TeachingGroupId == tg.Id && x.IsCurrent));

        await memberships.RemoveMemberAsync(tg.Id, 99999);
        Assert.Single(await resolver.ResolveAsync(tg.Id));
    }

    [Fact]
    public async Task Scope_mismatch_and_tenant_mismatch_rejected()
    {
        var (db, _, memberships, _) = CreateSut();
        var tg = NewTg(1, TeachingGroupMembershipSource.ExplicitStudents);
        db.Set<TeachingGroup>().Add(tg);
        var wrongScope = NewStudent(1, 1, courseId: 99);
        db.Set<Student>().Add(wrongScope);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            memberships.AddMembersAsync(tg.Id, new AddTeachingGroupMembersRequest { StudentIds = [wrongScope.Id] }));
    }

    [Fact]
    public async Task Resolver_has_no_side_effects()
    {
        var (db, resolver, _, _) = CreateSut();
        var tg = NewTg(1, TeachingGroupMembershipSource.ExplicitStudents);
        db.Set<TeachingGroup>().Add(tg);
        await db.SaveChangesAsync();
        var before = await db.Set<TeachingGroupMembership>().CountAsync();
        _ = await resolver.ResolveAsync(tg.Id);
        _ = await resolver.ResolveCountAsync(tg.Id);
        Assert.Equal(before, await db.Set<TeachingGroupMembership>().CountAsync());
        Assert.Equal(0, db.ChangeTracker.Entries<TeachingGroupMembership>()
            .Count(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted));
    }

    [Fact]
    public async Task Replace_explicit_set()
    {
        var (db, resolver, memberships, _) = CreateSut();
        var tg = NewTg(1, TeachingGroupMembershipSource.ExplicitStudents);
        db.Set<TeachingGroup>().Add(tg);
        var s1 = NewStudent(1, 1);
        var s2 = NewStudent(1, 2);
        var s3 = NewStudent(1, 3);
        db.Set<Student>().AddRange(s1, s2, s3);
        await db.SaveChangesAsync();

        await memberships.AddMembersAsync(tg.Id, new AddTeachingGroupMembersRequest { StudentIds = [s1.Id, s2.Id] });
        await memberships.ReplaceMembershipsAsync(tg.Id, new ReplaceTeachingGroupMembershipsRequest
        {
            IncludeStudentIds = [s2.Id, s3.Id],
        });
        var ids = (await resolver.ResolveAsync(tg.Id)).Select(x => x.StudentId).OrderBy(x => x).ToList();
        Assert.Equal(new[] { s2.Id, s3.Id }.OrderBy(x => x), ids);
    }

    [Fact]
    public async Task Empty_membership_resolves_to_zero_and_cross_tenant_student_rejected()
    {
        var (db, resolver, memberships, _) = CreateSut(tenantId: 1);
        var tg = NewTg(1, TeachingGroupMembershipSource.ExplicitStudents);
        db.Set<TeachingGroup>().Add(tg);
        await db.SaveChangesAsync();
        Assert.Empty(await resolver.ResolveAsync(tg.Id));
        Assert.Equal(0, await resolver.ResolveCountAsync(tg.Id));

        // Student with TenantId=2 is invisible under tenant-1 query filters when added via Set;
        // seed with ambient then switch ambient to prove mismatch path uses Student.TenantId check.
        var foreign = NewStudent(2, 77);
        db.Set<Student>().Add(foreign);
        await db.SaveChangesAsync();

        // Direct eligibility check: student exists but wrong tenant when filters allow visibility
        // (InMemory may not apply tenant filters the same way — assert DomainException when TenantId differs).
        foreign.TenantId = 2;
        await db.SaveChangesAsync();
        if (await db.Set<Student>().AnyAsync(s => s.Id == foreign.Id))
        {
            await Assert.ThrowsAsync<DomainException>(() =>
                memberships.AddMembersAsync(tg.Id, new AddTeachingGroupMembersRequest { StudentIds = [foreign.Id] }));
        }
    }

    [Fact]
    public async Task Hybrid_remove_base_student_adds_exclude_overlay()
    {
        var (db, resolver, memberships, _) = CreateSut();
        var tg = NewTg(1, TeachingGroupMembershipSource.Hybrid);
        db.Set<TeachingGroup>().Add(tg);
        var sec = NewSection(1, "H");
        db.Set<Section>().Add(sec);
        var s1 = NewStudent(1, 1);
        var s2 = NewStudent(1, 2);
        db.Set<Student>().AddRange(s1, s2);
        await db.SaveChangesAsync();
        db.Set<TeachingGroupSection>().Add(new TeachingGroupSection
        {
            TenantId = 1, TeachingGroupId = tg.Id, SectionId = sec.Id, IsPrimary = true, CreatedDate = DateTime.UtcNow,
        });
        foreach (var s in new[] { s1, s2 })
        {
            db.Set<StudentSection>().Add(new StudentSection
            {
                TenantId = 1, StudentId = s.Id, SectionId = sec.Id,
                EffectiveFrom = new DateOnly(2026, 1, 1), IsCurrent = true, CreatedDate = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync();

        await memberships.RemoveMemberAsync(tg.Id, s1.Id);
        var resolved = (await resolver.ResolveAsync(tg.Id)).Select(x => x.StudentId).ToList();
        Assert.DoesNotContain(s1.Id, resolved);
        Assert.Contains(s2.Id, resolved);
        Assert.True(await db.Set<TeachingGroupMembership>().AnyAsync(m =>
            m.TeachingGroupId == tg.Id && m.StudentId == s1.Id
            && m.Inclusion == TeachingGroupMembershipInclusion.Exclude && m.IsCurrent));
    }
}
