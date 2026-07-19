using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.TenantContext;

namespace Abhyanvaya.Infrastructure.TenantContext;

public sealed class InMemoryContextEventPublisher : IContextEventPublisher, IContextEventSubscriber
{
    private readonly List<Func<TenantContextSnapshot, Task>> _changedHandlers = [];
    private readonly List<Func<int, Task>> _clearedHandlers = [];
    private readonly List<Func<int, Task>> _expiredHandlers = [];
    private readonly List<Func<TenantContextSnapshot, Task>> _restoredHandlers = [];

    public void OnContextChanged(Func<TenantContextSnapshot, Task> handler) => _changedHandlers.Add(handler);

    public void OnContextCleared(Func<int, Task> handler) => _clearedHandlers.Add(handler);

    public void OnContextExpired(Func<int, Task> handler) => _expiredHandlers.Add(handler);

    public void OnContextRestored(Func<TenantContextSnapshot, Task> handler) => _restoredHandlers.Add(handler);

    public async Task PublishContextChangedAsync(TenantContextSnapshot context, CancellationToken cancellationToken = default) =>
        await DispatchAsync(_changedHandlers, h => h(context), cancellationToken);

    public async Task PublishContextClearedAsync(int userId, CancellationToken cancellationToken = default) =>
        await DispatchAsync(_clearedHandlers, h => h(userId), cancellationToken);

    public async Task PublishContextExpiredAsync(int userId, CancellationToken cancellationToken = default) =>
        await DispatchAsync(_expiredHandlers, h => h(userId), cancellationToken);

    public async Task PublishContextRestoredAsync(TenantContextSnapshot context, CancellationToken cancellationToken = default) =>
        await DispatchAsync(_restoredHandlers, h => h(context), cancellationToken);

    private static async Task DispatchAsync<THandler>(
        IReadOnlyList<THandler> handlers,
        Func<THandler, Task> invoke,
        CancellationToken cancellationToken)
    {
        foreach (var handler in handlers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await invoke(handler);
        }
    }
}
