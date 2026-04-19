namespace Abhyanvaya.Application.DTOs.Admin;

public sealed class CreateTenantCollegeRequest
{
    public int UniversityId { get; set; }

    public string CollegeName { get; set; } = string.Empty;

    public string CollegeCode { get; set; } = string.Empty;

    public int? ParentCollegeId { get; set; }

    /// <summary>First tenant admin username (unique per tenant).</summary>
    public string AdminUsername { get; set; } = string.Empty;

    public string AdminPassword { get; set; } = string.Empty;
}
