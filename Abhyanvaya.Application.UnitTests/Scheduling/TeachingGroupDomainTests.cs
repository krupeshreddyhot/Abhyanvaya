using System.Reflection;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-TG.3 Prompt 1A — Domain correction & final gate tests (no database).</summary>
public sealed class TeachingGroupDomainTests
{
    // --- Section link ---

    [Fact]
    public void SectionDerived_requires_exactly_one_Section()
    {
        TeachingGroupRules.ValidateSectionLinks(TeachingGroupType.SectionDerived, [10]);
        Assert.Throws<InvalidOperationException>(() =>
            TeachingGroupRules.ValidateSectionLinks(TeachingGroupType.SectionDerived, []));
        Assert.Throws<InvalidOperationException>(() =>
            TeachingGroupRules.ValidateSectionLinks(TeachingGroupType.SectionDerived, [10, 11]));
    }

    [Fact]
    public void CombinedSections_requires_at_least_two_Sections()
    {
        TeachingGroupRules.ValidateSectionLinks(TeachingGroupType.CombinedSections, [1, 2]);
        TeachingGroupRules.ValidateSectionLinks(TeachingGroupType.CombinedSections, [1, 2, 3]);
        Assert.Throws<InvalidOperationException>(() =>
            TeachingGroupRules.ValidateSectionLinks(TeachingGroupType.CombinedSections, [1]));
    }

    [Fact]
    public void CombinedSections_is_represented_entirely_by_TeachingGroupSection()
    {
        Assert.Null(typeof(TeachingGroup).GetProperty("SectionGroupId"));
        var tg = NewGroup(TeachingGroupType.CombinedSections, TeachingGroupMembershipSource.CombinedSections);
        tg.Sections.Add(new TeachingGroupSection { SectionId = 1, TenantId = 1 });
        tg.Sections.Add(new TeachingGroupSection { SectionId = 2, TenantId = 1 });
        TeachingGroupRules.ValidateSectionLinks(
            TeachingGroupType.CombinedSections,
            tg.Sections.Select(s => s.SectionId).ToList());
        Assert.Equal(2, tg.Sections.Count);
    }

    [Fact]
    public void Elective_does_not_require_Section()
    {
        TeachingGroupRules.ValidateSectionLinks(TeachingGroupType.Elective, []);
        Assert.Throws<InvalidOperationException>(() =>
            TeachingGroupRules.ValidateSectionLinks(TeachingGroupType.Elective, [5]));
    }

    [Fact]
    public void Laboratory_may_optionally_reference_Sections()
    {
        TeachingGroupRules.ValidateSectionLinks(TeachingGroupType.Laboratory, []);
        TeachingGroupRules.ValidateSectionLinks(TeachingGroupType.Laboratory, [3]);
    }

    [Fact]
    public void CapacitySplit_may_optionally_reference_Sections()
    {
        TeachingGroupRules.ValidateSectionLinks(TeachingGroupType.CapacitySplit, []);
        TeachingGroupRules.ValidateSectionLinks(TeachingGroupType.CapacitySplit, [9]);
        TeachingGroupRules.ValidateCapacitySplitExclusionKey(TeachingGroupType.CapacitySplit, "CA-SPLIT-2026");
        Assert.Throws<InvalidOperationException>(() =>
            TeachingGroupRules.ValidateCapacitySplitExclusionKey(TeachingGroupType.CapacitySplit, null));
    }

    // --- Capacity null/zero ---

    [Fact]
    public void Expected_null_and_Max_null_are_valid()
    {
        var tg = NewGroup(TeachingGroupType.Custom, TeachingGroupMembershipSource.ExplicitStudents);
        tg.SetCapacity(null, null);
        Assert.Null(tg.ExpectedStudentCount);
        Assert.Null(tg.MaxTeachingCapacity);
    }

    [Fact]
    public void Expected_zero_is_valid_for_Draft()
    {
        var tg = NewGroup(TeachingGroupType.Custom, TeachingGroupMembershipSource.ExplicitStudents);
        Assert.Equal(TeachingGroupStatus.Draft, tg.Status);
        tg.SetCapacity(0, null);
        Assert.Equal(0, tg.ExpectedStudentCount);
        Assert.Null(tg.MaxTeachingCapacity);
    }

