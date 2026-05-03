using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities
{
    public class StaffSubjectAssignment : BaseEntity
    {
        public int StaffId { get; set; }
        public Staff Staff { get; set; } = null!;

        public int SubjectId { get; set; }
        public Subject Subject { get; set; } = null!;
    }
}
