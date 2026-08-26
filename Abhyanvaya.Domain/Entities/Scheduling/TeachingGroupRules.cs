using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Domain.Exceptions;

namespace Abhyanvaya.Domain.Entities.Scheduling;

/// <summary>
/// AI-SCHED-TG.3 — Pure domain invariants for TeachingGroup (no DbContext).
/// </summary>
public static class TeachingGroupRules
{
    /// <summary>
    /// AI-SCHED-TG.4 Prompt 4 — User-facing message when a mutation would persist an incompatible TeachingGroup.
    /// Does not instruct silent clear/replace; caller must resolve explicitly.
    /// </summary>
    public const string TimetableEntryTeachingGroupIncompatibleMessage =
        "The selected Teaching Group is not compatible with the timetable entry's current academic/subject allocation. Select a compatible Teaching Group or clear the Teaching Group assignment before saving.";

    public static void ValidateSectionLinks(TeachingGroupType type, IReadOnlyCollection<int> sectionIds)
    {
        var distinct = sectionIds.Where(id => id > 0).Distinct().ToList();
        switch (type)
        {
            case TeachingGroupType.SectionDerived:
                if (distinct.Count != 1)
                    throw new InvalidOperationException("SectionDerived TeachingGroup requires exactly one Section.");
                break;
            case TeachingGroupType.CombinedSections:
                if (distinct.Count < 2)
                    throw new InvalidOperationException("CombinedSections TeachingGroup requires two or more Sections.");
                break;
            case TeachingGroupType.Elective:
                if (distinct.Count > 0)
                    throw new InvalidOperationException("Elective TeachingGroup must not require Section links.");
                break;
            case TeachingGroupType.StudentSubset:
            case TeachingGroupType.Laboratory:
            case TeachingGroupType.CapacitySplit:
            case TeachingGroupType.Custom:
                // Optional parent section(s) — any count including zero is allowed.
                break;
            default:
                throw new InvalidOperationException($"Unknown TeachingGroupType: {type}.");
        }
    }

    public static void ValidateCapacitySplitExclusionKey(TeachingGroupType type, string? exclusionGroupKey)
    {
        if (type == TeachingGroupType.CapacitySplit && string.IsNullOrWhiteSpace(exclusionGroupKey))
            throw new InvalidOperationException("CapacitySplit TeachingGroup requires ExclusionGroupKey.");
    }

    /// <summary>
    /// Mutual exclusion: same student cannot be in two groups that share Tenant + SubjectAllocation + ExclusionGroupKey
    /// when ExclusionGroupKey is non-null. Lecture (null key) + Lab (key) remains compatible.
    /// </summary>
    public static void EnsureStudentNotInMutuallyExclusiveGroup(
        int tenantId,
        int subjectAllocationId,
        int studentId,
        string? exclusionGroupKey,
        int? excludeTeachingGroupId,
        IEnumerable<(int TeachingGroupId, int TenantId, int SubjectAllocationId, string? ExclusionGroupKey, TeachingGroupStatus Status, IReadOnlyCollection<int> MemberStudentIds)> peers)
    {
        if (string.IsNullOrWhiteSpace(exclusionGroupKey))
            return;

        foreach (var peer in peers)
        {
            if (excludeTeachingGroupId is int self && peer.TeachingGroupId == self)
                continue;
            if (peer.TenantId != tenantId)
                continue;
            if (peer.SubjectAllocationId != subjectAllocationId)
                continue;
            if (peer.Status is TeachingGroupStatus.Archived)
                continue;
            if (!string.Equals(peer.ExclusionGroupKey, exclusionGroupKey, StringComparison.Ordinal))
                continue;
            if (peer.MemberStudentIds.Contains(studentId))
            {
                throw new InvalidOperationException(
                    $"Student {studentId} already belongs to mutually-exclusive TeachingGroup {peer.TeachingGroupId} " +
                    $"under ExclusionGroupKey '{exclusionGroupKey}'.");
            }
        }
    }

