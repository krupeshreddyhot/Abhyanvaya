using Abhyanvaya.Application.DTOs.Scheduling;

namespace Abhyanvaya.Application.Scheduling;

/// <summary>
/// AI-SCHED-TG.6 Prompt 4 / Prompt 2A — Read-only compatible Teaching Group query for a TimetableEntry.
/// Server-authoritative; never creates/assigns/clears Teaching Groups; never writes TimetableSection.
/// </summary>
public interface ICompatibleTeachingGroupQueryService
{
    Task<IReadOnlyList<CompatibleTeachingGroupOptionDto>> GetCompatibleTeachingGroupsForTimetableEntryAsync(
        int timetableEntryId,
        CancellationToken cancellationToken = default);
}
