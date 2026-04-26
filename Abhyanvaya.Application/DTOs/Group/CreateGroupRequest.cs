using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Abhyanvaya.Application.DTOs.Group
{
    public class CreateGroupRequest
    {
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public int CourseId { get; set; }
    }
}
