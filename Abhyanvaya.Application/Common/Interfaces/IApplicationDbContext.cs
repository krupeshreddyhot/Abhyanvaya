using Abhyanvaya.Application.Common.Interfaces;

using Abhyanvaya.Domain.Entities;



namespace Abhyanvaya.Application.Common.Interfaces

{

    public interface IApplicationDbContext : IUnitOfWork

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

        IQueryable<ApplicationRolePermission> ApplicationRolePermissions { get; }

        IQueryable<UserApplicationRole> UserApplicationRoles { get; }

        IQueryable<AttendanceSession> AttendanceSessions { get; }

        IQueryable<AttendanceRecognition> AttendanceRecognitions { get; }

        IQueryable<AttendanceRecognitionReviewHistory> AttendanceRecognitionReviewHistories { get; }

        IQueryable<AttendanceDetail> AttendanceDetails { get; }

        IQueryable<AuditEntry> AuditEntries { get; }

        IQueryable<StudentFaceEmbedding> StudentFaceEmbeddings { get; }

        IQueryable<ClassSchedule> ClassSchedules { get; }

        IQueryable<StudentEnrollmentBatch> StudentEnrollmentBatches { get; }

        IQueryable<StudentEnrollmentItem> StudentEnrollmentItems { get; }

        IQueryable<StudentEnrollmentProgressSnapshot> StudentEnrollmentProgressSnapshots { get; }

        IQueryable<EnrollmentStorageRecord> EnrollmentStorageRecords { get; }

        IQueryable<EnrollmentEmbeddingVersionSnapshot> EnrollmentEmbeddingVersionSnapshots { get; }

        IQueryable<EnrollmentPersistenceAudit> EnrollmentPersistenceAudits { get; }

        IQueryable<EnrollmentWorkLease> EnrollmentWorkLeases { get; }

        IQueryable<EnrollmentDeadLetterEntry> EnrollmentDeadLetterEntries { get; }

        IQueryable<AiModelDefinition> AiModelDefinitions { get; }

        IQueryable<AiModelVersion> AiModelVersions { get; }

        IQueryable<GoldenDatasetDefinition> GoldenDatasetDefinitions { get; }

        IQueryable<ModelRolloutPlan> ModelRolloutPlans { get; }

        IQueryable<ModelLifecycleAuditEntry> ModelLifecycleAuditEntries { get; }

        IQueryable<RetrainingCandidateEntry> RetrainingCandidateEntries { get; }

        IQueryable<StudentPhotoAcquisitionBatch> StudentPhotoAcquisitionBatches { get; }

        IQueryable<StudentPhotoAcquisitionItem> StudentPhotoAcquisitionItems { get; }

        Task AddAsync<T>(T entity) where T : class;

        void Remove<T>(T entity) where T : class;

        void AddAttendances(IEnumerable<Attendance> attendances);

        Task AddRangeAsync<T>(IEnumerable<T> entities) where T : class;

    }

}


