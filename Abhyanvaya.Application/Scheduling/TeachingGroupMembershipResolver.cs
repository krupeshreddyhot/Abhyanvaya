using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling;

/// <summary>
/// AI-SCHED-TG.5 Prompt 5 — Model B resolver:
/// Resolved = (Base ∪ ExplicitIncludes) − ExplicitExcludes for Hybrid;
/// source-specific rules for Explicit / Section / Combined / StudentSubject.
/// Side-effect free.
/// </summary>
public sealed class TeachingGroupMembershipResolver : ITeachingGroupMembershipResolver
{
    private readonly IApplicationDbContext _db;

    public TeachingGroupMembershipResolver(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ResolvedTeachingGroupMemberDto>> ResolveAsync(
        int teachingGroupId,
        CancellationToken cancellationToken = default)
    {
        var tg = await RequireTeachingGroupAsync(teachingGroupId, cancellationToken);
        return await ResolveCoreAsync(tg, cancellationToken);
    }

    public async Task<int> ResolveCountAsync(
        int teachingGroupId,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(teachingGroupId, cancellationToken);
        return resolved.Count;
    }

    /// <summary>Internal: resolve using an already-loaded TeachingGroup (same tenant filter).</summary>
    internal async Task<IReadOnlyList<ResolvedTeachingGroupMemberDto>> ResolveCoreAsync(
        TeachingGroup tg,
        CancellationToken cancellationToken)
    {
        var overlays = await _db.SchedulingTeachingGroupMemberships.AsNoTracking()
            .Where(x => x.TeachingGroupId == tg.Id && x.IsCurrent)
            .Select(x => new { x.StudentId, x.Inclusion })
            .ToListAsync(cancellationToken);

        var includes = overlays
            .Where(x => x.Inclusion == TeachingGroupMembershipInclusion.Include)
            .Select(x => x.StudentId)
            .ToHashSet();
        var excludes = overlays
            .Where(x => x.Inclusion == TeachingGroupMembershipInclusion.Exclude)
            .Select(x => x.StudentId)
            .ToHashSet();

        // Explicit source must not use Exclude overlays in resolution.
        if (tg.MembershipSource == TeachingGroupMembershipSource.ExplicitStudents)
            excludes.Clear();

        var baseIds = await LoadBaseStudentIdsAsync(tg, cancellationToken);
        baseIds = await FilterEligibleStudentsAsync(tg, baseIds, cancellationToken);
        includes = (await FilterEligibleStudentsAsync(tg, includes, cancellationToken)).ToHashSet();

        return ApplyModelB(baseIds, includes, excludes, tg.MembershipSource);
    }

    private static IReadOnlyList<ResolvedTeachingGroupMemberDto> ApplyModelB(
        IReadOnlyCollection<int> baseIds,
        IReadOnlyCollection<int> includes,
        IReadOnlyCollection<int> excludes,
        TeachingGroupMembershipSource source)
    {
        HashSet<int> working;
        Dictionary<int, TeachingGroupMemberProvenance> provenance = new();

        switch (source)
        {
            case TeachingGroupMembershipSource.ExplicitStudents:
                working = includes.ToHashSet();
                foreach (var id in working)
                    provenance[id] = TeachingGroupMemberProvenance.ExplicitInclude;
                break;

            case TeachingGroupMembershipSource.Section:
            case TeachingGroupMembershipSource.CombinedSections:
            case TeachingGroupMembershipSource.StudentSubject:
                working = baseIds.ToHashSet();
                foreach (var id in working)
                    provenance[id] = TeachingGroupMemberProvenance.Derived;
                break;

            case TeachingGroupMembershipSource.Hybrid:
                working = baseIds.ToHashSet();
                foreach (var id in working)
                    provenance[id] = TeachingGroupMemberProvenance.Derived;
                foreach (var id in includes)
                {
                    working.Add(id);
                    // ExplicitInclude wins provenance when also in base (still one student).
                    provenance[id] = TeachingGroupMemberProvenance.ExplicitInclude;
                }

                foreach (var id in excludes)
                {
                    working.Remove(id);
                    provenance.Remove(id);
                }

                break;

            default:
                throw new DomainException("Unknown Teaching Group membership source.");
        }

        // Exclude wins for Hybrid only (already applied). Dynamic sources ignore overlays.
        return working
            .OrderBy(id => id)
            .Select(id => new ResolvedTeachingGroupMemberDto
            {
                StudentId = id,
                Provenance = provenance.GetValueOrDefault(id, TeachingGroupMemberProvenance.Derived),
            })
            .ToList();
    }

    /// <summary>Base population only (no Include/Exclude overlay). Used by mutation capacity checks.</summary>
    internal async Task<HashSet<int>> LoadEligibleBaseStudentIdsAsync(
        TeachingGroup tg,
        CancellationToken cancellationToken)
    {
        var baseIds = await LoadBaseStudentIdsAsync(tg, cancellationToken);
        return await FilterEligibleStudentsAsync(tg, baseIds, cancellationToken);
    }

    private async Task<HashSet<int>> LoadBaseStudentIdsAsync(
        TeachingGroup tg,
        CancellationToken cancellationToken)
    {
        switch (tg.MembershipSource)
        {
            case TeachingGroupMembershipSource.ExplicitStudents:
                return [];

            case TeachingGroupMembershipSource.Section:
            case TeachingGroupMembershipSource.CombinedSections:
                return await LoadSectionDerivedStudentIdsAsync(tg, cancellationToken);

            case TeachingGroupMembershipSource.StudentSubject:
                return await LoadStudentSubjectIdsAsync(tg, cancellationToken);

            case TeachingGroupMembershipSource.Hybrid:
            {
                var sectionIds = await _db.SchedulingTeachingGroupSections.AsNoTracking()
                    .Where(x => x.TeachingGroupId == tg.Id)
                    .Select(x => x.SectionId)
                    .ToListAsync(cancellationToken);
                if (sectionIds.Count > 0)
                    return await LoadSectionDerivedStudentIdsAsync(tg, cancellationToken);
                return await LoadStudentSubjectIdsAsync(tg, cancellationToken);
            }

            default:
                throw new DomainException("Unknown Teaching Group membership source.");
        }
    }

    private async Task<HashSet<int>> LoadSectionDerivedStudentIdsAsync(
        TeachingGroup tg,
        CancellationToken cancellationToken)
    {
        var sectionIds = await _db.SchedulingTeachingGroupSections.AsNoTracking()
            .Where(x => x.TeachingGroupId == tg.Id)
            .Select(x => x.SectionId)
            .ToListAsync(cancellationToken);
        if (sectionIds.Count == 0)
            return [];

        // Section academic year must match TG; StudentSection is read-only source.
        var compatibleSectionIds = await _db.Sections.AsNoTracking()
            .Where(s => sectionIds.Contains(s.Id)
                        && s.AcademicYearId == tg.AcademicYearId
                        && s.CourseId == tg.CourseId
                        && s.GroupId == tg.GroupId
                        && s.SemesterId == tg.SemesterId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
        if (compatibleSectionIds.Count == 0)
            return [];

        var studentIds = await _db.StudentSections.AsNoTracking()
            .Where(ss => compatibleSectionIds.Contains(ss.SectionId) && ss.IsCurrent)
            .Select(ss => ss.StudentId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return studentIds.ToHashSet();
    }

    private async Task<HashSet<int>> LoadStudentSubjectIdsAsync(
        TeachingGroup tg,
        CancellationToken cancellationToken)
    {
        var studentIds = await _db.StudentSubjects.AsNoTracking()
            .Where(ss => ss.SubjectId == tg.SubjectId)
            .Select(ss => ss.StudentId)
            .Distinct()
            .ToListAsync(cancellationToken);
        return studentIds.ToHashSet();
    }

    private async Task<HashSet<int>> FilterEligibleStudentsAsync(
        TeachingGroup tg,
        IReadOnlyCollection<int> studentIds,
        CancellationToken cancellationToken)
    {
        if (studentIds.Count == 0)
            return [];

        // Student has Course/Group/Semester; AcademicYear is enforced via Section for derived paths.
        // Tenant isolation via query filters. College not modeled on TeachingGroup.
        var eligible = await _db.Students.AsNoTracking()
            .Where(s => studentIds.Contains(s.Id)
                        && s.CourseId == tg.CourseId
                        && s.GroupId == tg.GroupId
                        && s.SemesterId == tg.SemesterId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
        return eligible.ToHashSet();
    }

    private async Task<TeachingGroup> RequireTeachingGroupAsync(
        int teachingGroupId,
        CancellationToken cancellationToken)
    {
        if (teachingGroupId <= 0)
            throw new DomainException("A valid Teaching Group must be specified.");

        return await _db.SchedulingTeachingGroups.AsNoTracking()
                   .FirstOrDefaultAsync(x => x.Id == teachingGroupId, cancellationToken)
               ?? throw new KeyNotFoundException("Teaching Group was not found.");
    }
}
