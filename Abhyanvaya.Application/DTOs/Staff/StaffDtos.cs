namespace Abhyanvaya.Application.DTOs.Staff
{
    public class LookupItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Code { get; set; }
        public int SortOrder { get; set; }
    }

    public class StaffSetupMetadataDto
    {
        public List<LookupItemDto> StaffTypes { get; set; } = [];
        public List<LookupItemDto> PersonTitles { get; set; } = [];
        public List<LookupItemDto> Designations { get; set; } = [];
        public List<LookupItemDto> Qualifications { get; set; } = [];
        public List<LookupItemDto> EmploymentStatuses { get; set; } = [];
        public List<LookupItemDto> DepartmentRoles { get; set; } = [];
        public List<LookupItemDto> CollegeRoles { get; set; } = [];
        public List<CollegeSummaryDto> Colleges { get; set; } = [];
    }

    public class CollegeSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Code { get; set; } = "";
    }

    public class StaffListItemDto
    {
        public int Id { get; set; }
        public int CollegeId { get; set; }
        public string? StaffCode { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string StaffTypeName { get; set; } = "";
        public string DesignationName { get; set; } = "";
        public string? Email { get; set; }
        public DateTime? DateOfJoining { get; set; }
    }

    public class StaffDepartmentAssignmentDto
    {
        public int DepartmentId { get; set; }
        public List<int> DepartmentRoleLookupIds { get; set; } = [];
    }

    public class StaffDetailDto
    {
        public int Id { get; set; }
        public int CollegeId { get; set; }
        public string? StaffCode { get; set; }
        public int StaffTypeId { get; set; }
        public int? PersonTitleId { get; set; }
        public int DesignationId { get; set; }
        public int? QualificationId { get; set; }
        public int? GenderId { get; set; }
        public int? EmploymentStatusId { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string? Phone { get; set; }
        public string? AltPhone { get; set; }
        public string? Email { get; set; }
        public string? Website { get; set; }
        public DateTime? DateOfJoining { get; set; }
        public DateTime? ContractEndDate { get; set; }
        public DateTime? DateOfBirth { get; set; }

        public List<StaffDepartmentAssignmentDto> Departments { get; set; } = [];
        public List<int> CollegeRoleLookupIds { get; set; } = [];
        public List<int> SubjectIds { get; set; } = [];
    }
}
