namespace Abhyanvaya.Application.DTOs.Admin
{
    public class TenantCollegeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ShortName { get; set; }
        public string Code { get; set; } = string.Empty;
        public int UniversityId { get; set; }
        public string UniversityCode { get; set; } = string.Empty;
        public string UniversityName { get; set; } = string.Empty;
        public int? ParentCollegeId { get; set; }
        public string? ParentCollegeName { get; set; }

        /// <summary>Relative paths from API origin (no /api prefix), for &lt;img src&gt;.</summary>
        public string? LogoSmPath { get; set; }
        public string? LogoMdPath { get; set; }
        public string? LogoLgPath { get; set; }
    }
}
