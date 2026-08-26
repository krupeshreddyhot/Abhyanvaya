using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Domain.Entities.Scheduling;

/// <summary>
/// AI-SCHED-TG.3 — Operational teaching cohort under a SubjectAllocation.
/// Not an academic <c>Section</c>. One SubjectAllocation may own many TeachingGroups.
/// </summary>
public class TeachingGroup : BaseEntity
{
    public int AcademicYearId { get; set; }
    public int CourseId { get; set; }
    /// <summary>Curriculum specialization Group (not TeachingGroup).</summary>
    public int GroupId { get; set; }
    public int SemesterId { get; set; }
    public int SubjectId { get; set; }
    public int SubjectAllocationId { get; set; }
    public SubjectAllocation? SubjectAllocation { get; set; }

    /// <summary>
    /// Authoritative Section links are only via <see cref="Sections"/> (TeachingGroupSection).
    /// Academic SectionGroup remains a separate AI29 construct and is not linked here.
    /// </summary>
    public TeachingGroupType Type { get; set; }
    public TeachingGroupMembershipSource MembershipSource { get; set; }
    public TeachingGroupStatus Status { get; set; } = TeachingGroupStatus.Draft;
    public TeachingGroupActivityKind ActivityKind { get; set; } = TeachingGroupActivityKind.Lecture;

    public string? Code { get; set; }
    public string Name { get; set; } = null!;
    public int DisplayOrder { get; set; }

    /// <summary>Optional planning intent — not ResolvedStudentCount.</summary>
    public int? ExpectedStudentCount { get; set; }

    /// <summary>Optional operational teaching ceiling — not Room.Capacity.</summary>
    public int? MaxTeachingCapacity { get; set; }

    /// <summary>
    /// When non-null, a student may belong to at most one Active/Locked TeachingGroup
    /// sharing the same (TenantId, SubjectAllocationId, ExclusionGroupKey).
    /// </summary>
    public string? ExclusionGroupKey { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string? Notes { get; set; }

    public ICollection<TeachingGroupSection> Sections { get; set; } = new List<TeachingGroupSection>();
    public ICollection<TeachingGroupMembership> Memberships { get; set; } = new List<TeachingGroupMembership>();

    public bool IsMutable =>
        Status is TeachingGroupStatus.Draft or TeachingGroupStatus.Active;

    public bool CanAttachToTimetableEntry =>
        Status is TeachingGroupStatus.Draft or TeachingGroupStatus.Active or TeachingGroupStatus.Locked
        && !IsDeleted;

    public void EnsureCanMutate()
    {
        if (IsDeleted)
            throw new InvalidOperationException("Deleted TeachingGroup cannot be mutated.");
        if (Status == TeachingGroupStatus.Locked)
            throw new InvalidOperationException("Locked TeachingGroup membership/sections cannot be changed.");
        if (Status == TeachingGroupStatus.Archived)
            throw new InvalidOperationException("Archived TeachingGroup cannot be mutated.");
    }

    public void EnsureCanAttachToTimetableEntry()
    {
        if (IsDeleted)
            throw new InvalidOperationException("Deleted TeachingGroup cannot be attached to a TimetableEntry.");
        if (Status == TeachingGroupStatus.Archived)
            throw new InvalidOperationException("Archived TeachingGroup cannot be attached to a TimetableEntry.");
    }

    public void TransitionTo(TeachingGroupStatus next)
    {
        if (IsDeleted)
            throw new InvalidOperationException("Deleted TeachingGroup cannot change status.");

        var allowed = (Status, next) switch
        {
            (TeachingGroupStatus.Draft, TeachingGroupStatus.Active) => true,
            (TeachingGroupStatus.Draft, TeachingGroupStatus.Archived) => true,
            (TeachingGroupStatus.Active, TeachingGroupStatus.Locked) => true,
            (TeachingGroupStatus.Active, TeachingGroupStatus.Archived) => true,
            (TeachingGroupStatus.Active, TeachingGroupStatus.Draft) => true,
            (TeachingGroupStatus.Locked, TeachingGroupStatus.Active) => true, // unlock via application/governance
            (TeachingGroupStatus.Locked, TeachingGroupStatus.Archived) => true,
            _ when Status == next => true,
            _ => false,
        };

        if (!allowed)
            throw new InvalidOperationException($"Cannot transition TeachingGroup from {Status} to {next}.");

        Status = next;
        UpdatedDate = DateTime.UtcNow;
    }

    public void SetCapacity(int? expectedStudentCount, int? maxTeachingCapacity)
    {
        EnsureCanMutate();

        // null = not configured; 0+ = explicit Expected; Max 0 is invalid (use null for unset).
        if (expectedStudentCount is < 0)
            throw new InvalidOperationException("ExpectedStudentCount cannot be negative.");
        if (maxTeachingCapacity is int maxCap && maxCap <= 0)
            throw new InvalidOperationException("MaxTeachingCapacity must be a positive integer when configured; use null when unset.");
        if (expectedStudentCount is int expected
            && maxTeachingCapacity is int maxConfigured
            && expected > maxConfigured)
            throw new InvalidOperationException("ExpectedStudentCount cannot exceed MaxTeachingCapacity.");

        ExpectedStudentCount = expectedStudentCount;
        MaxTeachingCapacity = maxTeachingCapacity;
        UpdatedDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Derived headcount from provided operational student ids — never a persisted field.
    /// Does not query the database.
    /// </summary>
    public static int ComputeResolvedStudentCount(IEnumerable<int> distinctStudentIds)
        => distinctStudentIds.Distinct().Count();

    public void EnsureResolvedWithinMaxCapacity(int resolvedStudentCount)
    {
        // Only enforced when MaxTeachingCapacity is configured (non-null positive).
        if (MaxTeachingCapacity is int max && resolvedStudentCount > max)
            throw new InvalidOperationException(
                $"ResolvedStudentCount ({resolvedStudentCount}) exceeds MaxTeachingCapacity ({max}).");
    }
}
