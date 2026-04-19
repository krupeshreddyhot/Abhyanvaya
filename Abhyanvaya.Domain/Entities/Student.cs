using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities
{
    public class Student : BaseEntity
    {
        public required string StudentNumber { get; set; } // Unique
        public  string? AppraId { get; set; }
        public required string Name { get; set; }

        public int CourseId { get; set; }
        public int GroupId { get; set; }
        public int GenderId { get; set; }
        public int MediumId { get; set; }

        /// <summary>First language (single catalog). Defaults to English per tenant.</summary>
        public int FirstLanguageId { get; set; }

        /// <summary>Second language (<see cref="Language"/>).</summary>
        public int LanguageId { get; set; }
        public int? Batch { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public  string? MobileNumber { get; set; }
        public string? AlternateMobileNumber { get; set; }

        public  string? Email { get; set; }

        public  string? ParentMobileNumber { get; set; }
        public string? ParentAlternateMobileNumber { get; set; }

        public string? FatherName { get; set; }
        public string? MotherName { get; set; }
        
        //Navigation properties (nullable)
        public Course? Course { get; set; }
        public Group? Group { get; set; }
        public Gender? Gender { get; set; }
        public Medium? Medium { get; set; }

        public Language? FirstLanguage { get; set; }
        public Language? Language { get; set; }
        public int SemesterId { get; set; }
        public Semester? Semester { get; set; }

    }
}
