using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Abhyanvaya.Application.DTOs.Semester
{
    public class UpdateSemesterRequest
    {
        public int Id { get; set; }
        public int Number { get; set; }        // 1,2,3...
        public string Name { get; set; }      // Semester 1
        public int CourseId { get; set; }
        public int? GroupId { get; set; }
    }
}
