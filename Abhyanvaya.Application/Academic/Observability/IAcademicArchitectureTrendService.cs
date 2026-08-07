namespace Abhyanvaya.Application.Academic.Observability;

public interface IAcademicArchitectureTrendService
{
    /// <summary>Evaluates guard and enqueues persistence (non-blocking for callers when fire-and-forget).</summary>
    Task<ArchitectureTrendReportDto> CaptureAsync(CancellationToken cancellationToken = default);
    Task<ArchitectureTrendReportDto> GetReportAsync(int take = 30, CancellationToken cancellationToken = default);
}
