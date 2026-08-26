
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.DTOs
{
    public class MarkAttendanceRequest
    {
        public int SubjectId { get; set; }
        public DateTime Date { get; set; }

        public List<StudentAttendanceDto> Students { get; set; } = new();

        /// <summary>
        /// AI29.1D.15A Prompt 2 — optional single-section convenience field.
        /// Omitted with empty <see cref="SectionIds"/> = legacy full cohort (no section scope).
        /// </summary>
        public int? SectionId { get; set; }

        /// <summary>
        /// AI29.1D.15A Prompt 2 — optional section scope.
        /// One id = single section; multiple = combined operational class. Not mandatory.
        /// </summary>
        public List<int>? SectionIds { get; set; }
    }

    public class StudentAttendanceDto
    {
        public string StudentNumber { get; set; } = string.Empty;
        public AttendanceStatus Status { get; set; }
    }
}
