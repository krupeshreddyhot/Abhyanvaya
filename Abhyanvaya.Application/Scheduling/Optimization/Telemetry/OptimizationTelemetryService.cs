using System.Collections.Concurrent;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Application.Scheduling.Optimization.Telemetry;

public interface IOptimizationTelemetryService
{
    void RecordSimulation(long executionTimeMs, long scoringTimeMs, decimal improvementDelta);
    void RecordRejected();
    void RecordAccepted();
    void RecordMetricUsage(string metricName);
    Task<OptimizationTelemetryDto> GetSnapshotAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Optimization telemetry (no PII). Reuses platform duration recording via <see cref="IAITelemetryService"/>.
/// </summary>
public sealed class OptimizationTelemetryService : IOptimizationTelemetryService
{
    public const string SimulationCountKey = "optimization.simulation.count";
    public const string RejectedKey = "optimization.simulation.rejected";
    public const string AcceptedKey = "optimization.simulation.accepted";
    public const string ExecutionMsKey = "optimization.execution.ms";
    public const string ScoringMsKey = "optimization.scoring.ms";
    public const string ImprovementKey = "optimization.improvement.avg";

    private readonly ConcurrentDictionary<string, long> _counters = new();
    private readonly ConcurrentDictionary<string, long> _metricUsage = new();
    private readonly IAITelemetryService _platformTelemetry;
    private readonly IApplicationDbContext _db;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<OptimizationTelemetryService> _logger;

    public OptimizationTelemetryService(
        IAITelemetryService platformTelemetry,
        IApplicationDbContext db,
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        ILogger<OptimizationTelemetryService> logger)
    {
        _platformTelemetry = platformTelemetry;
        _db = db;
        _uow = uow;
        _currentUser = currentUser;
        _logger = logger;
    }

    public void RecordSimulation(long executionTimeMs, long scoringTimeMs, decimal improvementDelta)
    {
        _counters.AddOrUpdate(SimulationCountKey, 1, (_, v) => v + 1);
        _counters.AddOrUpdate(ExecutionMsKey, executionTimeMs, (_, v) => (v + executionTimeMs) / 2);
        _counters.AddOrUpdate(ScoringMsKey, scoringTimeMs, (_, v) => (v + scoringTimeMs) / 2);
        var improvementTicks = (long)(improvementDelta * 1000);
        _counters.AddOrUpdate(ImprovementKey, improvementTicks, (_, v) => (v + improvementTicks) / 2);
        _platformTelemetry.RecordDuration("optimization.execution.duration", TimeSpan.FromMilliseconds(executionTimeMs));
        _platformTelemetry.RecordDuration("optimization.scoring.duration", TimeSpan.FromMilliseconds(scoringTimeMs));
        _logger.LogInformation(
            "Optimization telemetry simulation tenant={TenantId} execMs={Exec} scoreMs={Score} improvement={Improvement}",
            _currentUser.TenantId, executionTimeMs, scoringTimeMs, improvementDelta);
    }

    public void RecordRejected() => _counters.AddOrUpdate(RejectedKey, 1, (_, v) => v + 1);

    public void RecordAccepted() => _counters.AddOrUpdate(AcceptedKey, 1, (_, v) => v + 1);

    public void RecordMetricUsage(string metricName) =>
        _metricUsage.AddOrUpdate(metricName, 1, (_, v) => v + 1);

    public async Task<OptimizationTelemetryDto> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await PersistAggregatesAsync(cancellationToken);

        return new OptimizationTelemetryDto
        {
            SimulationCount = _counters.GetValueOrDefault(SimulationCountKey),
            ExecutionTimeMs = _counters.GetValueOrDefault(ExecutionMsKey),
            ScoringTimeMs = _counters.GetValueOrDefault(ScoringMsKey),
            AverageImprovement = _counters.GetValueOrDefault(ImprovementKey) / 1000m,
            RejectedSimulations = _counters.GetValueOrDefault(RejectedKey),
            AcceptedSimulations = _counters.GetValueOrDefault(AcceptedKey),
            MostUsedMetrics = _metricUsage
                .OrderByDescending(kv => kv.Value)
                .Take(10)
                .Select(kv => new OptimizationNamedCountDto { Name = kv.Key, Count = (int)kv.Value })
                .ToList()
        };
    }

    private async Task PersistAggregatesAsync(CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId;
        var now = DateTime.UtcNow;
        foreach (var kv in _counters)
        {
            var row = await _db.SchedulingOptimizationTelemetryAggregates
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.MetricKey == kv.Key && !x.IsDeleted, cancellationToken);
            if (row is null)
            {
                await _db.AddAsync(new OptimizationTelemetryAggregate
                {
                    TenantId = tenantId,
                    MetricKey = kv.Key,
                    CounterValue = kv.Value,
                    AverageValue = kv.Value,
                    LastUpdatedUtc = now,
                    CreatedDate = now,
                    CreatedBy = _currentUser.UserId
                });
            }
            else
            {
                row.CounterValue = kv.Value;
                row.AverageValue = kv.Value;
                row.LastUpdatedUtc = now;
                row.UpdatedDate = now;
                row.UpdatedBy = _currentUser.UserId;
            }
        }

        await _uow.SaveChangesAsync(cancellationToken);
    }
}
