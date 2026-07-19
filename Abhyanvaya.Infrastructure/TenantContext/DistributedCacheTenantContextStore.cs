using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.TenantContext;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.TenantContext;

public sealed class DistributedCacheTenantContextStore : ITenantContextStore
{
    private readonly IContextPersistenceProvider _persistence;
    private readonly IOptions<ContextPlatformOptions> _options;
    private readonly ILogger<DistributedCacheTenantContextStore> _logger;

    public DistributedCacheTenantContextStore(
        IContextPersistenceProvider persistence,
        IOptions<ContextPlatformOptions> options,
        ILogger<DistributedCacheTenantContextStore> logger)
    {
        _persistence = persistence;
        _options = options;
        _logger = logger;
    }

    public Task<TenantContextSnapshot?> GetAsync(int userId, CancellationToken cancellationToken = default) =>
        _persistence.LoadAsync<TenantContextSnapshot>(BuildKey(userId), cancellationToken);

    public async Task SetAsync(int userId, TenantContextSnapshot context, CancellationToken cancellationToken = default)
    {
        var ttl = TimeSpan.FromHours(_options.Value.ExpirationHours);
        await _persistence.SaveAsync(BuildKey(userId), context, ttl, cancellationToken);
        _logger.LogDebug(
            "Tenant context stored for user {UserId}, college {CollegeId}, provider {Provider}",
            userId,
            context.SelectedCollegeId,
            _persistence.ProviderName);
    }

    public async Task RemoveAsync(int userId, CancellationToken cancellationToken = default)
    {
        await _persistence.DeleteAsync(BuildKey(userId), cancellationToken);
        _logger.LogDebug("Tenant context cleared for user {UserId}", userId);
    }

    internal static string BuildKey(int userId) => $"tenant-context:v1:{userId}";
}
