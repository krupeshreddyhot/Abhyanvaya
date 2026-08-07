using Abhyanvaya.Domain.Authorization;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
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
                new Permission { Id = 15, Key = PermissionKeys.SetupSemestersManage, Resource = "Setup.Semesters", Action = "Manage" },
                new Permission { Id = 16, Key = PermissionKeys.EnrollmentView, Resource = "Enrollment", Action = "View" },
                new Permission { Id = 17, Key = PermissionKeys.EnrollmentManage, Resource = "Enrollment", Action = "Manage" },
                new Permission { Id = 18, Key = PermissionKeys.SchedulingView, Resource = "Scheduling", Action = "View" },
                new Permission { Id = 19, Key = PermissionKeys.SchedulingManage, Resource = "Scheduling", Action = "Manage" },
#pragma warning disable CS0618 // AI30 AC1: retired Scheduling Department permissions retained for seed/DB compatibility
                new Permission { Id = 20, Key = PermissionKeys.SchedulingDepartmentView, Resource = "Scheduling.Department", Action = "View" },
                new Permission { Id = 21, Key = PermissionKeys.SchedulingDepartmentManage, Resource = "Scheduling.Department", Action = "Manage" },
#pragma warning restore CS0618
                new Permission { Id = 22, Key = PermissionKeys.SchedulingRoomAvailabilityView, Resource = "Scheduling.RoomAvailability", Action = "View" },
                new Permission { Id = 23, Key = PermissionKeys.SchedulingRoomAvailabilityManage, Resource = "Scheduling.RoomAvailability", Action = "Manage" },
                new Permission { Id = 24, Key = PermissionKeys.SchedulingFacultyAvailabilityView, Resource = "Scheduling.FacultyAvailability", Action = "View" },
                new Permission { Id = 25, Key = PermissionKeys.SchedulingFacultyAvailabilityManage, Resource = "Scheduling.FacultyAvailability", Action = "Manage" },
                new Permission { Id = 26, Key = PermissionKeys.SchedulingTemplateView, Resource = "Scheduling.Template", Action = "View" },
                new Permission { Id = 27, Key = PermissionKeys.SchedulingTemplateManage, Resource = "Scheduling.Template", Action = "Manage" },
                new Permission { Id = 28, Key = PermissionKeys.SchedulingFacultyPreferencesView, Resource = "Scheduling.FacultyPreferences", Action = "View" },
                new Permission { Id = 29, Key = PermissionKeys.SchedulingFacultyPreferencesManage, Resource = "Scheduling.FacultyPreferences", Action = "Manage" },
                new Permission { Id = 30, Key = PermissionKeys.SchedulingRoomFeaturesView, Resource = "Scheduling.RoomFeatures", Action = "View" },
                new Permission { Id = 31, Key = PermissionKeys.SchedulingRoomFeaturesManage, Resource = "Scheduling.RoomFeatures", Action = "Manage" },
                new Permission { Id = 32, Key = PermissionKeys.SchedulingSubjectDeliveryView, Resource = "Scheduling.SubjectDelivery", Action = "View" },
                new Permission { Id = 33, Key = PermissionKeys.SchedulingSubjectDeliveryManage, Resource = "Scheduling.SubjectDelivery", Action = "Manage" },
                new Permission { Id = 34, Key = PermissionKeys.SchedulingHolidayTypesView, Resource = "Scheduling.HolidayTypes", Action = "View" },
                new Permission { Id = 35, Key = PermissionKeys.SchedulingHolidayTypesManage, Resource = "Scheduling.HolidayTypes", Action = "Manage" },
                new Permission { Id = 36, Key = PermissionKeys.SchedulingTimetableView, Resource = "Scheduling.Timetable", Action = "View" },
                new Permission { Id = 37, Key = PermissionKeys.SchedulingTimetableManage, Resource = "Scheduling.Timetable", Action = "Manage" },
                new Permission { Id = 38, Key = PermissionKeys.SchedulingVersionView, Resource = "Scheduling.Version", Action = "View" },
                new Permission { Id = 39, Key = PermissionKeys.SchedulingVersionManage, Resource = "Scheduling.Version", Action = "Manage" },
                new Permission { Id = 40, Key = PermissionKeys.SchedulingReview, Resource = "Scheduling", Action = "Review" },
                new Permission { Id = 41, Key = PermissionKeys.SchedulingApprove, Resource = "Scheduling", Action = "Approve" },
                new Permission { Id = 42, Key = PermissionKeys.SchedulingPublish, Resource = "Scheduling", Action = "Publish" },
                new Permission { Id = 43, Key = PermissionKeys.SchedulingArchive, Resource = "Scheduling", Action = "Archive" },
                new Permission { Id = 44, Key = PermissionKeys.SchedulingClone, Resource = "Scheduling", Action = "Clone" },
                new Permission { Id = 45, Key = PermissionKeys.SchedulingHistoryView, Resource = "Scheduling.History", Action = "View" },
                new Permission { Id = 46, Key = PermissionKeys.SchedulingVersionCompareView, Resource = "Scheduling.VersionCompare", Action = "View" },
                new Permission { Id = 47, Key = PermissionKeys.SchedulingVersionCompareExport, Resource = "Scheduling.VersionCompare", Action = "Export" },
                new Permission { Id = 48, Key = PermissionKeys.SchedulingApprovalCommentsView, Resource = "Scheduling.ApprovalComments", Action = "View" },
                new Permission { Id = 49, Key = PermissionKeys.SchedulingApprovalCommentsManage, Resource = "Scheduling.ApprovalComments", Action = "Manage" },
                new Permission { Id = 50, Key = PermissionKeys.SchedulingFreeze, Resource = "Scheduling", Action = "Freeze" },
                new Permission { Id = 51, Key = PermissionKeys.SchedulingUnlock, Resource = "Scheduling", Action = "Unlock" },
                new Permission { Id = 52, Key = PermissionKeys.SchedulingArchiveView, Resource = "Scheduling.Archive", Action = "View" },
                new Permission { Id = 53, Key = PermissionKeys.SchedulingArchiveManage, Resource = "Scheduling.Archive", Action = "Manage" },
                new Permission { Id = 54, Key = PermissionKeys.SchedulingConflictView, Resource = "Scheduling.Conflict", Action = "View" },
                new Permission { Id = 55, Key = PermissionKeys.SchedulingConflictManage, Resource = "Scheduling.Conflict", Action = "Manage" },
                new Permission { Id = 210, Key = PermissionKeys.SectionView, Resource = "Section", Action = "View" },
                new Permission { Id = 211, Key = PermissionKeys.SectionCreate, Resource = "Section", Action = "Create" },
                new Permission { Id = 212, Key = PermissionKeys.SectionEdit, Resource = "Section", Action = "Edit" },
                new Permission { Id = 213, Key = PermissionKeys.SectionDelete, Resource = "Section", Action = "Delete" },
                new Permission { Id = 214, Key = PermissionKeys.SectionAssignStudents, Resource = "Section", Action = "AssignStudents" },
                new Permission { Id = 215, Key = PermissionKeys.SectionAssignFaculty, Resource = "Section", Action = "AssignFaculty" },
                new Permission { Id = 220, Key = PermissionKeys.ProgramView, Resource = "Program", Action = "View" },
                new Permission { Id = 221, Key = PermissionKeys.ProgramCreate, Resource = "Program", Action = "Create" },
                new Permission { Id = 222, Key = PermissionKeys.ProgramEdit, Resource = "Program", Action = "Edit" },
                new Permission { Id = 223, Key = PermissionKeys.ProgramDelete, Resource = "Program", Action = "Delete" },
                new Permission { Id = 224, Key = PermissionKeys.ProgramManage, Resource = "Program", Action = "Manage" });

            builder.Entity<ArchiveReasonLookup>().HasData(
                new ArchiveReasonLookup { Id = 701, TenantId = 1, Code = ArchiveReasonCode.Superseded, Name = "Superseded", Description = "Replaced by a newer schedule version", SortOrder = 1, IsActive = true, CreatedDate = SeedUtc, IsDeleted = false },
                new ArchiveReasonLookup { Id = 702, TenantId = 1, Code = ArchiveReasonCode.SemesterComplete, Name = "Semester Complete", Description = "Academic term or semester completed", SortOrder = 2, IsActive = true, CreatedDate = SeedUtc, IsDeleted = false },
                new ArchiveReasonLookup { Id = 703, TenantId = 1, Code = ArchiveReasonCode.Correction, Name = "Correction", Description = "Archived due to corrections", SortOrder = 3, IsActive = true, CreatedDate = SeedUtc, IsDeleted = false },
                new ArchiveReasonLookup { Id = 704, TenantId = 1, Code = ArchiveReasonCode.Emergency, Name = "Emergency", Description = "Emergency operational change", SortOrder = 4, IsActive = true, CreatedDate = SeedUtc, IsDeleted = false },
                new ArchiveReasonLookup { Id = 705, TenantId = 1, Code = ArchiveReasonCode.AcademicCouncil, Name = "Academic Council", Description = "Academic council directive", SortOrder = 5, IsActive = true, CreatedDate = SeedUtc, IsDeleted = false },
                new ArchiveReasonLookup { Id = 706, TenantId = 1, Code = ArchiveReasonCode.Other, Name = "Other", Description = "Other archive reason", SortOrder = 6, IsActive = true, CreatedDate = SeedUtc, IsDeleted = false });

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

            // Include AI30 Phase 2B Conflict (54–55) + AI29 Section (210–215) + AI29.1A Program (220–224)
            var adminLinks = Enumerable.Range(1, 55)
                .Concat(Enumerable.Range(210, 6))
                .Concat(Enumerable.Range(220, 5))
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
