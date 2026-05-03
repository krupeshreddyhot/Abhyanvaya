using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities
{
    public class QualificationLookup : BaseEntity
    {
        public string Name { get; set; } = null!;
        public string? Code { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
