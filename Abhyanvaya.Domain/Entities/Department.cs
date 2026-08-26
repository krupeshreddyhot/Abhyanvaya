using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Entities.Academic;

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

        /// <summary>AI-SCHED-CATALOG/TIMETABLE P1-2 — Programs owned by this department (optional EnablePrograms layer).</summary>
        public ICollection<Program> Programs { get; set; } = new List<Program>();
    }
}
