using Microsoft.EntityFrameworkCore;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums;
using System.Reflection;
using Microsoft.AspNetCore.Identity;

namespace Abhyanvaya.Infrastructure.Persistence
{
    public partial class ApplicationDbContext : DbContext, IApplicationDbContext
    {
        private readonly ICurrentUserService? _currentUserService;
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options,
                ICurrentUserService? currentUserService)
            : base(options)
        {
            _currentUserService = currentUserService;
        }

        public IQueryable<Student> Students => Set<Student>();
        public IQueryable<Attendance> Attendances => Set<Attendance>();
        public IQueryable<User> Users => Set<User>();
        public IQueryable<College> Colleges => Set<College>();
        public IQueryable<University> Universities => Set<University>();


        public IQueryable<Course> Courses => Set<Course>();
        public IQueryable<Group> Groups => Set<Group>();
        public IQueryable<Gender> Genders => Set<Gender>();
        public IQueryable<Medium> Mediums => Set<Medium>();
        public IQueryable<Language> Languages => Set<Language>();
        public IQueryable<TenantSubject> TenantSubjects => Set<TenantSubject>();

        public IQueryable<Semester> Semesters => Set<Semester>();
        public IQueryable<Subject> Subjects => Set<Subject>();
        public IQueryable<ElectiveGroup> ElectiveGroups => Set<ElectiveGroup>();
        public IQueryable<StudentSubject> StudentSubjects => Set<StudentSubject>();

        public IQueryable<Department> Departments => Set<Department>();
        public IQueryable<Staff> StaffMembers => Set<Staff>();
        public IQueryable<StaffDepartment> StaffDepartments => Set<StaffDepartment>();
        public IQueryable<StaffDepartmentRole> StaffDepartmentRoles => Set<StaffDepartmentRole>();
        public IQueryable<StaffCollegeRole> StaffCollegeRoles => Set<StaffCollegeRole>();
        public IQueryable<StaffSubjectAssignment> StaffSubjectAssignments => Set<StaffSubjectAssignment>();
        public IQueryable<StaffTypeLookup> StaffTypeLookups => Set<StaffTypeLookup>();
        public IQueryable<PersonTitleLookup> PersonTitleLookups => Set<PersonTitleLookup>();
        public IQueryable<DesignationLookup> DesignationLookups => Set<DesignationLookup>();
        public IQueryable<DepartmentRoleLookup> DepartmentRoleLookups => Set<DepartmentRoleLookup>();
        public IQueryable<CollegeRoleLookup> CollegeRoleLookups => Set<CollegeRoleLookup>();
        public IQueryable<QualificationLookup> QualificationLookups => Set<QualificationLookup>();
        public IQueryable<EmploymentStatusLookup> EmploymentStatusLookups => Set<EmploymentStatusLookup>();
        public IQueryable<Permission> Permissions => Set<Permission>();
        public IQueryable<ApplicationRole> ApplicationRoles => Set<ApplicationRole>();
        public IQueryable<ApplicationRolePermission> ApplicationRolePermissions => Set<ApplicationRolePermission>();
        public IQueryable<UserApplicationRole> UserApplicationRoles => Set<UserApplicationRole>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // FORCE EF TO INCLUDE  TABLES
            builder.Entity<Semester>();
            builder.Entity<TenantSubject>();
            builder.Entity<Subject>();
            builder.Entity<ElectiveGroup>();
            builder.Entity<StudentSubject>();            

            // FORCE EF TO INCLUDE LOOKUP TABLES
            builder.Entity<Course>();
            builder.Entity<Group>();
            builder.Entity<Gender>();
            builder.Entity<Medium>();
            builder.Entity<Language>();

            // FORCE EF TO INCLUDE ENTITIES
            builder.Entity<Student>();
            builder.Entity<Attendance>();
            builder.Entity<User>();
            builder.Entity<College>();
            builder.Entity<University>();

