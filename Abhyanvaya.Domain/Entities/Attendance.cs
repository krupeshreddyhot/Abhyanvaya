using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Abhyanvaya.Domain.Entities
{
    public class Attendance : BaseEntity
    {
        public required int StudentId { get; set; }

        public required int SubjectId { get; set; }

        public DateTime Date { get; set; }

        public AttendanceStatus Status { get; set; }

        public Student? Student { get; set; }   
        
        public Subject Subject { get; set; } = null!;

        public bool IsLocked { get; set; } = false;
    }
}
