/** AI31.7 / AI31.7.5 — reusable drill-down paths for Enterprise Operations Command Center. */
export const DashboardDrilldown = {
  attendanceRecovery: "/setup/attendance-recovery",
  conflictWorkspace: "/setup/scheduling/conflicts/workspace",
  approvalQueue: "/setup/scheduling/governance/approvals",
  optimizationWorkspace: "/setup/scheduling/optimization/workspace",
  catalogRooms: "/setup/scheduling/rooms",
  staff: "/setup/staff",
  students: "/students",
  schedulingHub: "/setup/scheduling",
  healthCenter: "/dashboard/health",
  notifications: "/dashboard/notifications",
  reports: "/reports",
  takeAttendance: "/attendance",
  academicYears: "/setup/scheduling/academic-years",
  versions: "/setup/scheduling/governance/versions",
  publishing: "/setup/scheduling/governance/publishing",
} as const;

export function resolveDrilldownPath(widgetCode: string, fallback?: string | null): string {
  const map: Record<string, string> = {
    "attendance-review-queue": DashboardDrilldown.attendanceRecovery,
    "attendance-recovery-queue": DashboardDrilldown.attendanceRecovery,
    "ai-recognition-pending": DashboardDrilldown.attendanceRecovery,
    "ai-recognition-queue": DashboardDrilldown.attendanceRecovery,
    "sessions-running": DashboardDrilldown.attendanceRecovery,
    "recognition-in-progress": DashboardDrilldown.attendanceRecovery,
    "recognition-failed": DashboardDrilldown.attendanceRecovery,
    "completed-today": DashboardDrilldown.attendanceRecovery,
    "scheduling-issues": DashboardDrilldown.conflictWorkspace,
    "timetable-approval-queue": DashboardDrilldown.approvalQueue,
    "pending-timetable-approvals": DashboardDrilldown.approvalQueue,
    "pending-timetable-approval": DashboardDrilldown.approvalQueue,
    "optimization-suggestions": DashboardDrilldown.optimizationWorkspace,
    rooms: DashboardDrilldown.catalogRooms,
    laboratories: DashboardDrilldown.catalogRooms,
    faculty: DashboardDrilldown.staff,
    students: DashboardDrilldown.students,
    "critical-system-alerts": DashboardDrilldown.healthCenter,
    "api-status": DashboardDrilldown.healthCenter,
    "database-status": DashboardDrilldown.healthCenter,
    "active-academic-year": DashboardDrilldown.academicYears,
    "active-timetable-version": DashboardDrilldown.versions,
    "current-schedule-version": DashboardDrilldown.versions,
    "last-published": DashboardDrilldown.publishing,
  };
  return map[widgetCode] ?? fallback ?? DashboardDrilldown.schedulingHub;
}
