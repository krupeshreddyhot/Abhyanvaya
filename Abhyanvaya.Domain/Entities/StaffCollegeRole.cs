using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities
{
    public class StaffCollegeRole : BaseEntity
    {
        public int StaffId { get; set; }
        public Staff Staff { get; set; } = null!;

        public int CollegeRoleLookupId { get; set; }
        public CollegeRoleLookup CollegeRoleLookup { get; set; } = null!;

        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
    }
}
