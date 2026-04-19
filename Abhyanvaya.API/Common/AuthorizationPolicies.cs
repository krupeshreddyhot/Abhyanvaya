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
    }
}
