namespace Abhyanvaya.Application.DTOs.Admin;

public sealed class ProvisionedCollegeDto
{
    public int TenantId { get; set; }
    public int CollegeId { get; set; }
    public string CollegeCode { get; set; } = string.Empty;
    public string CollegeName { get; set; } = string.Empty;
    public string UniversityCode { get; set; } = string.Empty;
}
