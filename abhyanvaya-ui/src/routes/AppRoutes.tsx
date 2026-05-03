import { BrowserRouter, Routes, Route } from "react-router-dom";
import { PermissionKeys } from "../auth/permissionKeys";
import Login from "../pages/Login";
import Dashboard from "../pages/Dashboard";
import ProtectedRoute from "./ProtectedRoute";
import MainLayout from "../layouts/MainLayout";
import OrganizationPage from "../pages/OrganizationPage";
import AttendanceMarking from "../pages/AttendanceMarking";
import StudentsPage from "../pages/StudentsPage";
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
import ReportsPage from "../pages/ReportsPage";
import CollegeProfilePage from "../pages/setup/CollegeProfilePage";
import DepartmentsPage from "../pages/setup/DepartmentsPage";
import StaffPage from "../pages/setup/StaffPage";
import ChangePasswordPage from "../pages/ChangePasswordPage";
import ForgotPasswordPage from "../pages/ForgotPasswordPage";
import ResetPasswordPage from "../pages/ResetPasswordPage";

const AppRoutes = () => {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Login />} />
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
                <StudentsPage />
              </ProtectedRoute>
            }
          />

          <Route
            path="attendance"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.AttendanceManage]}>
                <AttendanceMarking />
              </ProtectedRoute>
            }
          />

          <Route
            path="reports"
            element={
              <ProtectedRoute anyPermission={[PermissionKeys.ReportsView]}>
                <ReportsPage />
              </ProtectedRoute>
            }
          />
        </Route>
      </Routes>
    </BrowserRouter>
  );
};

export default AppRoutes;
