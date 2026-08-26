using Abhyanvaya.Application.DTOs.Course;
using Abhyanvaya.Application.DTOs.Group;

namespace Abhyanvaya.Application.DTOs.Semester
{
    public class CreateSemesterRequest
    {
        public int Number { get; set; }        // 1,2,3...
        public string Name { get; set; } = null!;      // Semester 1
        /// <summary>Optional client hint; server derives authoritative CourseId from Group.</summary>
        public int CourseId { get; set; }
        /// <summary>AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 2A — required for new operational Semesters.</summary>
        public int GroupId { get; set; }
    }
}
