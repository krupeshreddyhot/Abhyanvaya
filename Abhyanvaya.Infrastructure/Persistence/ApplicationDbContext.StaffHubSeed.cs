using Abhyanvaya.Domain.Authorization;
using Abhyanvaya.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Infrastructure.Persistence
{
    public partial class ApplicationDbContext
    {
        private static readonly DateTime SeedUtc = new(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc);

        private void SeedPermissionsAndRoles(ModelBuilder builder)
        {
            builder.Entity<Permission>().HasData(
                new Permission { Id = 1, Key = PermissionKeys.StudentsView, Resource = "Students", Action = "View" },
                new Permission { Id = 2, Key = PermissionKeys.StudentsManage, Resource = "Students", Action = "Manage" },
                new Permission { Id = 3, Key = PermissionKeys.AttendanceView, Resource = "Attendance", Action = "View" },
                new Permission { Id = 4, Key = PermissionKeys.AttendanceManage, Resource = "Attendance", Action = "Manage" },
                new Permission { Id = 5, Key = PermissionKeys.ReportsView, Resource = "Reports", Action = "View" },
                new Permission { Id = 6, Key = PermissionKeys.SetupSubjectsManage, Resource = "Setup.Subjects", Action = "Manage" },
                new Permission { Id = 7, Key = PermissionKeys.SetupDepartmentsManage, Resource = "Setup.Departments", Action = "Manage" },
                new Permission { Id = 8, Key = PermissionKeys.SetupStaffManage, Resource = "Setup.Staff", Action = "Manage" },
                new Permission { Id = 9, Key = PermissionKeys.DashboardView, Resource = "Dashboard", Action = "View" },
                new Permission { Id = 10, Key = PermissionKeys.OrganizationManage, Resource = "Organization", Action = "Manage" },
                new Permission { Id = 11, Key = PermissionKeys.MasterView, Resource = "Master", Action = "View" },
                new Permission { Id = 12, Key = PermissionKeys.SetupLookupsManage, Resource = "Setup.Lookups", Action = "Manage" },
                new Permission { Id = 13, Key = PermissionKeys.SetupCoursesManage, Resource = "Setup.Courses", Action = "Manage" },
                new Permission { Id = 14, Key = PermissionKeys.SetupGroupsManage, Resource = "Setup.Groups", Action = "Manage" },
                new Permission { Id = 15, Key = PermissionKeys.SetupSemestersManage, Resource = "Setup.Semesters", Action = "Manage" });

            builder.Entity<ApplicationRole>().HasData(
                new ApplicationRole
                {
                    Id = 100,
                    TenantId = 1,
                    Name = "Administrator",
                    Code = "ADMIN",
                    Description = "Full tenant administration (legacy Admin enum)",
                    CreatedDate = SeedUtc,
                    IsDeleted = false
                },
                new ApplicationRole
                {
                    Id = 101,
                    TenantId = 1,
                    Name = "Faculty",
                    Code = "FACULTY",
                    Description = "Teaching staff (legacy Faculty enum)",
                    CreatedDate = SeedUtc,
                    IsDeleted = false
                });

            var adminLinks = Enumerable.Range(1, 15)
                .Select(pid => new ApplicationRolePermission { ApplicationRoleId = 100, PermissionId = pid })
                .ToArray();

            var facultyPermIds = new[] { 1, 3, 4, 5, 9, 11 };
            var facultyLinks = facultyPermIds
                .Select(pid => new ApplicationRolePermission { ApplicationRoleId = 101, PermissionId = pid })
                .ToArray();

            builder.Entity<ApplicationRolePermission>().HasData(adminLinks.Concat(facultyLinks).ToArray());

            builder.Entity<UserApplicationRole>().HasData(
                new UserApplicationRole { UserId = 1, ApplicationRoleId = 100 });
        }

        private void SeedStaffLookupDefaults(ModelBuilder builder)
        {
            builder.Entity<StaffTypeLookup>().HasData(
                new StaffTypeLookup { Id = 501, TenantId = 1, Name = "Teaching", Code = "TEACHING", SortOrder = 1, IsActive = true, CreatedDate = SeedUtc, IsDeleted = false },
                new StaffTypeLookup { Id = 502, TenantId = 1, Name = "Non-teaching", Code = "NONTEACHING", SortOrder = 2, IsActive = true, CreatedDate = SeedUtc, IsDeleted = false });

            builder.Entity<PersonTitleLookup>().HasData(
                new PersonTitleLookup { Id = 503, TenantId = 1, Name = "Dr", Code = "DR", SortOrder = 1, IsActive = true, CreatedDate = SeedUtc, IsDeleted = false },
                new PersonTitleLookup { Id = 504, TenantId = 1, Name = "Mr", Code = "MR", SortOrder = 2, IsActive = true, CreatedDate = SeedUtc, IsDeleted = false });

            builder.Entity<DesignationLookup>().HasData(
                new DesignationLookup { Id = 505, TenantId = 1, Name = "Lecturer", Code = "LECT", SortOrder = 1, IsActive = true, CreatedDate = SeedUtc, IsDeleted = false },
                new DesignationLookup { Id = 506, TenantId = 1, Name = "Professor", Code = "PROF", SortOrder = 2, IsActive = true, CreatedDate = SeedUtc, IsDeleted = false });

            builder.Entity<DepartmentRoleLookup>().HasData(
                new DepartmentRoleLookup { Id = 507, TenantId = 1, Name = "Head of Department", Code = "HOD", SortOrder = 1, IsActive = true, IsExclusivePerDepartment = true, CreatedDate = SeedUtc, IsDeleted = false },
                new DepartmentRoleLookup { Id = 508, TenantId = 1, Name = "Academic Coordinator", Code = "ACAD_COORD", SortOrder = 2, IsActive = true, IsExclusivePerDepartment = false, CreatedDate = SeedUtc, IsDeleted = false });

            builder.Entity<CollegeRoleLookup>().HasData(
                new CollegeRoleLookup { Id = 509, TenantId = 1, Name = "Principal", Code = "PRINCIPAL", SortOrder = 1, IsActive = true, IsExclusivePerCollege = true, CreatedDate = SeedUtc, IsDeleted = false },
                new CollegeRoleLookup { Id = 510, TenantId = 1, Name = "Vice Principal", Code = "VP", SortOrder = 2, IsActive = true, IsExclusivePerCollege = false, CreatedDate = SeedUtc, IsDeleted = false },
                new CollegeRoleLookup { Id = 511, TenantId = 1, Name = "Correspondent", Code = "CORR", SortOrder = 3, IsActive = true, IsExclusivePerCollege = false, CreatedDate = SeedUtc, IsDeleted = false },
                new CollegeRoleLookup { Id = 512, TenantId = 1, Name = "Chief Controller of Examinations", Code = "CCE", SortOrder = 4, IsActive = true, IsExclusivePerCollege = false, CreatedDate = SeedUtc, IsDeleted = false });

            builder.Entity<QualificationLookup>().HasData(
                new QualificationLookup { Id = 513, TenantId = 1, Name = "Ph.D.", Code = "PHD", SortOrder = 1, IsActive = true, CreatedDate = SeedUtc, IsDeleted = false },
                new QualificationLookup { Id = 514, TenantId = 1, Name = "M.A.", Code = "MA", SortOrder = 2, IsActive = true, CreatedDate = SeedUtc, IsDeleted = false });

            builder.Entity<EmploymentStatusLookup>().HasData(
                new EmploymentStatusLookup { Id = 515, TenantId = 1, Name = "Active", Code = "ACTIVE", SortOrder = 1, IsActive = true, CreatedDate = SeedUtc, IsDeleted = false });
        }
    }
}
