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

        /// <summary>Tenant college profile, branding, parent linkage (JWT <c>Organization.Manage</c>).</summary>
        public const string CanManageOrganization = "CanManageOrganization";
    }
}