    [Fact]
    public void Expected_negative_is_rejected()
    {
        var tg = NewGroup(TeachingGroupType.Custom, TeachingGroupMembershipSource.ExplicitStudents);
        Assert.Throws<InvalidOperationException>(() => tg.SetCapacity(-1, 40));
    }

    [Fact]
    public void Max_null_is_valid()
    {
        var tg = NewGroup(TeachingGroupType.Custom, TeachingGroupMembershipSource.ExplicitStudents);
        tg.SetCapacity(10, null);
        Assert.Equal(10, tg.ExpectedStudentCount);
        Assert.Null(tg.MaxTeachingCapacity);
    }

    [Fact]
    public void Max_zero_is_rejected()
    {
        var tg = NewGroup(TeachingGroupType.Custom, TeachingGroupMembershipSource.ExplicitStudents);
        Assert.Throws<InvalidOperationException>(() => tg.SetCapacity(10, 0));
        Assert.Throws<InvalidOperationException>(() => tg.SetCapacity(null, 0));
    }

    [Fact]
    public void Max_negative_is_rejected()
    {
        var tg = NewGroup(TeachingGroupType.Custom, TeachingGroupMembershipSource.ExplicitStudents);
        Assert.Throws<InvalidOperationException>(() => tg.SetCapacity(null, -1));
    }

    [Fact]
    public void Expected_less_than_Max_is_valid()
    {
        var tg = NewGroup(TeachingGroupType.Custom, TeachingGroupMembershipSource.ExplicitStudents);
        tg.SetCapacity(30, 40);
        Assert.Equal(30, tg.ExpectedStudentCount);
        Assert.Equal(40, tg.MaxTeachingCapacity);
    }

    [Fact]
    public void Expected_equal_Max_is_valid()
    {
        var tg = NewGroup(TeachingGroupType.Custom, TeachingGroupMembershipSource.ExplicitStudents);
        tg.SetCapacity(40, 40);
        Assert.Equal(40, tg.ExpectedStudentCount);
        Assert.Equal(40, tg.MaxTeachingCapacity);
    }

    [Fact]
    public void Expected_greater_than_Max_is_rejected()
    {
        var tg = NewGroup(TeachingGroupType.Custom, TeachingGroupMembershipSource.ExplicitStudents);
        Assert.Throws<InvalidOperationException>(() => tg.SetCapacity(50, 40));
    }

    [Fact]
    public void Expected_configured_with_Max_null_is_valid()
    {
        var tg = NewGroup(TeachingGroupType.Custom, TeachingGroupMembershipSource.ExplicitStudents);
        tg.SetCapacity(60, null);
        Assert.Equal(60, tg.ExpectedStudentCount);
        Assert.Null(tg.MaxTeachingCapacity);
    }

    [Fact]
    public void Max_configured_with_Expected_null_is_valid()
    {
        var tg = NewGroup(TeachingGroupType.Custom, TeachingGroupMembershipSource.ExplicitStudents);
        tg.SetCapacity(null, 40);
        Assert.Null(tg.ExpectedStudentCount);
        Assert.Equal(40, tg.MaxTeachingCapacity);
    }

    [Fact]
    public void Zero_is_not_silently_normalized_to_null()
    {
        var tg = NewGroup(TeachingGroupType.Custom, TeachingGroupMembershipSource.ExplicitStudents);
        tg.SetCapacity(0, null);
        Assert.Equal(0, tg.ExpectedStudentCount);
        Assert.False(tg.ExpectedStudentCount is null);
    }

    // --- Derived count ---

    [Fact]
    public void ResolvedStudentCount_remains_derived_and_deduplicates()
    {
        Assert.Null(typeof(TeachingGroup).GetProperty("ResolvedStudentCount"));
        Assert.Null(typeof(TeachingGroup).GetProperty("CurrentStrength"));
        Assert.Null(typeof(TeachingGroup).GetProperty("ActualStudentCount"));
        var resolved = TeachingGroup.ComputeResolvedStudentCount([1, 2, 3, 3]);
        Assert.Equal(3, resolved);
    }

