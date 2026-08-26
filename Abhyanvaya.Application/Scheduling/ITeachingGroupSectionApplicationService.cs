using Abhyanvaya.Application.DTOs.Scheduling;

namespace Abhyanvaya.Application.Scheduling;

/// <summary>
/// AI-SCHED-TG.4A Prompt 3 — Application boundary for TeachingGroupSection (source of truth).
/// Does not create/infer TeachingGroups. TimetableSection projection is Prompt 4.
/// </summary>
public interface ITeachingGroupSectionApplicationService
{
    Task<IReadOnlyList<TeachingGroupSectionDto>> GetSectionsAsync(
        int teachingGroupId,
        CancellationToken cancellationToken = default);

    /// <summary>Replace the full TeachingGroupSection set with the supplied SectionIds (0..N per type rules).</summary>
    Task<IReadOnlyList<TeachingGroupSectionDto>> ReplaceSectionsAsync(
        int teachingGroupId,
        IReadOnlyList<int> sectionIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// AI-SCHED-TG.4A Prompt 4 — Replace TeachingGroupSection then project TimetableSection for all
    /// entries bound to the TeachingGroup, then <b>SaveChanges once</b>.
    /// Used by the future legacy /sections bridge so Attendance never sees SoT without matching projection.
    /// </summary>
    Task<IReadOnlyList<TeachingGroupSectionDto>> ReplaceSectionsAndProjectAsync(
        int teachingGroupId,
        IReadOnlyList<int> sectionIds,
        CancellationToken cancellationToken = default);

    Task<TeachingGroupSectionDto> AddSectionAsync(
        int teachingGroupId,
        int sectionId,
        bool isPrimary = false,
        CancellationToken cancellationToken = default);

    Task RemoveSectionAsync(
        int teachingGroupId,
        int sectionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// AI-SCHED-TG.5 Prompt 2 — Add one TeachingGroupSection then project TimetableSection (single SaveChanges).
    /// HTTP section POST must use this path, not direct TimetableSection writes.
    /// </summary>
    Task<TeachingGroupSectionDto> AddSectionAndProjectAsync(
        int teachingGroupId,
        int sectionId,
        bool isPrimary = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// AI-SCHED-TG.5 Prompt 2 — Remove one TeachingGroupSection then project TimetableSection (single SaveChanges).
    /// </summary>
    Task RemoveSectionAndProjectAsync(
        int teachingGroupId,
        int sectionId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// AI-SCHED-TG.4A Prompt 4 — Sole approved writer of TimetableSection from TeachingGroupSection.
/// Mutations are staged on the shared DbContext; <b>does not call SaveChanges</b> — the parent
/// application operation commits once (SoT + projection).
/// </summary>
public interface ITimetableSectionProjector
{
    /// <summary>
    /// Synchronize TimetableSection for every tenant TimetableEntry with the given TeachingGroupId
    /// to match <paramref name="canonicalSectionIds"/> (TeachingGroupSection SoT).
    /// When <paramref name="canonicalSectionIds"/> is null, loads active TeachingGroupSection ids.
    /// </summary>
    Task SyncTeachingGroupSectionsToTimetableEntriesAsync(
        int teachingGroupId,
        IReadOnlyList<int>? canonicalSectionIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronize TimetableSection for one TimetableEntry from its assigned TeachingGroup's SoT.
    /// Does not change TeachingGroupId. Does not SaveChanges.
    /// </summary>
    Task SyncTeachingGroupSectionsToTimetableEntryAsync(
        int timetableEntryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// AI-SCHED-TG.6 Final Gate Prompt 21 — Soft-delete all TimetableSection rows for one entry
    /// when TeachingGroupId is cleared. Does not mutate TeachingGroupSection SoT or TeachingGroupId.
    /// Does not SaveChanges.
    /// </summary>
    Task ClearTimetableEntryProjectionAsync(
        int timetableEntryId,
        CancellationToken cancellationToken = default);
}
