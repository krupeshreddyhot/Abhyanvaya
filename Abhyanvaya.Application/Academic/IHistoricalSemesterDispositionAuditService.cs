using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3K-A — read-only historical disposition discovery audit.
/// </summary>
public interface IHistoricalSemesterDispositionAuditService
{
    Task<HistoricalSemesterDispositionAuditDto> BuildAuditAsync(CancellationToken cancellationToken = default);
}
