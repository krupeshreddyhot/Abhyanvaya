using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.DTOs.Subject
{
    public class UpdateSubjectRequest
    {
        public int Id { get; set; }
        public int TenantSubjectId { get; set; }

        public int CourseId { get; set; }

        public int GroupId { get; set; }

        public int SemesterId { get; set; }

        public bool IsElective { get; set; }

        public int? ElectiveGroupId { get; set; }

        public SubjectLanguageSlot LanguageSubjectSlot { get; set; }

        public int? TeachingLanguageId { get; set; }

        public decimal? HPW { get; set; }
        public decimal? Credits { get; set; }
        public decimal? ExamHours { get; set; }
        public decimal? Marks { get; set; }
    }
}