            builder.Entity<StaffTypeLookup>();
            builder.Entity<PersonTitleLookup>();
            builder.Entity<DesignationLookup>();
            builder.Entity<DepartmentRoleLookup>();
            builder.Entity<CollegeRoleLookup>();
            builder.Entity<QualificationLookup>();
            builder.Entity<EmploymentStatusLookup>();
            builder.Entity<Department>();
            builder.Entity<Staff>();
            builder.Entity<StaffDepartment>();
            builder.Entity<StaffDepartmentRole>();
            builder.Entity<StaffCollegeRole>();
            builder.Entity<StaffSubjectAssignment>();
            builder.Entity<Permission>();
            builder.Entity<ApplicationRole>();
            builder.Entity<ApplicationRolePermission>();
            builder.Entity<UserApplicationRole>();

            builder.Entity<Attendance>()
                .HasOne(a => a.Student)          // navigation
                .WithMany()                     // no collection in Student
                .HasForeignKey(a => a.StudentId);
            builder.Entity<Attendance>()
                .HasOne(a => a.Subject)
                .WithMany()
                .HasForeignKey(a => a.SubjectId);

            // Seed data
            builder.Entity<Course>().HasData(
                    new Course
                    {
                        Id = 1,
                        Code = "BCOM",
                        Name = "B.Com"
                    }
                );

            builder.Entity<Group>().HasData(
                new Group
                {
                    Id = 1,
                    Code = "FIN",
                    Name = "Finance",
                    CourseId = 1
                }
            );

