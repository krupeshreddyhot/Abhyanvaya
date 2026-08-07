using Microsoft.Extensions.Options;

namespace Abhyanvaya.Application.Academic.Observability;

public sealed class AcademicDomainEventMetrics : IAcademicDomainEventMetrics
{
    private readonly AcademicMetricsStore _store;
    private readonly AcademicPlatformOptions _options;

    public AcademicDomainEventMetrics(AcademicMetricsStore store, IOptions<AcademicPlatformOptions> options)
    {
        _store = store;
        _options = options.Value;
    }

    public void RecordPublished(string eventName)
    {
        if (!_options.EnablePerformanceMetrics) return;
        _store.Increment($"event.{eventName}.published");
    }

    public void RecordSucceeded(string eventName, TimeSpan processing)
    {
        if (!_options.EnablePerformanceMetrics) return;
        _store.Increment($"event.{eventName}.succeeded");
        _store.RecordDurationMs($"event.{eventName}.processing", processing.TotalMilliseconds);
    }

    public void RecordFailed(string eventName, TimeSpan processing)
    {
        if (!_options.EnablePerformanceMetrics) return;
        _store.Increment($"event.{eventName}.failed");
        _store.RecordDurationMs($"event.{eventName}.processing", processing.TotalMilliseconds);
    }

    public IReadOnlyList<AcademicDomainEventMetricsDto> GetMetrics() => _store.GetDomainEventMetrics();
}
