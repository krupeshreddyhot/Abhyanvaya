using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Application.Internal;

/// <summary>
/// Dispatches and clears domain events collected on aggregates.
/// </summary>
internal static class DomainEventPublisher
{
    public static async Task DispatchAndClearAsync(
        IHasDomainEvents aggregate,
        IDomainEventDispatcher dispatcher,
        CancellationToken cancellationToken = default)
    {
        if (aggregate.DomainEvents.Count == 0)
        {
            return;
        }

        var events = aggregate.DomainEvents.ToList();
        aggregate.ClearDomainEvents();
        await dispatcher.DispatchAsync(events, cancellationToken);
    }
}
