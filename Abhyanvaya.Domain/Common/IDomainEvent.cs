namespace Abhyanvaya.Domain.Common;

/// <summary>
/// Marker for domain events raised by aggregates.
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredUtc { get; }
}
