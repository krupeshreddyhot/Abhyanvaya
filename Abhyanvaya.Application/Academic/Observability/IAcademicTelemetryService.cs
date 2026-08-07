namespace Abhyanvaya.Application.Academic.Observability;

/// <summary>
/// AI29.1A.7 — Academic telemetry over existing <see cref="Common.Interfaces.IAITelemetryService"/> / tracing.
/// </summary>
public interface IAcademicTelemetryService
{
    Task<T> TrackAsync<T>(
        string operationName,
        string spanName,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default);

    Task TrackAsync(
        string operationName,
        string spanName,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default);

    void RecordCacheHit(string cacheKind);
    void RecordCacheMiss(string cacheKind);
    void RecordDuration(string metricName, TimeSpan duration);
}
