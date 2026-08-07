namespace Abhyanvaya.Application.Academic.Observability;

public interface IAcademicDomainEventMetrics
{
    void RecordPublished(string eventName);
    void RecordSucceeded(string eventName, TimeSpan processing);
    void RecordFailed(string eventName, TimeSpan processing);
    IReadOnlyList<AcademicDomainEventMetricsDto> GetMetrics();
}
