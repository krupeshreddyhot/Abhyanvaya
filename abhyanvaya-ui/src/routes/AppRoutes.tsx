import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import { PermissionKeys } from "../auth/permissionKeys";
import Login from "../pages/Login";
import Dashboard from "../pages/Dashboard";
import ProtectedRoute from "./ProtectedRoute";
import MainLayout from "../layouts/MainLayout";
import OrganizationPage from "../pages/OrganizationPage";
import AttendanceMarking from "../pages/AttendanceMarking";
import FacultyWorkspacePage from "../pages/faculty/FacultyWorkspacePage";
import AttendanceRecognitionReviewPage from "../pages/AttendanceRecognitionReviewPage";
import ContextAwareLayout from "../layouts/ContextAwareLayout";
import StudentsPage from "../pages/StudentsPage";
import ReportsPage from "../pages/ReportsPage";
import SetupHub from "../pages/setup/SetupHub";
import CoursesPage from "../pages/setup/CoursesPage";
import GroupsPage from "../pages/setup/GroupsPage";
import SemestersPage from "../pages/setup/SemestersPage";
import SubjectsPage from "../pages/setup/SubjectsPage";
import LanguagesPage from "../pages/setup/LanguagesPage";
import GendersPage from "../pages/setup/GendersPage";
import MediumsPage from "../pages/setup/MediumsPage";
import ElectiveGroupsPage from "../pages/setup/ElectiveGroupsPage";
import StaffLookupsHub from "../pages/setup/StaffLookupsHub";
import TenantRbacPage from "../pages/setup/TenantRbacPage";
import CollegeProfilePage from "../pages/setup/CollegeProfilePage";
import DepartmentsPage from "../pages/setup/DepartmentsPage";
import StaffPage from "../pages/setup/StaffPage";
import SchedulingHub from "../pages/setup/scheduling/SchedulingHub";
import SchedulingDashboardPage from "../pages/setup/scheduling/SchedulingDashboardPage";
import AcademicYearsPage from "../pages/setup/scheduling/AcademicYearsPage";
import WorkingDaysPage from "../pages/setup/scheduling/WorkingDaysPage";
import HolidaysPage from "../pages/setup/scheduling/HolidaysPage";
import CampusFacilitiesPage from "../pages/setup/scheduling/CampusFacilitiesPage";
import RoomsPage from "../pages/setup/scheduling/RoomsPage";
import TimeSlotsPage from "../pages/setup/scheduling/TimeSlotsPage";
import FacultyWorkloadPage from "../pages/setup/scheduling/FacultyWorkloadPage";
import SubjectAllocationPage from "../pages/setup/scheduling/SubjectAllocationPage";
import RoomRulesPage from "../pages/setup/scheduling/RoomRulesPage";
import FacultyAvailabilityPage from "../pages/setup/scheduling/FacultyAvailabilityPage";
import RoomAvailabilityPage from "../pages/setup/scheduling/RoomAvailabilityPage";
import SubjectCategoriesPage from "../pages/setup/scheduling/SubjectCategoriesPage";
import TimeSlotTemplatesPage from "../pages/setup/scheduling/TimeSlotTemplatesPage";
import FacultyPreferencesPage from "../pages/setup/scheduling/FacultyPreferencesPage";
import RoomFeaturesPage from "../pages/setup/scheduling/RoomFeaturesPage";
import SubjectDeliveryPage from "../pages/setup/scheduling/SubjectDeliveryPage";
import HolidayTypesPage from "../pages/setup/scheduling/HolidayTypesPage";
import TimetableHubPage from "../pages/setup/scheduling/timetable/TimetableHubPage";
import TimetableDesignerPage from "../pages/setup/scheduling/timetable/TimetableDesignerPage";
import FacultyTimetablePage from "../pages/setup/scheduling/timetable/FacultyTimetablePage";
import StudentTimetablePage from "../pages/setup/scheduling/timetable/StudentTimetablePage";
import RoomTimetablePage from "../pages/setup/scheduling/timetable/RoomTimetablePage";
import TimetableDashboardPage from "../pages/setup/scheduling/timetable/TimetableDashboardPage";
import GovernanceDashboardPage from "../pages/setup/scheduling/governance/GovernanceDashboardPage";
import ScheduleVersionsPage from "../pages/setup/scheduling/governance/ScheduleVersionsPage";
import ApprovalQueuePage from "../pages/setup/scheduling/governance/ApprovalQueuePage";
import PublishingPage from "../pages/setup/scheduling/governance/PublishingPage";
import CloneWizardPage from "../pages/setup/scheduling/governance/CloneWizardPage";
import ChangeHistoryPage from "../pages/setup/scheduling/governance/ChangeHistoryPage";
import ConflictDashboardPage from "../pages/setup/scheduling/conflicts/ConflictDashboardPage";
import ConflictWorkspacePage from "../pages/setup/scheduling/conflicts/ConflictWorkspacePage";
import ConflictAnalyticsPage from "../pages/setup/scheduling/conflicts/ConflictAnalyticsPage";
import ConflictRuleThresholdsPage from "../pages/setup/scheduling/conflicts/ConflictRuleThresholdsPage";
import OptimizationPreviewPage from "../pages/setup/scheduling/optimization/OptimizationPreviewPage";
import OptimizationWorkspacePage from "../pages/setup/scheduling/optimization/OptimizationWorkspacePage";
import OptimizationDashboardPage from "../pages/setup/scheduling/optimization/OptimizationDashboardPage";
import ChangePasswordPage from "../pages/ChangePasswordPage";
import ForgotPasswordPage from "../pages/ForgotPasswordPage";
import ResetPasswordPage from "../pages/ResetPasswordPage";
import AiCenterPage from "../pages/ai/AiCenterPage";
import StudentEnrollmentPage from "../pages/ai/StudentEnrollmentPage";
import ContextDiagnosticsPage from "../pages/admin/ContextDiagnosticsPage";

