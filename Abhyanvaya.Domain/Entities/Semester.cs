using Abhyanvaya.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Abhyanvaya.Domain.Entities
{
    public class Semester : BaseEntity
    {
        public int Number { get; set; }        // 1,2,3...
        public string Name { get; set; }       // Semester 1

        public int CourseId { get; set; }
        public Course? Course { get; set; }

        public int? GroupId { get; set; }    
        public Group? Group { get; set; }

        /// <summary>AI29.1A.5 — display sort key (DisplayOrder then Name).</summary>
        public int DisplayOrder { get; set; }

        /// <summary>
        /// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3J-A (PromptCode P1-4-3JA) —
        /// Explicit historical disposition. Distinct from <see cref="Abhyanvaya.Domain.Common.BaseEntity.IsDeleted"/>.
        /// Historical rows remain readable for audit/reporting/FK integrity but are excluded from
        /// operational Semester selection and new Student/SA/TT/TG assignments.
        /// Does not imply Group ownership and must never be used to guess GroupId.
        /// </summary>
        public bool IsHistoricalArchive { get; set; }
    }
}
