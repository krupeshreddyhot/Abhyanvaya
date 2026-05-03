namespace Abhyanvaya.Domain.Entities
{
    public class ApplicationRolePermission
    {
        public int ApplicationRoleId { get; set; }
        public ApplicationRole ApplicationRole { get; set; } = null!;

        public int PermissionId { get; set; }
        public Permission Permission { get; set; } = null!;
    }
}
