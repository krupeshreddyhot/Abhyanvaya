namespace Abhyanvaya.API.Common
{
    public static class AuthorizationPolicies
    {
        public const string AuthenticatedUser = "AuthenticatedUser";
        public const string TenantScopedUser = "TenantScopedUser";
        public const string AdminOnly = "AdminOnly";
        public const string AdminOrFaculty = "AdminOrFaculty";
        public const string CanViewStudents = "CanViewStudents";
        public const string CanManageStudents = "CanManageStudents";
        public const string CanManageAttendance = "CanManageAttendance";
        public const string CanViewReports = "CanViewReports";

        /// <summary>Role SuperAdmin only (no tenant scope).</summary>
        public const string SuperAdminOnly = "SuperAdminOnly";

        /// <summary>Admin with a valid tenant (college-scoped).</summary>
        public const string TenantScopedAdmin = "TenantScopedAdmin";

        /// <summary>List universities for admin UI or Super Admin org setup.</summary>
        public const string UniversityListAccess = "UniversityListAccess";

        /// <summary>Dashboard overview: tenant Admin/Faculty or Super Admin (zeros).</summary>
        public const string DashboardOverviewAccess = "DashboardOverviewAccess";

        public const string CanManageCourses = "CanManageCourses";
        public const string CanManageGroups = "CanManageGroups";
        public const string CanManageSemesters = "CanManageSemesters";

        // AI29 — Section management
        public const string CanViewSections = "CanViewSections";
        public const string CanCreateSections = "CanCreateSections";
        public const string CanEditSections = "CanEditSections";
        public const string CanDeleteSections = "CanDeleteSections";
        public const string CanAssignSectionStudents = "CanAssignSectionStudents";
        public const string CanAssignSectionFaculty = "CanAssignSectionFaculty";

        // AI29.1A — Program management
        public const string CanViewPrograms = "CanViewPrograms";
        public const string CanCreatePrograms = "CanCreatePrograms";
        public const string CanEditPrograms = "CanEditPrograms";
        public const string CanDeletePrograms = "CanDeletePrograms";
        public const string CanManagePrograms = "CanManagePrograms";

        /// <summary>Tenant college profile, branding, parent linkage (JWT <c>Organization.Manage</c>).</summary>
        public const string CanManageOrganization = "CanManageOrganization";

        /// <summary>College tenant Admin role only (JWT <c>TenantId</c> &gt; 0). Excludes Super Admin.</summary>
        public const string TenantCollegeAdminOnly = "TenantCollegeAdminOnly";

        /// <summary>View enrollment dashboard, batches, and student explorer.</summary>
        public const string CanViewEnrollment = "CanViewEnrollment";

        /// <summary>Create, cancel, and retry enrollment batches.</summary>
        public const string CanManageEnrollment = "CanManageEnrollment";

        /// <summary>View enterprise scheduling foundation data.</summary>
        public const string CanViewScheduling = "CanViewScheduling";

        /// <summary>Manage enterprise scheduling foundation data.</summary>
        public const string CanManageScheduling = "CanManageScheduling";

        /// <summary>Read Catalog Department for Catalog admins and Scheduling consumers (SSOT lookup).</summary>
        public const string CanViewDepartmentLookup = "CanViewDepartmentLookup";

        public const string CanViewSchedulingRoomAvailability = "CanViewSchedulingRoomAvailability";
        public const string CanManageSchedulingRoomAvailability = "CanManageSchedulingRoomAvailability";
        public const string CanViewSchedulingFacultyAvailability = "CanViewSchedulingFacultyAvailability";
        public const string CanManageSchedulingFacultyAvailability = "CanManageSchedulingFacultyAvailability";
        public const string CanViewSchedulingTemplate = "CanViewSchedulingTemplate";
        public const string CanManageSchedulingTemplate = "CanManageSchedulingTemplate";
        public const string CanViewSchedulingFacultyPreferences = "CanViewSchedulingFacultyPreferences";
        public const string CanManageSchedulingFacultyPreferences = "CanManageSchedulingFacultyPreferences";
        public const string CanViewSchedulingRoomFeatures = "CanViewSchedulingRoomFeatures";
        public const string CanManageSchedulingRoomFeatures = "CanManageSchedulingRoomFeatures";
        public const string CanViewSchedulingSubjectDelivery = "CanViewSchedulingSubjectDelivery";
        public const string CanManageSchedulingSubjectDelivery = "CanManageSchedulingSubjectDelivery";
        public const string CanViewSchedulingHolidayTypes = "CanViewSchedulingHolidayTypes";
        public const string CanManageSchedulingHolidayTypes = "CanManageSchedulingHolidayTypes";
        public const string CanViewSchedulingTimetable = "CanViewSchedulingTimetable";
        public const string CanManageSchedulingTimetable = "CanManageSchedulingTimetable";
        public const string CanViewSchedulingVersion = "CanViewSchedulingVersion";
        public const string CanManageSchedulingVersion = "CanManageSchedulingVersion";
        public const string CanReviewScheduling = "CanReviewScheduling";
        public const string CanApproveScheduling = "CanApproveScheduling";
        public const string CanPublishScheduling = "CanPublishScheduling";
        public const string CanArchiveScheduling = "CanArchiveScheduling";
        public const string CanCloneScheduling = "CanCloneScheduling";
        public const string CanViewSchedulingHistory = "CanViewSchedulingHistory";
        public const string CanViewSchedulingGovernanceDashboard = "CanViewSchedulingGovernanceDashboard";
        public const string CanViewSchedulingVersionCompare = "CanViewSchedulingVersionCompare";
        public const string CanExportSchedulingVersionCompare = "CanExportSchedulingVersionCompare";
        public const string CanViewSchedulingApprovalComments = "CanViewSchedulingApprovalComments";
        public const string CanManageSchedulingApprovalComments = "CanManageSchedulingApprovalComments";
        public const string CanFreezeScheduling = "CanFreezeScheduling";
        public const string CanUnlockScheduling = "CanUnlockScheduling";
        public const string CanViewSchedulingArchive = "CanViewSchedulingArchive";
        public const string CanManageSchedulingArchive = "CanManageSchedulingArchive";
        public const string CanViewSchedulingConflict = "CanViewSchedulingConflict";
        public const string CanManageSchedulingConflict = "CanManageSchedulingConflict";
    }
}
