using Abhyanvaya.Application.AIOperations;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Operations;

public sealed class AIHealthRegistry : IAIHealthRegistry
{
    private readonly object _lock = new();
    private readonly List<IAIHealthCheckProvider> _providers = new();

    public IReadOnlyList<string> RegisteredComponents
    {
        get
        {
            lock (_lock)
            {
                return _providers.Select(p => p.ComponentName).ToList();
            }
        }
    }

    public void Register(IAIHealthCheckProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        lock (_lock)
        {
            if (_providers.Any(p => p.ComponentName == provider.ComponentName))
            {
                return;
            }

            _providers.Add(provider);
        }
    }

    internal IReadOnlyList<IAIHealthCheckProvider> GetProviders()
    {
        lock (_lock)
        {
            return _providers.ToList();
        }
    }
}

public sealed class AIHealthService : IAIHealthService
{
    private readonly AIHealthRegistry _registry;
    private readonly ILogger<AIHealthService> _logger;

    public AIHealthService(AIHealthRegistry registry, ILogger<AIHealthService> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public async Task<AIPlatformHealthReport> GetPlatformHealthAsync(CancellationToken cancellationToken = default)
    {
        var checks = new List<AIHealthCheckResult>();
        foreach (var provider in _registry.GetProviders())
        {
            var result = await provider.CheckHealthAsync(cancellationToken);
            checks.Add(result);
            _logger.LogInformation(
                "Health check completed for {Component} with status {Status}",
                result.ComponentName,
                result.Status);
        }

        var overall = ResolveOverallStatus(checks);
        return new AIPlatformHealthReport
        {
            OverallStatus = overall,
            Checks = checks,
            GeneratedUtc = DateTime.UtcNow,
        };
    }

    public async Task<AIHealthCheckResult> GetComponentHealthAsync(string componentName, CancellationToken cancellationToken = default)
    {
        var provider = _registry.GetProviders().FirstOrDefault(p => p.ComponentName == componentName)
            ?? throw new KeyNotFoundException($"Health provider not registered: {componentName}");
        return await provider.CheckHealthAsync(cancellationToken);
    }

    private static AIHealthStatus ResolveOverallStatus(IReadOnlyList<AIHealthCheckResult> checks)
    {
        if (checks.Count == 0)
        {
            return AIHealthStatus.Offline;
        }

        if (checks.Any(c => c.Status == AIHealthStatus.Offline))
        {
            return AIHealthStatus.Offline;
        }

        if (checks.Any(c => c.Status == AIHealthStatus.Degraded))
        {
            return AIHealthStatus.Degraded;
        }

        if (checks.Any(c => c.Status == AIHealthStatus.Maintenance))
        {
            return AIHealthStatus.Maintenance;
        }

        if (checks.All(c => c.Status == AIHealthStatus.Live))
        {
            return AIHealthStatus.Live;
        }

        return AIHealthStatus.Ready;
    }
}
