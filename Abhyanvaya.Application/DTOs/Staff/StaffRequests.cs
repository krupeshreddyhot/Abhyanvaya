namespace Abhyanvaya.Application.DTOs.Staff
{
    public class CreateStaffRequest
    {
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

        public List<StaffDepartmentAssignmentDto>? Departments { get; set; }
        public List<int>? CollegeRoleLookupIds { get; set; }
        public List<int>? SubjectIds { get; set; }
    }

    public class UpdateStaffRequest : CreateStaffRequest
    {
    }

    public class ReplaceStaffAssignmentsRequest
    {
        public List<StaffDepartmentAssignmentDto>? Departments { get; set; }
        public List<int>? CollegeRoleLookupIds { get; set; }
        public List<int>? SubjectIds { get; set; }
    }
}
