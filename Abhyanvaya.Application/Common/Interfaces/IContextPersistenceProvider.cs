namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Enterprise context persistence abstraction — decouples platform code from Redis, SQL, PostgreSQL, etc.
/// </summary>
public interface IContextPersistenceProvider
{
    string ProviderName { get; }

    Task SaveAsync<T>(
        string key,
        T value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default);

    Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    Task RefreshAsync(string key, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
}
