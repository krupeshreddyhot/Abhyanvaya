namespace Abhyanvaya.Application.Common.Interfaces;

public interface IDistributedLockProvider
{
    Task<IAsyncDisposable?> TryAcquireLockAsync(
        string resourceKey,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
