using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.DTOs.Subject
{
    public class CreateSubjectRequest
    {
        public string Name { get; set; }

        public int CourseId { get; set; }        

        public int GroupId { get; set; }      

        public int SemesterId { get; set; }        

        public bool IsElective { get; set; }

        public int? ElectiveGroupId { get; set; }

        public SubjectLanguageSlot LanguageSubjectSlot { get; set; }

        public int? TeachingLanguageId { get; set; }
    }
}
