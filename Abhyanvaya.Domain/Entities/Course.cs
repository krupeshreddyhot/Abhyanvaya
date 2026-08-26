using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Entities.Academic;

namespace Abhyanvaya.Domain.Entities
{
    public class Course : BaseEntity
    {
        public string Code { get; set; }   // BCOM, BSC, BBA
        public string Name { get; set; }   // B.Com, B.Sc, BBA

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-3 — authoritative catalog Department ownership (Option A).
        /// Required; same Tenant; when ProgramId is set must equal Program.DepartmentId.
        /// </summary>
        public int DepartmentId { get; set; }

        public Department? Department { get; set; }

        /// <summary>
        /// AI29.1A — optional Program link. Null when Programs are disabled or unassigned.
        /// Additive only; existing courses remain valid without a Program.
        /// </summary>
        public int? ProgramId { get; set; }

        public Program? Program { get; set; }

        /// <summary>AI29.1A.5 — display sort key (DisplayOrder then Name).</summary>
        public int DisplayOrder { get; set; }
    }
}