    [Fact]
    public void ExpectedStudentCount_is_distinct_from_ResolvedStudentCount()
    {
        var tg = NewGroup(TeachingGroupType.SectionDerived, TeachingGroupMembershipSource.Section);
        tg.SetCapacity(55, 60);
        var resolved = TeachingGroup.ComputeResolvedStudentCount([1, 2, 3, 3]);
        Assert.Equal(55, tg.ExpectedStudentCount);
        Assert.Equal(3, resolved);
        Assert.NotEqual(tg.ExpectedStudentCount, resolved);
    }

    [Fact]
    public void MaxTeachingCapacity_rejects_resolved_over_max()
    {
        var tg = NewGroup(TeachingGroupType.CapacitySplit, TeachingGroupMembershipSource.ExplicitStudents);
        tg.ExclusionGroupKey = "CA-SPLIT-2026";
        tg.SetCapacity(40, 40);
        Assert.Throws<InvalidOperationException>(() => tg.EnsureResolvedWithinMaxCapacity(45));
        tg.EnsureResolvedWithinMaxCapacity(40);
    }

    // --- Membership / StudentSection boundary (no empty assertion method) ---

    [Fact]
    public void TeachingGroupMembership_carries_StudentId_only_and_has_no_StudentSection_API()
    {
        Assert.Null(typeof(TeachingGroupRules).GetMethod("EnsureMembershipDoesNotClaimStudentSectionMutation"));
        var tg = NewGroup(TeachingGroupType.StudentSubset, TeachingGroupMembershipSource.ExplicitStudents);
        tg.Memberships.Add(new TeachingGroupMembership
        {
            TeachingGroupId = 1,
            StudentId = 101,
            Inclusion = TeachingGroupMembershipInclusion.Include,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            TenantId = tg.TenantId,
        });
        Assert.Single(tg.Memberships);
        Assert.Null(typeof(TeachingGroupMembership).GetProperty("StudentSectionId"));
        Assert.Null(typeof(TeachingGroup).GetMethod("AssignStudentSection"));
    }

    // --- Mutual exclusion ---

    [Fact]
    public void Same_exclusion_group_same_student_is_rejected()
    {
        var peers = new[]
        {
            (TeachingGroupId: 1, TenantId: 1, SubjectAllocationId: 50, ExclusionGroupKey: (string?)"CA-LAB-2026",
                Status: TeachingGroupStatus.Active, MemberStudentIds: (IReadOnlyCollection<int>)[101, 102]),
        };
        Assert.Throws<InvalidOperationException>(() =>
            TeachingGroupRules.EnsureStudentNotInMutuallyExclusiveGroup(
                1, 50, 101, "CA-LAB-2026", excludeTeachingGroupId: 2, peers));
    }

    [Fact]
    public void Null_exclusion_key_means_no_mutual_exclusion()
    {
        var peers = new[]
        {
            (TeachingGroupId: 1, TenantId: 1, SubjectAllocationId: 50, ExclusionGroupKey: (string?)"CA-LAB-2026",
                Status: TeachingGroupStatus.Active, MemberStudentIds: (IReadOnlyCollection<int>)[101]),
        };
        TeachingGroupRules.EnsureStudentNotInMutuallyExclusiveGroup(
            1, 50, 101, exclusionGroupKey: null, excludeTeachingGroupId: 2, peers);
    }

    [Fact]
    public void Lecture_null_plus_Lab_key_is_allowed()
    {
        var peers = new[]
        {
            (TeachingGroupId: 1, TenantId: 1, SubjectAllocationId: 50, ExclusionGroupKey: (string?)null,
                Status: TeachingGroupStatus.Active, MemberStudentIds: (IReadOnlyCollection<int>)[101]),
        };
        TeachingGroupRules.EnsureStudentNotInMutuallyExclusiveGroup(
            1, 50, 101, "CA-LAB-2026", excludeTeachingGroupId: 2, peers);
    }

    [Fact]
    public void Archived_peer_does_not_block_membership()
    {
        var peers = new[]
        {
            (TeachingGroupId: 1, TenantId: 1, SubjectAllocationId: 50, ExclusionGroupKey: (string?)"CA-LAB-2026",
                Status: TeachingGroupStatus.Archived, MemberStudentIds: (IReadOnlyCollection<int>)[101]),
        };
        TeachingGroupRules.EnsureStudentNotInMutuallyExclusiveGroup(
            1, 50, 101, "CA-LAB-2026", excludeTeachingGroupId: 2, peers);
    }

