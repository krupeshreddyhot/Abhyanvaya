using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities
{
    /// <summary>
    /// Tenant-scoped subject lookup/master. Reused by Subject assignments across course/group/semester.
    /// </summary>
    public class TenantSubject : BaseEntity
    {
        public string? Code { get; set; }
        public string Name { get; set; } = null!;
    }
}
