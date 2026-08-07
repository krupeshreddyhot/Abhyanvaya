using Abhyanvaya.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Abhyanvaya.Domain.Entities
{
    public class Group : BaseEntity
    {
        public string Code { get; set; }   // FIN, CA
        public string Name { get; set; }   // Finance, Computer Applications
        public int CourseId { get; set; }

        /// <summary>AI29.1A.5 — display sort key (DisplayOrder then Name).</summary>
        public int DisplayOrder { get; set; }

        public Course Course { get; set; }
    }
}
