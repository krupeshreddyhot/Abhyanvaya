using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Entities
{
    public class Subject : BaseEntity
    {
        public string Code { get; set; } = null!;
        public string Name { get; set; }

        public int CourseId { get; set; }
        public Course? Course { get; set; }

        public int GroupId { get; set; }
        public Group? Group { get; set; }

        public int SemesterId { get; set; }
        public Semester? Semester { get; set; }

        public bool IsElective { get; set; }

        public int? ElectiveGroupId { get; set; }
        public ElectiveGroup? ElectiveGroup { get; set; }

        /// <summary>For language subjects: first vs second language slot (single <see cref="Language"/> catalog).</summary>
        public SubjectLanguageSlot LanguageSubjectSlot { get; set; }

        /// <summary>Which language this subject teaches (e.g. Sanskrit FL or SL row).</summary>
        public int? TeachingLanguageId { get; set; }
        public Language? TeachingLanguage { get; set; }
    }
}
