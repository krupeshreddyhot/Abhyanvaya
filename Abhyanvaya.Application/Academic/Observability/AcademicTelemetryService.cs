using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Application.Academic.Observability;

/// <summary>
/// Vendor-neutral academic telemetry. Uses platform <see cref="IAITelemetryService"/> + <see cref="ActivitySource"/>.
/// No DataDog/OpenTelemetry APIs in Application layer.
/// </summary>
public sealed class AcademicTelemetryService : IAcademicTelemetryService
{
    public static readonly ActivitySource ActivitySource = new("Abhyanvaya.Academic");

    private readonly IAITelemetryService _platformTelemetry;
    private readonly IAITracingService _tracing;
    private readonly IAIMetricsCollector _metrics;
    private readonly AcademicMetricsStore _store;
    private readonly AcademicPlatformOptions _options;
    private readonly ILogger<AcademicTelemetryService> _logger;

    public AcademicTelemetryService(
        IAITelemetryService platformTelemetry,
        IAITracingService tracing,
        IAIMetricsCollector metrics,
        AcademicMetricsStore store,
        IOptions<AcademicPlatformOptions> options,
        ILogger<AcademicTelemetryService> logger)
    {
        _platformTelemetry = platformTelemetry;
        _tracing = tracing;
        _metrics = metrics;
        _store = store;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<T> TrackAsync<T>(
        string operationName,
        string spanName,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableTelemetry && !_options.EnablePerformanceMetrics)
            return await action(cancellationToken);

        using var activity = _options.EnableTelemetry ? ActivitySource.StartActivity(spanName) : null;
        var sw = Stopwatch.StartNew();
        var ctx = _options.EnableTelemetry
            ? _tracing.CreateContext()
            : null;
        if (ctx is not null)
            _tracing.StartSpan(ctx, spanName, "Academic");

        try
        {
            var result = await action(cancellationToken);
            sw.Stop();
            RecordSuccess(operationName, spanName, sw.Elapsed);
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            RecordFailure(operationName, spanName, sw.Elapsed, ex);
            throw;
        }
    }

    public Task TrackAsync(
        string operationName,
        string spanName,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
        => TrackAsync(operationName, spanName, async ct =>
        {
            await action(ct);
            return true;
        }, cancellationToken);

    public void RecordCacheHit(string cacheKind)
    {
        if (!_options.EnablePerformanceMetrics) return;
        _store.Increment($"cache.{cacheKind}.hit");
        _metrics.Increment($"academic.cache.{cacheKind}.hit");
    }

    public void RecordCacheMiss(string cacheKind)
    {
        if (!_options.EnablePerformanceMetrics) return;
        _store.Increment($"cache.{cacheKind}.miss");
        _metrics.Increment($"academic.cache.{cacheKind}.miss");
    }

    public void RecordDuration(string metricName, TimeSpan duration)
    {
        if (!_options.EnableTelemetry && !_options.EnablePerformanceMetrics) return;
        _platformTelemetry.RecordDuration(metricName, duration);
        _store.RecordDurationMs(metricName, duration.TotalMilliseconds);
    }

    private void RecordSuccess(string operationName, string spanName, TimeSpan elapsed)
    {
        if (_options.EnablePerformanceMetrics)
        {
            _store.RecordDurationMs(operationName, elapsed.TotalMilliseconds);
            _store.Increment($"{operationName}.count");
        }
        if (_options.EnableTelemetry)
        {
            _platformTelemetry.RecordDuration($"academic.{operationName}", elapsed);
            _metrics.Increment($"academic.{operationName}.success");
        }
        _logger.LogInformation(
            "Academic telemetry success Operation={Operation} Span={Span} DurationMs={DurationMs}",
            operationName, spanName, elapsed.TotalMilliseconds);
    }

    private void RecordFailure(string operationName, string spanName, TimeSpan elapsed, Exception ex)
    {
        if (_options.EnablePerformanceMetrics)
        {
            _store.RecordDurationMs(operationName, elapsed.TotalMilliseconds);
            _store.Increment($"{operationName}.failures");
        }
        if (_options.EnableTelemetry)
        {
            _platformTelemetry.RecordDuration($"academic.{operationName}.failure", elapsed);
            _metrics.Increment($"academic.{operationName}.failure");
        }
        _logger.LogWarning(
            ex,
            "Academic telemetry failure Operation={Operation} Span={Span} DurationMs={DurationMs}",
            operationName, spanName, elapsed.TotalMilliseconds);
    }
}
