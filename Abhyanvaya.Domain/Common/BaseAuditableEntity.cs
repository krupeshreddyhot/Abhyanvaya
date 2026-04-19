namespace Abhyanvaya.Domain.Common
{
    public abstract class BaseAuditableEntity : BaseEntity
    {
        public DateTime? ModifiedDate { get; set; }
    }
}
