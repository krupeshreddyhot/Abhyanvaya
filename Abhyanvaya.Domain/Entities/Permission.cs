namespace Abhyanvaya.Domain.Entities
{
    /// <summary>
    /// Global permission catalog (not tenant-scoped). Linked from tenant-scoped <see cref="ApplicationRole"/> via <see cref="ApplicationRolePermission"/>.
    /// </summary>
    public class Permission
    {
        public int Id { get; set; }

        /// <summary>Stable key, e.g. Attendance.Edit — used in JWT claims.</summary>
        public string Key { get; set; } = null!;

        public string Resource { get; set; } = null!;
        public string Action { get; set; } = null!;
    }
}