            var hasher = new PasswordHasher<User>();
            builder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Username = "admin",
                    PasswordHash = "AQAAAAIAAYagAAAAEPYuSGVNqHBQTcjqI3OyH/6RZiCAR+6UuGlXPm5sNXqwQZt9izgviBHgdfNrJmzf3A==",
                    Role = UserRole.Admin,
                    TenantId = 1,
                    CourseId = 1, 
                    GroupId = 1,
                    CreatedDate = DateTime.UtcNow
                }
            );
            builder.Entity<Student>()
                    .HasOne(s => s.Course)
                    .WithMany()
                    .HasForeignKey(s => s.CourseId);

            builder.Entity<Student>()
                .HasOne(s => s.Group)
                .WithMany()
                .HasForeignKey(s => s.GroupId);

            builder.Entity<Student>()
                .HasOne(s => s.Gender)
                .WithMany()
                .HasForeignKey(s => s.GenderId);

            builder.Entity<Student>()
                .HasOne(s => s.Medium)
                .WithMany()
                .HasForeignKey(s => s.MediumId);

            builder.Entity<Student>()
                .HasOne(s => s.Language)
                .WithMany()
                .HasForeignKey(s => s.LanguageId);

            builder.Entity<Student>()
                .HasOne(s => s.FirstLanguage)
                .WithMany()
                .HasForeignKey(s => s.FirstLanguageId);

            builder.Entity<User>()
                    .HasOne(u => u.Course)
                    .WithMany()
                    .HasForeignKey(u => u.CourseId);

            builder.Entity<User>()
                    .HasOne(u => u.Group)
                    .WithMany()
                    .HasForeignKey(u => u.GroupId);
            builder.Entity<Subject>()
                .HasOne(x => x.TenantSubject)
                .WithMany()
                .HasForeignKey(x => x.TenantSubjectId);

            builder.Entity<Subject>()
                .HasOne(x => x.Semester)
                .WithMany()
                .HasForeignKey(x => x.SemesterId);

            builder.Entity<Subject>()
                .HasOne(x => x.ElectiveGroup)
                .WithMany()
                .HasForeignKey(x => x.ElectiveGroupId);

            builder.Entity<Subject>()
                .HasOne(s => s.TeachingLanguage)
                .WithMany()
                .HasForeignKey(s => s.TeachingLanguageId);

            // StudentSubject
            builder.Entity<StudentSubject>()
                .HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId);

            builder.Entity<StudentSubject>()
                .HasOne(x => x.Subject)
                .WithMany()
                .HasForeignKey(x => x.SubjectId);
            builder.Entity<Student>()
                .HasOne(s => s.Semester)
                .WithMany()
                .HasForeignKey(s => s.SemesterId);
            builder.Entity<User>()
                .HasIndex(u => new { u.TenantId, u.Username })
                .IsUnique();

            builder.Entity<Student>()
                .HasIndex(s => new { s.TenantId, s.StudentNumber })
                .IsUnique();

            builder.Entity<Course>()
                .HasIndex(x => new { x.TenantId, x.Code });

            builder.Entity<Group>()
                .HasIndex(x => new { x.TenantId, x.CourseId, x.Code });

            builder.Entity<TenantSubject>()
                .HasIndex(x => new { x.TenantId, x.Name });

            builder.Entity<Subject>()
                .HasIndex(s => new { s.TenantId, s.CourseId, s.GroupId, s.SemesterId, s.TenantSubjectId });

            builder.Entity<Attendance>()
                .HasIndex(x => new { x.TenantId, x.StudentId, x.SubjectId, x.Date })
                .IsUnique();

            // Speeds listing attendance by subject + date (marking screen, dashboards, reports)
            builder.Entity<Attendance>()
                .HasIndex(x => new { x.TenantId, x.SubjectId, x.Date })
                .HasDatabaseName("IX_Attendance_Tenant_Subject_Date");

            builder.Entity<University>()
                .HasIndex(u => u.Code)
                .IsUnique();

            builder.Entity<College>()
                .HasOne(c => c.University)
                .WithMany()
                .HasForeignKey(c => c.UniversityId);

            builder.Entity<College>()
                .HasOne(c => c.ParentCollege)
                .WithMany()
                .HasForeignKey(c => c.ParentCollegeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<College>()
                .HasIndex(x => new { x.UniversityId, x.Code })
                .IsUnique();

            StaffHubModelConfigurator.ConfigureStaffHub(builder);
            SeedPermissionsAndRoles(builder);
            SeedStaffLookupDefaults(builder);

            // Apply configurations
            builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

            // Global Tenant Filter
            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                {
                    var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(SetTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.MakeGenericMethod(entityType.ClrType);

                    method?.Invoke(this, new object[] { builder });
                }
            }
        }
        private void SetTenantFilter<TEntity>(ModelBuilder builder) where TEntity : BaseEntity
        {
            builder.Entity<TEntity>().HasQueryFilter(e =>
                !e.IsDeleted &&
                (_currentUserService == null
                 || IsSuperAdminCrossTenant()
                 || e.TenantId == _currentUserService.TenantId));
        }

        /// <summary>Super Admin operates across tenants (JWT TenantId is 0); skip row-level tenant filter.</summary>
        private bool IsSuperAdminCrossTenant() =>
            _currentUserService != null
            && string.Equals(_currentUserService.Role, nameof(UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase);
        public void AddAttendances(IEnumerable<Attendance> attendances)
        {
            Set<Attendance>().AddRange(attendances);
        }
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var currentUserId = _currentUserService?.UserId ?? 0;

            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                if (entry.State == EntityState.Added)
                {
                    if (entry.Entity.TenantId == 0 && _currentUserService?.TenantId > 0)
                    {
                        entry.Entity.TenantId = _currentUserService.TenantId;
                    }

                    entry.Entity.CreatedDate = DateTime.UtcNow;
                    entry.Entity.CreatedBy = currentUserId;
                }

                if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedDate = DateTime.UtcNow;
                    entry.Entity.UpdatedBy = currentUserId;
                }
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
        public async Task AddAsync<T>(T entity) where T : class
        {
            await Set<T>().AddAsync(entity);
        }

        public new void Remove<T>(T entity) where T : class
        {
            Set<T>().Remove(entity);
        }

        public async Task AddRangeAsync<T>(IEnumerable<T> entities) where T : class
        {
            await Set<T>().AddRangeAsync(entities);
        }
    }
   
}



