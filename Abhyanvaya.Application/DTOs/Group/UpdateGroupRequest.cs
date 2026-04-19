using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Abhyanvaya.Application.DTOs.Group
{
    public class UpdateGroupRequest
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;

        public int CourseId { get; set; }
    }
}
