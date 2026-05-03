using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities
{
    public class CollegeRoleLookup : BaseEntity
    {
        public string Name { get; set; } = null!;
        public string? Code { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;

        /// <summary>When true, at most one staff member per college may hold this role (e.g. Principal).</summary>
        public bool IsExclusivePerCollege { get; set; }
    }
}
