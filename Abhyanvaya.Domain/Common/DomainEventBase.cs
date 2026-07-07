namespace Abhyanvaya.Domain.Common;

/// <summary>
/// Base type for domain events with a UTC occurrence timestamp.
/// </summary>
public abstract record DomainEventBase : IDomainEvent
{
    protected DomainEventBase(DateTime? occurredUtc = null) =>
        OccurredUtc = occurredUtc ?? DateTime.UtcNow;

    public DateTime OccurredUtc { get; init; }
}
