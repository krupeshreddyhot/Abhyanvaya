
namespace Abhyanvaya.Domain.Common
{
    public abstract class BaseEntity
    {
        public int Id { get; set; }

        public int TenantId { get; set; }

        public DateTime CreatedDate { get; set; }
        public int? CreatedBy { get; set; }

        public DateTime? UpdatedDate { get; set; }
        public int? UpdatedBy { get; set; }

        public bool IsDeleted { get; set; } = false; // Soft delete
    }
}
