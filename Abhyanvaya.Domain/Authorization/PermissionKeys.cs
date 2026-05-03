namespace Abhyanvaya.Domain.Authorization
{
    /// <summary>Stable permission keys stored in <see cref="Entities.Permission"/> and JWT claims.</summary>
    public static class PermissionKeys
    {
        public const string StudentsView = "Students.View";
        public const string StudentsManage = "Students.Manage";
        public const string AttendanceView = "Attendance.View";
        public const string AttendanceManage = "Attendance.Manage";
        public const string ReportsView = "Reports.View";
        public const string SetupSubjectsManage = "Setup.Subjects.Manage";
        public const string SetupDepartmentsManage = "Setup.Departments.Manage";
        public const string SetupStaffManage = "Setup.Staff.Manage";
        public const string DashboardView = "Dashboard.View";
        public const string OrganizationManage = "Organization.Manage";
        public const string MasterView = "Master.View";
        public const string SetupLookupsManage = "Setup.Lookups.Manage";
        public const string SetupCoursesManage = "Setup.Courses.Manage";
        public const string SetupGroupsManage = "Setup.Groups.Manage";
        public const string SetupSemestersManage = "Setup.Semesters.Manage";

        public static IReadOnlyList<string> All { get; } =
        [
            StudentsView,
            StudentsManage,
            AttendanceView,
            AttendanceManage,
            ReportsView,
            SetupSubjectsManage,
            SetupDepartmentsManage,
            SetupStaffManage,
            DashboardView,
            OrganizationManage,
            MasterView,
            SetupLookupsManage,
            SetupCoursesManage,
            SetupGroupsManage,
            SetupSemestersManage
        ];

        /// <summary>Fallback when <see cref="Entities.UserApplicationRole"/> rows are absent (legacy enum roles).</summary>
        public static IReadOnlyList<string> LegacyFacultySet { get; } =
        [
            StudentsView,
            AttendanceView,
            AttendanceManage,
            ReportsView,
            DashboardView,
            MasterView
        ];
    }
}
