using Abhyanvaya.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Abhyanvaya.Domain.Entities
{
    public class Course : BaseEntity
    {
        public string Code { get; set; }   // BCOM, BSC, BBA
        public string Name { get; set; }   // B.Com, B.Sc, BBA

        /// <summary>
        /// AI29.1A — optional Program link. Null when Programs are disabled or unassigned.
        /// Additive only; existing courses remain valid without a Program.
        /// </summary>
        public int? ProgramId { get; set; }

        /// <summary>AI29.1A.5 — display sort key (DisplayOrder then Name).</summary>
        public int DisplayOrder { get; set; }
    }
}
