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
        public string Name { get; set; }   // Finance, Computer Applications
        public int CourseId { get; set; }

        public Course Course { get; set; }
    }
}
