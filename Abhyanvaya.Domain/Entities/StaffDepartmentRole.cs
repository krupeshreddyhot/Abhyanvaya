using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities
{
    public class StaffDepartmentRole : BaseEntity
    {
        public int StaffDepartmentId { get; set; }
        public StaffDepartment StaffDepartment { get; set; } = null!;

        public int DepartmentRoleLookupId { get; set; }
        public DepartmentRoleLookup DepartmentRoleLookup { get; set; } = null!;

        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
    }
}
