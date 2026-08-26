using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3K-B — controlled HISTORICAL_ARCHIVE execution.
/// </summary>
public interface IHistoricalSemesterDispositionExecutionService
{
    Task<HistoricalSemesterDispositionExecuteResultDto> ExecuteAsync(
        HistoricalSemesterDispositionExecuteRequest request,
        CancellationToken cancellationToken = default);
}
