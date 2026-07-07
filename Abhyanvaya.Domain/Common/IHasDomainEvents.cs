namespace Abhyanvaya.Domain.Common;

/// <summary>
/// Aggregate roots that accumulate domain events before dispatch.
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void AddDomainEvent(IDomainEvent domainEvent);

    void ClearDomainEvents();
}