    public static void EnsureSameTenant(int teachingGroupTenantId, int relatedTenantId, string relatedName)
    {
        if (teachingGroupTenantId != relatedTenantId)
            throw new InvalidOperationException($"{relatedName} tenant does not match TeachingGroup tenant.");
    }

    /// <summary>
    /// AI-SCHED-TG.4 — When a TimetableEntry references a TeachingGroup, tenants must match.
    /// PostgreSQL FK cannot enforce TenantId equality; application boundary must validate.
    /// Does not resolve/create TeachingGroups from SubjectAllocation.
    /// </summary>
    public static void EnsureTimetableEntryTeachingGroupTenant(
        int timetableEntryTenantId,
        int? teachingGroupId,
        int? teachingGroupTenantId)
    {
        if (teachingGroupId is null)
            return;
        if (teachingGroupTenantId is null)
            throw new InvalidOperationException("TeachingGroup tenant could not be resolved for TimetableEntry association.");
        EnsureSameTenant(teachingGroupTenantId.Value, timetableEntryTenantId, "TimetableEntry");
    }

    /// <summary>
    /// AI-SCHED-TG.4 — Single authoritative compatibility check for TimetableEntry ↔ TeachingGroup.
    /// Used on explicit assignment and on any mutation that could leave TeachingGroupId attached
    /// to a changed SubjectAllocation / academic scope. Does not select or create TeachingGroups.
    /// AcademicYear lives on TeachingGroup/SubjectAllocation (not TimetableEntry); matching
    /// SubjectAllocationId is the SA identity contract. College is not modeled on these entities.
    /// </summary>
    public static void EnsureCompatibleWithTimetableEntry(TeachingGroup teachingGroup, TimetableEntry entry)
    {
        ArgumentNullException.ThrowIfNull(teachingGroup);
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            EnsureTimetableEntryTeachingGroupTenant(entry.TenantId, teachingGroup.Id, teachingGroup.TenantId);
        }
        catch (InvalidOperationException)
        {
            throw new DomainException(TimetableEntryTeachingGroupIncompatibleMessage);
        }

        try
        {
            teachingGroup.EnsureCanAttachToTimetableEntry();
        }
        catch (InvalidOperationException ex)
        {
            throw new DomainException(ex.Message, ex);
        }

        if (teachingGroup.SubjectAllocationId != entry.SubjectAllocationId
            || teachingGroup.CourseId != entry.CourseId
            || teachingGroup.GroupId != entry.GroupId
            || teachingGroup.SemesterId != entry.SemesterId
            || teachingGroup.SubjectId != entry.SubjectId)
        {
            throw new DomainException(TimetableEntryTeachingGroupIncompatibleMessage);
        }
    }

    public const string TeachingGroupSectionIncompatibleMessage =
        "The selected Section is not compatible with the Teaching Group's academic scope.";

    /// <summary>
    /// AI-SCHED-TG.4A — Section must share tenant + academic year/course/group/semester with TeachingGroup.
    /// College exists on Section only and is not part of the TeachingGroup scope contract.
    /// </summary>
    public static void EnsureSectionCompatibleWithTeachingGroup(
        TeachingGroup teachingGroup,
        int sectionTenantId,
        int sectionAcademicYearId,
        int sectionCourseId,
        int sectionGroupId,
        int sectionSemesterId)
    {
        ArgumentNullException.ThrowIfNull(teachingGroup);

        if (sectionTenantId != teachingGroup.TenantId)
            throw new DomainException(TeachingGroupSectionIncompatibleMessage);

        if (sectionAcademicYearId != teachingGroup.AcademicYearId
            || sectionCourseId != teachingGroup.CourseId
            || sectionGroupId != teachingGroup.GroupId
            || sectionSemesterId != teachingGroup.SemesterId)
        {
            throw new DomainException(TeachingGroupSectionIncompatibleMessage);
        }
    }
}
