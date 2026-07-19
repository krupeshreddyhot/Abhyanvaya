using Abhyanvaya.Application.AIOperations;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Operations;

public sealed class AIHealthRegistry : IAIHealthRegistry
{
    private readonly object _lock = new();
    private readonly HashSet<string> _componentNames = new(StringComparer.Ordinal);

    public IReadOnlyList<string> RegisteredComponents
    {
        get
        {
            lock (_lock)
            {
                return _componentNames.OrderBy(n => n).ToList();
            }
        }
    }

    public void Register(IAIHealthCheckProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        RegisterComponent(provider.ComponentName);
    }

    public void RegisterComponent(string componentName)
    {
        if (string.IsNullOrWhiteSpace(componentName))
        {
            return;
        }

        lock (_lock)
        {
            _componentNames.Add(componentName);
        }
    }
}

public sealed class AIHealthService : IAIHealthService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AIHealthService> _logger;

    public AIHealthService(IServiceScopeFactory scopeFactory, ILogger<AIHealthService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<AIPlatformHealthReport> GetPlatformHealthAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var providers = scope.ServiceProvider.GetServices<IAIHealthCheckProvider>();
        var checks = new List<AIHealthCheckResult>();

        foreach (var provider in providers)
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
        await using var scope = _scopeFactory.CreateAsyncScope();
        var provider = scope.ServiceProvider.GetServices<IAIHealthCheckProvider>()
            .FirstOrDefault(p => p.ComponentName == componentName)
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