const StudentsPageWithContext = () => (
  <ContextAwareLayout breadcrumbItems={[{ label: "Students" }]}>
    <StudentsPage />
  </ContextAwareLayout>
);

const ReportsPageWithContext = () => (
  <ContextAwareLayout breadcrumbItems={[{ label: "Reports" }]}>
    <ReportsPage />
  </ContextAwareLayout>
);

const AttendancePageWithContext = () => (
  <ContextAwareLayout breadcrumbItems={[{ label: "Attendance" }]}>
    <AttendanceMarking />
  </ContextAwareLayout>
);

const schedulingHubPermissions = [
  PermissionKeys.SchedulingView,
  PermissionKeys.SchedulingManage,
  PermissionKeys.SchedulingRoomAvailabilityView,
  PermissionKeys.SchedulingRoomAvailabilityManage,
  PermissionKeys.SchedulingFacultyAvailabilityView,
  PermissionKeys.SchedulingFacultyAvailabilityManage,
  PermissionKeys.SchedulingTemplateView,
  PermissionKeys.SchedulingTemplateManage,
  PermissionKeys.SchedulingFacultyPreferencesView,
  PermissionKeys.SchedulingFacultyPreferencesManage,
  PermissionKeys.SchedulingRoomFeaturesView,
  PermissionKeys.SchedulingRoomFeaturesManage,
  PermissionKeys.SchedulingSubjectDeliveryView,
  PermissionKeys.SchedulingSubjectDeliveryManage,
  PermissionKeys.SchedulingHolidayTypesView,
  PermissionKeys.SchedulingHolidayTypesManage,
  PermissionKeys.SchedulingTimetableView,
  PermissionKeys.SchedulingTimetableManage,
  PermissionKeys.SchedulingVersionView,
  PermissionKeys.SchedulingVersionManage,
  PermissionKeys.SchedulingReview,
  PermissionKeys.SchedulingApprove,
  PermissionKeys.SchedulingPublish,
  PermissionKeys.SchedulingArchive,
  PermissionKeys.SchedulingClone,
  PermissionKeys.SchedulingHistoryView,
  PermissionKeys.SchedulingConflictView,
  PermissionKeys.SchedulingConflictManage,
];

