using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.TenantContext;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.TenantContext;

public sealed class DistributedCacheTenantContextStore : ITenantContextStore
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(8);
    private readonly ICacheService _cache;
    private readonly ILogger<DistributedCacheTenantContextStore> _logger;

    public DistributedCacheTenantContextStore(ICacheService cache, ILogger<DistributedCacheTenantContextStore> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public Task<TenantContextSnapshot?> GetAsync(int userId, CancellationToken cancellationToken = default) =>
        _cache.GetAsync<TenantContextSnapshot>(BuildKey(userId));

    public async Task SetAsync(int userId, TenantContextSnapshot context, CancellationToken cancellationToken = default)
    {
        await _cache.SetAsync(BuildKey(userId), context, DefaultTtl);
        _logger.LogDebug("Tenant context stored for user {UserId}, college {CollegeId}", userId, context.SelectedCollegeId);
    }

    public async Task RemoveAsync(int userId, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(BuildKey(userId));
        _logger.LogDebug("Tenant context cleared for user {UserId}", userId);
    }

    private static string BuildKey(int userId) => $"tenant-context:v1:{userId}";
}
