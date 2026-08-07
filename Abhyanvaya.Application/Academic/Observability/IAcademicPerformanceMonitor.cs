namespace Abhyanvaya.Application.Academic.Observability;

public interface IAcademicPerformanceMonitor
{
    AcademicPerformanceReportDto GetReport();
    AcademicOperationMetricsDto GetOperation(string operation);
}
