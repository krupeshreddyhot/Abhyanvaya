using System.Collections.Concurrent;
using System.Diagnostics;
using Abhyanvaya.Application.AIOperations;
using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Operations;

public sealed class AITelemetryService : IAITelemetryService
{
    private readonly ConcurrentDictionary<string, long> _durationTicks = new();
    private readonly ILogger<AITelemetryService> _logger;

    public AITelemetryService(ILogger<AITelemetryService> logger)
    {
        _logger = logger;
    }

    public void RecordDuration(string metricName, TimeSpan duration)
    {
        _durationTicks.AddOrUpdate(metricName, duration.Ticks, (_, existing) => (existing + duration.Ticks) / 2);
        _logger.LogInformation("Telemetry recorded {Metric} durationMs={DurationMs}", metricName, duration.TotalMilliseconds);
    }

    public Task<TelemetrySnapshot> CollectSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var process = Process.GetCurrentProcess();
        var snapshot = new TelemetrySnapshot
        {
            RecognitionDuration = GetAverageDuration("recognition.duration"),
            AttendanceDuration = GetAverageDuration("attendance.duration"),
            EmbeddingDuration = GetAverageDuration("embedding.duration"),
            VectorSearchDuration = GetAverageDuration("vectorsearch.duration"),
            ModelLoadTime = GetAverageDuration("model.load"),
            QueueLength = 0,
            WorkerLoadPercent = 0,
            MemoryBytes = process.WorkingSet64,
            CpuPercent = 0,
            GpuPercent = null,
        };

        return Task.FromResult(snapshot);
    }

    private TimeSpan GetAverageDuration(string key)
    {
        return _durationTicks.TryGetValue(key, out var ticks)
            ? TimeSpan.FromTicks(ticks)
            : TimeSpan.Zero;
    }
}

public sealed class AIMetricsCollector : IAIMetricsCollector
{
    private readonly ConcurrentDictionary<string, long> _counters = new();
    private readonly IApplicationDbContext _context;
    private readonly ILogger<AIMetricsCollector> _logger;

    public AIMetricsCollector(IApplicationDbContext context, ILogger<AIMetricsCollector> logger)
    {
        _context = context;
        _logger = logger;
    }

    public void Increment(string metricName, long delta = 1)
    {
        _counters.AddOrUpdate(metricName, delta, (_, existing) => existing + delta);
    }

    public async Task<OperationalMetricsSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        var recognitionCount = await _context.AttendanceRecognitions
            .AsNoTracking()
            .LongCountAsync(cancellationToken);

        var sessionCount = await _context.AttendanceSessions
            .AsNoTracking()
            .LongCountAsync(cancellationToken);

        var failures = _counters.GetValueOrDefault("failures");
        var retries = _counters.GetValueOrDefault("retries");
        var unknownFaces = _counters.GetValueOrDefault("unknown.faces");
        var manualReviews = _counters.GetValueOrDefault("manual.reviews");

        var process = Process.GetCurrentProcess();
        _logger.LogInformation(
            "Metrics collected recognition={Recognition} sessions={Sessions}",
            recognitionCount,
            sessionCount);

        return new OperationalMetricsSnapshot
        {
            RecognitionRequests = recognitionCount,
            AttendanceSessions = sessionCount,
            Failures = failures,
            Retries = retries,
            UnknownFaces = unknownFaces,
            ManualReviews = manualReviews,
            AverageLatency = TimeSpan.FromMilliseconds(_counters.GetValueOrDefault("latency.ms")),
            ThroughputPerMinute = (int)_counters.GetValueOrDefault("throughput.perminute"),
            CpuPercent = 0,
            MemoryBytes = process.WorkingSet64,
            QueueDepth = (int)_counters.GetValueOrDefault("queue.depth"),
            AverageDatabaseTime = TimeSpan.FromMilliseconds(_counters.GetValueOrDefault("database.ms")),
        };
    }
}

public sealed class AITracingService : IAITracingService
{
    private readonly ConcurrentDictionary<Guid, List<AISpan>> _spans = new();
    private readonly ILogger<AITracingService> _logger;

    public AITracingService(ILogger<AITracingService> logger)
    {
        _logger = logger;
    }

    public AITraceContext CreateContext(Guid? correlationId = null, int? tenantId = null, string? pipelineId = null)
    {
        var traceId = Guid.NewGuid();
        var spanId = Guid.NewGuid();
        return new AITraceContext
        {
            TraceId = traceId,
            CorrelationId = correlationId ?? Guid.NewGuid(),
            TenantId = tenantId,
            PipelineId = pipelineId,
            CurrentSpanId = spanId,
        };
    }

    public AITraceContext StartSpan(AITraceContext parent, string operationName, string component)
    {
        var spanId = Guid.NewGuid();
        var span = new AISpan
        {
            SpanId = spanId,
            TraceId = parent.TraceId,
            OperationName = operationName,
            Component = component,
            StartedUtc = DateTime.UtcNow,
            ParentSpanId = parent.CurrentSpanId,
            Success = true,
        };

        _spans.AddOrUpdate(
            parent.TraceId,
            _ => new List<AISpan> { span },
            (_, list) =>
            {
                lock (list)
                {
                    list.Add(span);
                }

                return list;
            });

        _logger.LogInformation(
            "Trace span started traceId={TraceId} spanId={SpanId} operation={Operation}",
            parent.TraceId,
            spanId,
            operationName);

        return parent with { ParentSpanId = parent.CurrentSpanId, CurrentSpanId = spanId };
    }

    public void EndSpan(AISpan span, bool success)
    {
        var ended = DateTime.UtcNow;
        var updated = span with
        {
            EndedUtc = ended,
            Duration = ended - span.StartedUtc,
            Success = success,
        };

        if (_spans.TryGetValue(span.TraceId, out var list))
        {
            lock (list)
            {
                var index = list.FindIndex(s => s.SpanId == span.SpanId);
                if (index >= 0)
                {
                    list[index] = updated;
                }
            }
        }
    }

    public IReadOnlyList<AISpan> GetActiveSpans(Guid traceId)
    {
        if (!_spans.TryGetValue(traceId, out var list))
        {
            return Array.Empty<AISpan>();
        }

        lock (list)
        {
            return list.ToList();
        }
    }
}
