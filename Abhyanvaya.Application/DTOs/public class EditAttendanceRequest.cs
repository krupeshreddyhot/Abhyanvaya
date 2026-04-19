using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Abhyanvaya.Application.DTOs
{
    public class EditAttendanceRequest
    {
        public int SubjectId { get; set; }
        public DateTime Date { get; set; }
        public List<StudentAttendanceDto> Students { get; set; }
    }
}
