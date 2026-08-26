using Abhyanvaya.Application.DTOs.Scheduling;

namespace Abhyanvaya.Application.Scheduling;

/// <summary>
/// AI-SCHED-TG.4A Prompt 7 — Explicit disposable conversion of selected TimetableEntries to TeachingGroup.
/// Not a production backfill, hosted job, or startup reconciler.
/// </summary>
public interface ILegacyTimetableTeachingGroupConversionService
{
    /// <summary>
    /// Identify tenant TimetableEntries with <c>TeachingGroupId == null</c> (optional timetable filter).
    /// Read-only; does not convert.
    /// </summary>
    Task<IReadOnlyList<LegacyTimetableEntryWithoutTeachingGroupDto>> ListEntriesWithoutTeachingGroupAsync(
        int? timetableId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convert only the explicitly supplied TimetableEntry → TeachingGroup mappings.
    /// Supports <see cref="ConvertLegacyTimetableEntriesRequest.DryRun"/>.
    /// </summary>
    Task<LegacyTimetableConversionReportDto> ConvertAsync(
        ConvertLegacyTimetableEntriesRequest request,
        CancellationToken cancellationToken = default);
}
