namespace Abhyanvaya.Application.DTOs.Admin
{
    public class UpdateTenantCollegeRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? ShortName { get; set; }
        public string Code { get; set; } = string.Empty;
        public int UniversityId { get; set; }
        public int? ParentCollegeId { get; set; }
    }
}
