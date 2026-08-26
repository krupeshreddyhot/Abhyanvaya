using Abhyanvaya.Application.DTOs.Scheduling;

namespace Abhyanvaya.Application.Scheduling;

/// <summary>
/// AI-SCHED-TG.4 Prompt 3 — Application boundary for explicit TimetableEntry ↔ TeachingGroup assignment.
/// Does not infer TeachingGroups from SubjectAllocation/Section and does not auto-create them.
/// </summary>
public interface ITeachingGroupApplicationService
{
    Task<TimetableEntryDto> AssignToTimetableEntryAsync(
        int timetableEntryId,
        int teachingGroupId,
        CancellationToken cancellationToken = default);

    Task<TimetableEntryDto> ClearFromTimetableEntryAsync(
        int timetableEntryId,
        CancellationToken cancellationToken = default);
}
