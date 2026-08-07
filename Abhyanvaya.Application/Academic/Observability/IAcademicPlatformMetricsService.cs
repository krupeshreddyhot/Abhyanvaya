namespace Abhyanvaya.Application.Academic.Observability;

public interface IAcademicPlatformMetricsService
{
    Task<AcademicPlatformMetricsDto> GetMetricsAsync(CancellationToken cancellationToken = default);
    AcademicPerformanceReportDto GetPerformanceReport();
}
