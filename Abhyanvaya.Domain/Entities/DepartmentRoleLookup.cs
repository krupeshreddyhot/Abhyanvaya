using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities
{
    public class DepartmentRoleLookup : BaseEntity
    {
        public string Name { get; set; } = null!;
        public string? Code { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;

        /// <summary>When true, at most one staff member may hold this role per department.</summary>
        public bool IsExclusivePerDepartment { get; set; }
    }
}
