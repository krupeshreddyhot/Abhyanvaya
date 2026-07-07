using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.DomainEvents;

/// <summary>
/// Infrastructure dispatcher for domain events. Every event is logged generically, then
/// routed to any <see cref="IDomainEventHandler{TEvent}"/> registered in DI for its concrete type.
/// Events with no registered handler simply produce the generic log line, exactly as before
/// this routing capability was introduced.
/// </summary>
public sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(IServiceProvider serviceProvider, ILogger<DomainEventDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            _logger.LogInformation(
                "Domain event dispatched: {DomainEventType} at {OccurredUtc}",
                domainEvent.GetType().Name,
                domainEvent.OccurredUtc);

            await InvokeRegisteredHandlersAsync(domainEvent, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task InvokeRegisteredHandlersAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
        var handlers = _serviceProvider.GetServices(handlerType);
        var handleMethod = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync));

        foreach (var handler in handlers)
        {
            if (handler is null || handleMethod is null)
            {
                continue;
            }

            try
            {
                if (handleMethod.Invoke(handler, [domainEvent, cancellationToken]) is Task task)
                {
                    await task.ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                // Handlers are logging-only observers; a handler failure must never fail the
                // originating business operation (attendance was already persisted).
                _logger.LogError(
                    ex,
                    "Domain event handler {HandlerType} failed for {DomainEventType}",
                    handler.GetType().Name,
                    domainEvent.GetType().Name);
            }
        }
    }
}
