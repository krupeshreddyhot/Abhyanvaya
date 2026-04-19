
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.DTOs
{
    public class MarkAttendanceRequest
    {
        public int SubjectId { get; set; }
        public DateTime Date { get; set; }

        public List<StudentAttendanceDto> Students { get; set; } = new();
    }

    public class StudentAttendanceDto
    {
        public string StudentNumber { get; set; } = string.Empty;
        public AttendanceStatus Status { get; set; }
    }
}
