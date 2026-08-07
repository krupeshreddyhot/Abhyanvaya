namespace Abhyanvaya.Application.Academic.Observability;

public interface IAcademicCacheMetricsService
{
    void RecordHierarchyHit(TimeSpan retrieval);
    void RecordHierarchyMiss(TimeSpan retrieval);
    void RecordStatisticsHit(TimeSpan retrieval);
    void RecordStatisticsMiss(TimeSpan retrieval);
    void RecordWarm();
    void RecordRefresh();
    void RecordInvalidate();
    AcademicCacheMetricsDto GetMetrics();
}