    [Fact]
    public void CapacitySplit_siblings_are_mutually_exclusive()
    {
        var peers = new[]
        {
            (TeachingGroupId: 10, TenantId: 1, SubjectAllocationId: 7, ExclusionGroupKey: (string?)"CA-SPLIT-2026",
                Status: TeachingGroupStatus.Active, MemberStudentIds: (IReadOnlyCollection<int>)[41]),
        };
        Assert.Throws<InvalidOperationException>(() =>
            TeachingGroupRules.EnsureStudentNotInMutuallyExclusiveGroup(
                1, 7, 41, "CA-SPLIT-2026", excludeTeachingGroupId: 11, peers));
    }

    // --- Lifecycle ---

    [Fact]
    public void Draft_and_Active_mutation_allowed()
    {
        var tg = NewGroup(TeachingGroupType.Custom, TeachingGroupMembershipSource.ExplicitStudents);
        tg.EnsureCanMutate();
        tg.SetCapacity(5, 10);
        tg.TransitionTo(TeachingGroupStatus.Active);
        tg.EnsureCanMutate();
        tg.SetCapacity(6, 10);
    }

    [Fact]
    public void Locked_mutation_rejected()
    {
        var tg = NewGroup(TeachingGroupType.SectionDerived, TeachingGroupMembershipSource.Section);
        tg.TransitionTo(TeachingGroupStatus.Active);
        tg.TransitionTo(TeachingGroupStatus.Locked);
        Assert.Throws<InvalidOperationException>(() => tg.EnsureCanMutate());
        Assert.Throws<InvalidOperationException>(() => tg.SetCapacity(10, 10));
    }

    [Fact]
    public void Archived_mutation_and_attach_rejected()
    {
        var tg = NewGroup(TeachingGroupType.Elective, TeachingGroupMembershipSource.StudentSubject);
        tg.TransitionTo(TeachingGroupStatus.Active);
        tg.TransitionTo(TeachingGroupStatus.Archived);
        Assert.Throws<InvalidOperationException>(() => tg.EnsureCanMutate());
        Assert.Throws<InvalidOperationException>(() => tg.EnsureCanAttachToTimetableEntry());
        Assert.False(tg.CanAttachToTimetableEntry);
    }

    [Fact]
    public void Multiple_TeachingGroups_may_share_SubjectAllocation()
    {
        var lecture = NewGroup(TeachingGroupType.SectionDerived, TeachingGroupMembershipSource.Section, name: "CA-A Lecture");
        var lab = NewGroup(TeachingGroupType.Laboratory, TeachingGroupMembershipSource.Hybrid, name: "CA-Lab-01");
        lab.ExclusionGroupKey = "CA-LAB-2026";
        lab.ActivityKind = TeachingGroupActivityKind.Laboratory;
        Assert.Equal(lecture.SubjectAllocationId, lab.SubjectAllocationId);
    }

    [Fact]
    public void One_TeachingGroup_can_support_multiple_TimetableEntries_conceptually()
    {
        var tg = NewGroup(TeachingGroupType.SectionDerived, TeachingGroupMembershipSource.Section);
        tg.TransitionTo(TeachingGroupStatus.Active);
        tg.EnsureCanAttachToTimetableEntry();
        tg.EnsureCanAttachToTimetableEntry();
        Assert.True(tg.CanAttachToTimetableEntry);
    }

    [Fact]
    public void Student_membership_cannot_cross_tenant_boundaries()
    {
        Assert.Throws<InvalidOperationException>(() =>
            TeachingGroupRules.EnsureSameTenant(1, 2, "Student"));
        TeachingGroupRules.EnsureSameTenant(1, 1, "Student");
    }

    private static TeachingGroup NewGroup(
        TeachingGroupType type,
        TeachingGroupMembershipSource source,
        string name = "TG-1")
        => new()
        {
            TenantId = 1,
            AcademicYearId = 1,
            CourseId = 1,
            GroupId = 2,
            SemesterId = 3,
            SubjectId = 10,
            SubjectAllocationId = 50,
            Type = type,
            MembershipSource = source,
            Status = TeachingGroupStatus.Draft,
            Name = name,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedDate = DateTime.UtcNow,
        };
}
