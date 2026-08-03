using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Domain.Entities
{
    public class Subject : BaseEntity
    {
        public int TenantSubjectId { get; set; }
        public TenantSubject? TenantSubject { get; set; }

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

        public decimal? HPW { get; set; }
        public decimal? Credits { get; set; }
        public decimal? ExamHours { get; set; }
        public decimal? Marks { get; set; }

        public int? SubjectCategoryId { get; set; }
        public SubjectCategory? SubjectCategory { get; set; }
        public RoomType? RequiresRoomType { get; set; }
        public int? DefaultDurationMinutes { get; set; }
        public bool RequiresLabEquipment { get; set; }

        public int? DeliveryTypeId { get; set; }
        public SubjectDeliveryType? DeliveryType { get; set; }
        public int? PreferredRoomFeatureId { get; set; }
        public RoomFeature? PreferredRoomFeature { get; set; }
        public bool RequiresAttendance { get; set; } = true;
        public int? ExpectedCapacity { get; set; }
    }
}
