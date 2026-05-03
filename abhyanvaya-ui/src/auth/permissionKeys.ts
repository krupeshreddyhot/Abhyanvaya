/** Mirrors backend `Abhyanvaya.Domain.Authorization.PermissionKeys` (JWT `permission` claims). */
export const PermissionKeys = {
  StudentsView: "Students.View",
  StudentsManage: "Students.Manage",
  AttendanceView: "Attendance.View",
  AttendanceManage: "Attendance.Manage",
  ReportsView: "Reports.View",
  SetupSubjectsManage: "Setup.Subjects.Manage",
  SetupDepartmentsManage: "Setup.Departments.Manage",
  SetupStaffManage: "Setup.Staff.Manage",
  DashboardView: "Dashboard.View",
  OrganizationManage: "Organization.Manage",
  MasterView: "Master.View",
  SetupLookupsManage: "Setup.Lookups.Manage",
  SetupCoursesManage: "Setup.Courses.Manage",
  SetupGroupsManage: "Setup.Groups.Manage",
  SetupSemestersManage: "Setup.Semesters.Manage",
} as const;

export type PermissionKey = (typeof PermissionKeys)[keyof typeof PermissionKeys];
