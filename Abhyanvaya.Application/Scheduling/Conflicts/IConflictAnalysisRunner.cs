namespace Abhyanvaya.Application.Scheduling.Conflicts;

/// <summary>
/// Read-only conflict analysis runner used by ConflictDetectionService and Publish Readiness.
/// Implementations must not mutate timetable / TG / attendance state.
/// </summary>
public interface IConflictAnalysisRunner
{
    Task<(ConflictAnalysisContext Context, ConflictResultBag Bag)> AnalyzeAsync(
        int tenantId,
        int academicYearId,
        int? timetableId,
        int? departmentId,
        CancellationToken cancellationToken = default);
}
