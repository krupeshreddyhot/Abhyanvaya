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
    }
}
