using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities
{
    public class Department : BaseEntity
    {
        public int CollegeId { get; set; }
        public College College { get; set; } = null!;

        public string Name { get; set; } = null!;
        public string? Code { get; set; }
        public int SortOrder { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
