using Abhyanvaya.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Abhyanvaya.Domain.Entities
{
    public class ElectiveGroup : BaseEntity
    {
        public string Name { get; set; }       // Major 1, Major 2

        public int CourseId { get; set; }
        public Course? Course { get; set; }

        public int SemesterId { get; set; }
        public Semester? Semester { get; set; }

        public int GroupId { get; set; }
        public Group? Group { get; set; }
    }
}
