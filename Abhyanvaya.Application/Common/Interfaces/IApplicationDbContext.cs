using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Application.Common.Interfaces
{
    public interface IApplicationDbContext
    {
        IQueryable<Student> Students { get; }
        IQueryable<Attendance> Attendances { get; }
        IQueryable<User> Users { get; }
        IQueryable<College> Colleges { get; }
        IQueryable<University> Universities { get; }
        IQueryable<Course> Courses { get; }
        IQueryable<Group> Groups { get; }
        IQueryable<Gender> Genders { get; }
        IQueryable<Medium> Mediums { get; }
        IQueryable<Language> Languages { get; }
        IQueryable<TenantSubject> TenantSubjects { get; }
        IQueryable<Subject> Subjects { get; }
        IQueryable<StudentSubject> StudentSubjects { get; }
        IQueryable<ElectiveGroup> ElectiveGroups { get; }
        IQueryable<Semester> Semesters { get; }
        IQueryable<Department> Departments { get; }
        IQueryable<Staff> StaffMembers { get; }
        IQueryable<StaffDepartment> StaffDepartments { get; }
        IQueryable<StaffDepartmentRole> StaffDepartmentRoles { get; }
        IQueryable<StaffCollegeRole> StaffCollegeRoles { get; }
        IQueryable<StaffSubjectAssignment> StaffSubjectAssignments { get; }
        IQueryable<StaffTypeLookup> StaffTypeLookups { get; }
        IQueryable<PersonTitleLookup> PersonTitleLookups { get; }
        IQueryable<DesignationLookup> DesignationLookups { get; }
        IQueryable<DepartmentRoleLookup> DepartmentRoleLookups { get; }
        IQueryable<CollegeRoleLookup> CollegeRoleLookups { get; }
        IQueryable<QualificationLookup> QualificationLookups { get; }
        IQueryable<EmploymentStatusLookup> EmploymentStatusLookups { get; }
        IQueryable<Permission> Permissions { get; }
        IQueryable<ApplicationRole> ApplicationRoles { get; }
        IQueryable<UserApplicationRole> UserApplicationRoles { get; }
        Task AddAsync<T>(T entity) where T : class;
        void Remove<T>(T entity) where T : class;
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        void AddAttendances(IEnumerable<Attendance> attendances);
        Task AddRangeAsync<T>(IEnumerable<T> entities) where T : class;

    }
}