const AppRoutes = () => {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Login />} />
        {/* Alias so bookmarked/typed /login URLs (e.g. /login?superAdmin=1) render the same
            page as "/" instead of hitting no matching route. */}
        <Route path="/login" element={<Login />} />
        <Route path="/forgot-password" element={<ForgotPasswordPage />} />
        <Route path="/reset-password" element={<ResetPasswordPage />} />
        <Route
          path="/change-password"
          element={
            <ProtectedRoute>
              <ChangePasswordPage />
            </ProtectedRoute>
          }
        />

        <Route
          path="/"
          element={
            <ProtectedRoute>
              <MainLayout />
            </ProtectedRoute>
          }
        >
          <Route
            path="dashboard"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.DashboardView]}>
                <Dashboard />
              </ProtectedRoute>
            }
          />

          <Route
            path="setup"
            element={
              <ProtectedRoute
                allowRoleOrPermission
                allowedRoles={["Admin"]}
                anyPermission={[
                  PermissionKeys.SetupDepartmentsManage,
                  PermissionKeys.SetupStaffManage,
                  PermissionKeys.SetupSubjectsManage,
                  PermissionKeys.SetupLookupsManage,
                  PermissionKeys.SetupCoursesManage,
                  PermissionKeys.SetupGroupsManage,
                  PermissionKeys.SetupSemestersManage,
                  PermissionKeys.OrganizationManage,
                  PermissionKeys.SchedulingView,
                  PermissionKeys.SchedulingManage,
                  PermissionKeys.SchedulingRoomAvailabilityView,
                  PermissionKeys.SchedulingRoomAvailabilityManage,
                  PermissionKeys.SchedulingFacultyAvailabilityView,
                  PermissionKeys.SchedulingFacultyAvailabilityManage,
                  PermissionKeys.SchedulingTemplateView,
                  PermissionKeys.SchedulingTemplateManage,
                  PermissionKeys.SchedulingFacultyPreferencesView,
                  PermissionKeys.SchedulingFacultyPreferencesManage,
                  PermissionKeys.SchedulingRoomFeaturesView,
                  PermissionKeys.SchedulingRoomFeaturesManage,
                  PermissionKeys.SchedulingSubjectDeliveryView,
                  PermissionKeys.SchedulingSubjectDeliveryManage,
                  PermissionKeys.SchedulingHolidayTypesView,
                  PermissionKeys.SchedulingHolidayTypesManage,
                  PermissionKeys.SchedulingTimetableView,
                  PermissionKeys.SchedulingTimetableManage,
                  PermissionKeys.SchedulingVersionView,
                  PermissionKeys.SchedulingVersionManage,
                  PermissionKeys.SchedulingReview,
                  PermissionKeys.SchedulingApprove,
                  PermissionKeys.SchedulingPublish,
                  PermissionKeys.SchedulingArchive,
                  PermissionKeys.SchedulingClone,
                  PermissionKeys.SchedulingHistoryView,
                ]}
              >
                <SetupHub />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/departments"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.SetupDepartmentsManage]}>
                <DepartmentsPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/staff"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.SetupStaffManage]}>
                <StaffPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/courses"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.SetupCoursesManage]}>
                <CoursesPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/groups"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.SetupGroupsManage]}>
                <GroupsPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/semesters"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.SetupSemestersManage]}>
                <SemestersPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/subjects"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.SetupSubjectsManage]}>
                <SubjectsPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/languages"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.SetupLookupsManage]}>
                <LanguagesPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/genders"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.SetupLookupsManage]}>
                <GendersPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/mediums"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.SetupLookupsManage]}>
                <MediumsPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/elective-groups"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.SetupLookupsManage]}>
                <ElectiveGroupsPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/staff-lookups"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.SetupLookupsManage]}>
                <StaffLookupsHub />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/roles"
            element={
              <ProtectedRoute allowedRoles={["Admin"]} requireTenantScope>
                <TenantRbacPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/college"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.OrganizationManage]}>
                <CollegeProfilePage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling"
            element={
              <ProtectedRoute anyPermission={schedulingHubPermissions}>
                <SchedulingHub />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/dashboard"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.SchedulingView, PermissionKeys.SchedulingManage]}>
                <SchedulingDashboardPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/academic-years"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.SchedulingView, PermissionKeys.SchedulingManage]}>
                <AcademicYearsPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/working-days"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.SchedulingView, PermissionKeys.SchedulingManage]}>
                <WorkingDaysPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/holidays"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.SchedulingView, PermissionKeys.SchedulingManage]}>
                <HolidaysPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/campuses"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.SchedulingView, PermissionKeys.SchedulingManage]}>
                <CampusFacilitiesPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/rooms"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.SchedulingView, PermissionKeys.SchedulingManage]}>
                <RoomsPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/time-slots"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.SchedulingView, PermissionKeys.SchedulingManage]}>
                <TimeSlotsPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/faculty-workloads"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.SchedulingView, PermissionKeys.SchedulingManage]}>
                <FacultyWorkloadPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/subject-allocations"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.SchedulingView, PermissionKeys.SchedulingManage]}>
                <SubjectAllocationPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/room-rules"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.SchedulingView, PermissionKeys.SchedulingManage]}>
                <RoomRulesPage />
              </ProtectedRoute>
            }
          />
          {/* AC1: Scheduling Department CRUD removed — redirect bookmarks to Catalog SSOT */}
          <Route path="setup/scheduling/departments" element={<Navigate to="/setup/departments" replace />} />
          <Route
            path="setup/scheduling/faculty-availability"
            element={
              <ProtectedRoute
                anyPermission={[
                  PermissionKeys.SchedulingFacultyAvailabilityView,
                  PermissionKeys.SchedulingFacultyAvailabilityManage,
                ]}
              >
                <FacultyAvailabilityPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/room-availability"
            element={
              <ProtectedRoute
                anyPermission={[
                  PermissionKeys.SchedulingRoomAvailabilityView,
                  PermissionKeys.SchedulingRoomAvailabilityManage,
                ]}
              >
                <RoomAvailabilityPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/subject-categories"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.SchedulingView, PermissionKeys.SchedulingManage]}>
                <SubjectCategoriesPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/time-slot-templates"
            element={
              <ProtectedRoute
                anyPermission={[PermissionKeys.SchedulingTemplateView, PermissionKeys.SchedulingTemplateManage]}
              >
                <TimeSlotTemplatesPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/faculty-preferences"
            element={
              <ProtectedRoute
                anyPermission={[
                  PermissionKeys.SchedulingFacultyPreferencesView,
                  PermissionKeys.SchedulingFacultyPreferencesManage,
                ]}
              >
                <FacultyPreferencesPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/room-features"
            element={
              <ProtectedRoute
                anyPermission={[PermissionKeys.SchedulingRoomFeaturesView, PermissionKeys.SchedulingRoomFeaturesManage]}
              >
                <RoomFeaturesPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/subject-delivery"
            element={
              <ProtectedRoute
                anyPermission={[
                  PermissionKeys.SchedulingSubjectDeliveryView,
                  PermissionKeys.SchedulingSubjectDeliveryManage,
                ]}
              >
                <SubjectDeliveryPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/holiday-types"
            element={
              <ProtectedRoute
                anyPermission={[PermissionKeys.SchedulingHolidayTypesView, PermissionKeys.SchedulingHolidayTypesManage]}
              >
                <HolidayTypesPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/timetables"
            element={
              <ProtectedRoute
                anyPermission={[PermissionKeys.SchedulingTimetableView, PermissionKeys.SchedulingTimetableManage]}
              >
                <TimetableHubPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/timetables/:id"
            element={
              <ProtectedRoute
                anyPermission={[PermissionKeys.SchedulingTimetableView, PermissionKeys.SchedulingTimetableManage]}
              >
                <TimetableDesignerPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/timetable-faculty"
            element={
              <ProtectedRoute
                anyPermission={[PermissionKeys.SchedulingTimetableView, PermissionKeys.SchedulingTimetableManage]}
              >
                <FacultyTimetablePage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/timetable-student"
            element={
              <ProtectedRoute
                anyPermission={[PermissionKeys.SchedulingTimetableView, PermissionKeys.SchedulingTimetableManage]}
              >
                <StudentTimetablePage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/timetable-room"
            element={
              <ProtectedRoute
                anyPermission={[PermissionKeys.SchedulingTimetableView, PermissionKeys.SchedulingTimetableManage]}
              >
                <RoomTimetablePage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/timetable-dashboard"
            element={
              <ProtectedRoute
                anyPermission={[PermissionKeys.SchedulingTimetableView, PermissionKeys.SchedulingTimetableManage]}
              >
                <TimetableDashboardPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/governance/dashboard"
            element={
              <ProtectedRoute
                anyPermission={[PermissionKeys.SchedulingVersionView, PermissionKeys.SchedulingVersionManage]}
              >
                <GovernanceDashboardPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/governance/versions"
            element={
              <ProtectedRoute
                anyPermission={[PermissionKeys.SchedulingVersionView, PermissionKeys.SchedulingVersionManage]}
              >
                <ScheduleVersionsPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/governance/approvals"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.SchedulingReview, PermissionKeys.SchedulingApprove]}>
                <ApprovalQueuePage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/governance/publishing"
            element={
              <ProtectedRoute
                anyPermission={[
                  PermissionKeys.SchedulingPublish,
                  PermissionKeys.SchedulingArchive,
                  PermissionKeys.SchedulingTimetableView,
                ]}
              >
                <PublishingPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/governance/clone"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.SchedulingClone]}>
                <CloneWizardPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/governance/history"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.SchedulingHistoryView]}>
                <ChangeHistoryPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/conflicts/dashboard"
            element={
              <ProtectedRoute
                anyPermission={[PermissionKeys.SchedulingConflictView, PermissionKeys.SchedulingConflictManage]}
              >
                <ConflictDashboardPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/conflicts/workspace"
            element={
              <ProtectedRoute
                anyPermission={[PermissionKeys.SchedulingConflictView, PermissionKeys.SchedulingConflictManage]}
              >
                <ConflictWorkspacePage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/conflicts/analytics"
            element={
              <ProtectedRoute
                anyPermission={[PermissionKeys.SchedulingConflictView, PermissionKeys.SchedulingConflictManage]}
              >
                <ConflictAnalyticsPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/conflicts/rules"
            element={
              <ProtectedRoute
                anyPermission={[PermissionKeys.SchedulingConflictView, PermissionKeys.SchedulingConflictManage]}
              >
                <ConflictRuleThresholdsPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/optimization/preview"
            element={
              <ProtectedRoute
                anyPermission={[PermissionKeys.SchedulingConflictView, PermissionKeys.SchedulingConflictManage]}
              >
                <OptimizationPreviewPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/optimization/workspace"
            element={
              <ProtectedRoute
                anyPermission={[PermissionKeys.SchedulingConflictView, PermissionKeys.SchedulingConflictManage]}
              >
                <OptimizationWorkspacePage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/scheduling/optimization/dashboard"
            element={
              <ProtectedRoute
                anyPermission={[PermissionKeys.SchedulingConflictView, PermissionKeys.SchedulingConflictManage]}
              >
                <OptimizationDashboardPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="admin-setup"
            element={
              <ProtectedRoute allowedRoles={["SuperAdmin"]}>
                <OrganizationPage />
              </ProtectedRoute>
            }
          />

          <Route
            path="students"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.StudentsView]}>
                <StudentsPageWithContext />
              </ProtectedRoute>
            }
          />

          <Route
            path="faculty"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.AttendanceManage]}>
                <ContextAwareLayout breadcrumbItems={[{ label: "Faculty Workspace" }]}>
                  <FacultyWorkspacePage />
                </ContextAwareLayout>
              </ProtectedRoute>
            }
          />

          <Route
            path="attendance"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.AttendanceManage]}>
                <AttendancePageWithContext />
              </ProtectedRoute>
            }
          />

          <Route
            path="attendance/sessions/:sessionId/review"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.AttendanceManage]}>
                <AttendanceRecognitionReviewPage />
              </ProtectedRoute>
            }
          />

          <Route
            path="reports"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.ReportsView]}>
                <ReportsPageWithContext />
              </ProtectedRoute>
            }
          />

          <Route
            path="ai"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.EnrollmentView, PermissionKeys.EnrollmentManage]} allowedRoles={["SuperAdmin"]} allowRoleOrPermission>
                <AiCenterPage />
              </ProtectedRoute>
            }
          />

          <Route
            path="ai/enrollment"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.EnrollmentView, PermissionKeys.EnrollmentManage]} allowedRoles={["SuperAdmin"]} allowRoleOrPermission>
                <StudentEnrollmentPage />
              </ProtectedRoute>
            }
          />

          <Route
            path="admin/context-diagnostics"
            element={
              <ProtectedRoute allowedRoles={["SuperAdmin"]}>
                <ContextDiagnosticsPage />
              </ProtectedRoute>
            }
          />
        </Route>

        {/* Catch-all: any unmatched path (typos, stale deep links, refreshes on an unknown
            route) redirects to the login/dashboard entry point instead of rendering blank. */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
};

export default AppRoutes;
