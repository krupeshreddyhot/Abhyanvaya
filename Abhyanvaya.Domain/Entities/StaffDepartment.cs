using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities
{
    public class StaffDepartment : BaseEntity
    {
        public int StaffId { get; set; }
        public Staff Staff { get; set; } = null!;

        public int DepartmentId { get; set; }
        public Department Department { get; set; } = null!;

        public ICollection<StaffDepartmentRole> StaffDepartmentRoles { get; set; } = new List<StaffDepartmentRole>();
    }
}
