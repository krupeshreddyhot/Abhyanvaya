using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities;

/// <summary>
/// Domain event support for the <see cref="AttendanceSession"/> aggregate root.
/// </summary>
public partial class AttendanceSession : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
