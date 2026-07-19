using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.TenantContext;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.TenantContext;

public sealed class ContextExpirationService : IContextExpirationService
{
    private readonly IOptions<ContextPlatformOptions> _options;

    public ContextExpirationService(IOptions<ContextPlatformOptions> options)
    {
        _options = options;
    }

    public TimeSpan DefaultTimeout => TimeSpan.FromHours(_options.Value.ExpirationHours);

    public DateTime ComputeExpiresUtc(DateTime createdUtc) =>
        createdUtc.Add(DefaultTimeout);

    public bool IsExpired(TenantContextSnapshot snapshot)
    {
        if (snapshot.IsGlobal)
        {
            return false;
        }

        var expiresUtc = snapshot.ExpiresUtc ?? ComputeExpiresUtc(snapshot.CreatedUtc);
        return DateTime.UtcNow >= expiresUtc;
    }

    public TimeSpan GetRemainingTime(TenantContextSnapshot snapshot)
    {
        var expiresUtc = snapshot.ExpiresUtc ?? ComputeExpiresUtc(snapshot.CreatedUtc);
        var remaining = expiresUtc - DateTime.UtcNow;
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }
}

public sealed class ContextRefreshService : IContextRefreshService
{
    private readonly ITenantContextStore _store;
    private readonly IContextPersistenceProvider _persistence;
    private readonly IContextExpirationService _expiration;
    private readonly IContextEventPublisher _events;
    private readonly IAuditService _audit;
    private readonly IContextOperationalMetricsCollector _metrics;

    public ContextRefreshService(
        ITenantContextStore store,
        IContextPersistenceProvider persistence,
        IContextExpirationService expiration,
        IContextEventPublisher events,
        IAuditService audit,
        IContextOperationalMetricsCollector metrics)
    {
        _store = store;
        _persistence = persistence;
        _expiration = expiration;
        _events = events;
        _audit = audit;
        _metrics = metrics;
    }

    public async Task<bool> RefreshAsync(int userId, CancellationToken cancellationToken = default)
    {
        var context = await _store.GetAsync(userId, cancellationToken);
        if (context is null || context.IsGlobal)
        {
            return false;
        }

        var renewed = context with
        {
            CreatedUtc = DateTime.UtcNow,
            ExpiresUtc = _expiration.ComputeExpiresUtc(DateTime.UtcNow),
        };

        await _store.SetAsync(userId, renewed, cancellationToken);
        await _events.PublishContextRestoredAsync(renewed, cancellationToken);
        await RecordRenewedAuditAsync(userId, renewed, cancellationToken);
        _metrics.RecordContextDuration(_expiration.DefaultTimeout);
        return true;
    }

    private async Task RecordRenewedAuditAsync(int userId, TenantContextSnapshot renewed, CancellationToken cancellationToken) =>
        await _audit.RecordAsync(
            "TenantContext",
            userId.ToString(),
            Domain.Enums.AuditAction.Custom,
            newValues: new { Action = "ContextRenewed", renewed.SelectedCollegeId, renewed.ExpiresUtc });
}

public sealed class ContextCleanupWorker : IContextCleanupWorker
{
    private readonly ITenantContextStore _store;
    private readonly IContextExpirationService _expiration;
    private readonly IContextEventPublisher _events;
    private readonly IAuditService _audit;
    private readonly IContextOperationalMetricsCollector _metrics;
    private readonly IAITelemetryService _telemetry;

    public ContextCleanupWorker(
        ITenantContextStore store,
        IContextExpirationService expiration,
        IContextEventPublisher events,
        IAuditService audit,
        IContextOperationalMetricsCollector metrics,
        IAITelemetryService telemetry)
    {
        _store = store;
        _expiration = expiration;
        _events = events;
        _audit = audit;
        _metrics = metrics;
        _telemetry = telemetry;
    }

    public async Task<int> CleanupExpiredContextAsync(int userId, CancellationToken cancellationToken = default)
    {
        var context = await _store.GetAsync(userId, cancellationToken);
        if (context is null || context.IsGlobal || !_expiration.IsExpired(context))
        {
            return 0;
        }

        await _store.RemoveAsync(userId, cancellationToken);
        await _events.PublishContextExpiredAsync(userId, cancellationToken);
        await _audit.RecordAsync(
            "TenantContext",
            userId.ToString(),
            Domain.Enums.AuditAction.Custom,
            newValues: new { Action = "ContextExpired", context.SelectedCollegeId });

        _metrics.RecordContextExpired();
        _telemetry.RecordDuration("context.expired", TimeSpan.Zero);
        return 1;
    }
}
