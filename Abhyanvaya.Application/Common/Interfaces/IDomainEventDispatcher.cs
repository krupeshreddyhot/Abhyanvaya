using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Dispatches domain events after successful persistence.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
