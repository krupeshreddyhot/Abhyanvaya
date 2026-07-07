using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Handles a specific domain event type. Implementations must be side-effect-light
/// (e.g. structured logging) — no business logic or persistence should live here.
/// Multiple handlers may be registered for the same event type.
/// </summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}
