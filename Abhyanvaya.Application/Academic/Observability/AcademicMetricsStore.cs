using System.Collections.Concurrent;

namespace Abhyanvaya.Application.Academic.Observability;

/// <summary>
/// AI29.1A.7 — In-process metrics store (singleton). Persistence is async/optional; never blocks requests.
/// </summary>
public sealed class AcademicMetricsStore
{
    private const int SampleCapacity = 256;

    private readonly ConcurrentDictionary<string, long> _counters = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentQueue<double>> _samples = new(StringComparer.OrdinalIgnoreCase);
    private int _hierarchySize;
    private int _statisticsCacheSize;

    public void Increment(string key, long delta = 1) =>
        _counters.AddOrUpdate(key, delta, (_, v) => v + delta);

    public long GetCounter(string key) => _counters.GetValueOrDefault(key);

    public void RecordDurationMs(string operation, double milliseconds)
    {
        var queue = _samples.GetOrAdd(operation, _ => new ConcurrentQueue<double>());
        queue.Enqueue(milliseconds);
        while (queue.Count > SampleCapacity && queue.TryDequeue(out _))
        {
        }
    }

    public void SetHierarchySize(int size) => Interlocked.Exchange(ref _hierarchySize, size);
    public void SetStatisticsCacheSize(int size) => Interlocked.Exchange(ref _statisticsCacheSize, size);
    public int HierarchySize => Volatile.Read(ref _hierarchySize);
    public int StatisticsCacheSize => Volatile.Read(ref _statisticsCacheSize);

    public AcademicOperationMetricsDto GetOperationMetrics(string operation, double budgetMs)
    {
        var samples = SnapshotSamples(operation);
        var failures = GetCounter($"{operation}.failures");
        var avg = samples.Count == 0 ? 0 : samples.Average();
        var p95 = Percentile(samples, 0.95);
        var p99 = Percentile(samples, 0.99);
        return new AcademicOperationMetricsDto
        {
            Operation = operation,
            ExecutionCount = samples.Count,
            FailureCount = failures,
            AverageMs = Math.Round(avg, 2),
            P95Ms = Math.Round(p95, 2),
            P99Ms = Math.Round(p99, 2),
            BudgetMs = budgetMs,
            WithinBudget = avg <= budgetMs || samples.Count == 0,
        };
    }

    public AcademicCacheMetricsDto GetCacheMetrics()
    {
        var hHits = GetCounter("cache.hierarchy.hit");
        var hMiss = GetCounter("cache.hierarchy.miss");
        var sHits = GetCounter("cache.statistics.hit");
        var sMiss = GetCounter("cache.statistics.miss");
        var hTotal = hHits + hMiss;
        var sTotal = sHits + sMiss;
        var hSamples = SnapshotSamples("cache.hierarchy.retrieval");
        var sSamples = SnapshotSamples("cache.statistics.retrieval");
        return new AcademicCacheMetricsDto
        {
            HierarchyHits = hHits,
            HierarchyMisses = hMiss,
            HierarchyHitRatePercent = hTotal == 0 ? 0 : Math.Round(100.0 * hHits / hTotal, 2),
            StatisticsHits = sHits,
            StatisticsMisses = sMiss,
            StatisticsHitRatePercent = sTotal == 0 ? 0 : Math.Round(100.0 * sHits / sTotal, 2),
            RefreshCount = GetCounter("cache.refresh"),
            WarmCount = GetCounter("cache.warm"),
            InvalidateCount = GetCounter("cache.invalidate"),
            AverageHierarchyRetrievalMs = hSamples.Count == 0 ? 0 : Math.Round(hSamples.Average(), 2),
            AverageStatisticsRetrievalMs = sSamples.Count == 0 ? 0 : Math.Round(sSamples.Average(), 2),
        };
    }

    public IReadOnlyList<AcademicDomainEventMetricsDto> GetDomainEventMetrics()
    {
        var names = new[]
        {
            "ProgramCreated", "ProgramUpdated", "ProgramArchived", "CourseAssigned", "CourseRemoved"
        };
        return names.Select(n =>
        {
            var samples = SnapshotSamples($"event.{n}.processing");
            return new AcademicDomainEventMetricsDto
            {
                EventName = n,
                Published = GetCounter($"event.{n}.published"),
                Succeeded = GetCounter($"event.{n}.succeeded"),
                Failed = GetCounter($"event.{n}.failed"),
                AverageProcessingMs = samples.Count == 0 ? 0 : Math.Round(samples.Average(), 2),
            };
        }).ToList();
    }

    private List<double> SnapshotSamples(string operation)
    {
        if (!_samples.TryGetValue(operation, out var queue))
            return [];
        return queue.ToArray().ToList();
    }

    private static double Percentile(IReadOnlyList<double> samples, double p)
    {
        if (samples.Count == 0) return 0;
        var ordered = samples.OrderBy(x => x).ToArray();
        var index = (int)Math.Ceiling(p * ordered.Length) - 1;
        index = Math.Clamp(index, 0, ordered.Length - 1);
        return ordered[index];
    }
}
