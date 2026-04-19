using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities
{
    public class College : BaseEntity
    {
        public required string Name { get; set; }
        public string? ShortName { get; set; }
        public required string Code { get; set; }

        public int UniversityId { get; set; }
        public University University { get; set; } = null!;

        public int? ParentCollegeId { get; set; }
        public College? ParentCollege { get; set; }

        /// <summary>Opaque key for public logo URLs under wwwroot/branding/{key}/.</summary>
        public Guid? LogoAccessKey { get; set; }

        /// <summary>UTC timestamp of last logo upload (cache-busting).</summary>
        public DateTime? LogoUpdatedUtc { get; set; }
    }
}
