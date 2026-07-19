using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.TenantContext.ContextPersistence;

/// <summary>
/// Context persistence via the existing distributed cache stack (SmartCache → Memory / Redis).
/// </summary>
public sealed class DistributedCacheContextPersistenceProvider : IContextPersistenceProvider
{
    private readonly ICacheService _cache;
    private readonly ILogger<DistributedCacheContextPersistenceProvider> _logger;

    public DistributedCacheContextPersistenceProvider(
        ICacheService cache,
        ILogger<DistributedCacheContextPersistenceProvider> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public string ProviderName => "DistributedCache";

    public Task SaveAsync<T>(
        string key,
        T value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _cache.SetAsync(key, value, expiry);
    }

    public Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _cache.GetAsync<T>(key);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _cache.RemoveAsync(key);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await _cache.GetAsync<object>(key);
        return value is not null;
    }

    public async Task RefreshAsync(string key, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await _cache.GetAsync<object>(key);
        if (value is null)
        {
            _logger.LogDebug("Refresh skipped — key {Key} not found.", key);
            return;
        }

        await _cache.SetAsync(key, value, expiry);
        _logger.LogDebug("Refreshed TTL for key {Key}.", key);
    }
}
