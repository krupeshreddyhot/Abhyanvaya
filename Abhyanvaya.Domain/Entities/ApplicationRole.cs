using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities
{
    public class ApplicationRole : BaseEntity
    {
        public string Name { get; set; } = null!;
        public string Code { get; set; } = null!;
        public string? Description { get; set; }

        public ICollection<ApplicationRolePermission> ApplicationRolePermissions { get; set; } = new List<ApplicationRolePermission>();
        public ICollection<UserApplicationRole> UserApplicationRoles { get; set; } = new List<UserApplicationRole>();
    }
}
