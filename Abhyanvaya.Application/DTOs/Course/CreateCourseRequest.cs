using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Abhyanvaya.Application.DTOs.Course
{
    public class CreateCourseRequest
    {
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;

        /// <summary>AI29.1A optional — ignored when Programs disabled.</summary>
        public int? ProgramId { get; set; }
    }
}
