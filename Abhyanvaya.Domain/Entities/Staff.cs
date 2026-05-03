using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities
{
    public class Staff : BaseEntity
    {
        public int CollegeId { get; set; }
        public College College { get; set; } = null!;

        /// <summary>Employee/staff code or number; unique per college when set.</summary>
        public string? StaffCode { get; set; }

        public int StaffTypeId { get; set; }
        public StaffTypeLookup StaffType { get; set; } = null!;

        public int? PersonTitleId { get; set; }
        public PersonTitleLookup? PersonTitle { get; set; }

        public int DesignationId { get; set; }
        public DesignationLookup Designation { get; set; } = null!;

        public int? QualificationId { get; set; }
        public QualificationLookup? Qualification { get; set; }

        public int? GenderId { get; set; }
        public Gender? Gender { get; set; }

        public int? EmploymentStatusId { get; set; }
        public EmploymentStatusLookup? EmploymentStatus { get; set; }

        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string? Phone { get; set; }
        public string? AltPhone { get; set; }
        public string? Email { get; set; }
        public string? Website { get; set; }

        public DateTime? DateOfJoining { get; set; }
        public DateTime? ContractEndDate { get; set; }
        public DateTime? DateOfBirth { get; set; }

        public ICollection<StaffDepartment> StaffDepartments { get; set; } = new List<StaffDepartment>();
        public ICollection<StaffCollegeRole> StaffCollegeRoles { get; set; } = new List<StaffCollegeRole>();
        public ICollection<StaffSubjectAssignment> StaffSubjectAssignments { get; set; } = new List<StaffSubjectAssignment>();
    }
}
