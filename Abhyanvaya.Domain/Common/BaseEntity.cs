
namespace Abhyanvaya.Domain.Common
{
    public abstract class BaseEntity : IHasDomainEvents
    {
        private readonly List<IDomainEvent> _domainEvents = [];

        public int Id { get; set; }

        public int TenantId { get; set; }

        public DateTime CreatedDate { get; set; }
        public int? CreatedBy { get; set; }

        public DateTime? UpdatedDate { get; set; }
        public int? UpdatedBy { get; set; }

        public bool IsDeleted { get; set; } = false;

        public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        public void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

        public void ClearDomainEvents() => _domainEvents.Clear();
    }
}
