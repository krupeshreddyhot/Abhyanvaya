import { BrowserRouter, Routes, Route } from "react-router-dom";
import Login from "../pages/Login";
import Dashboard from "../pages/Dashboard";
import ProtectedRoute from "./ProtectedRoute";
import MainLayout from "../layouts/MainLayout";
import AdminSetup from "../pages/AdminSetup";
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
import ReportsPage from "../pages/ReportsPage";

const AppRoutes = () => {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Login />} />

        <Route
          path="/"
          element={
            <ProtectedRoute>
              <MainLayout />
            </ProtectedRoute>
          }
        >
          <Route path="dashboard" element={<Dashboard />} />

          <Route
            path="setup"
            element={
              <ProtectedRoute allowedRoles={["Admin"]}>
                <SetupHub />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/courses"
            element={
              <ProtectedRoute allowedRoles={["Admin"]}>
                <CoursesPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/groups"
            element={
              <ProtectedRoute allowedRoles={["Admin"]}>
                <GroupsPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/semesters"
            element={
              <ProtectedRoute allowedRoles={["Admin"]}>
                <SemestersPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/subjects"
            element={
              <ProtectedRoute allowedRoles={["Admin"]}>
                <SubjectsPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/languages"
            element={
              <ProtectedRoute allowedRoles={["Admin"]}>
                <LanguagesPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/genders"
            element={
              <ProtectedRoute allowedRoles={["Admin"]}>
                <GendersPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/mediums"
            element={
              <ProtectedRoute allowedRoles={["Admin"]}>
                <MediumsPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="setup/elective-groups"
            element={
              <ProtectedRoute allowedRoles={["Admin"]}>
                <ElectiveGroupsPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="admin-setup"
            element={
              <ProtectedRoute allowedRoles={["Admin"]}>
                <AdminSetup />
              </ProtectedRoute>
            }
          />

          <Route
            path="students"
            element={
              <ProtectedRoute allowedRoles={["Admin"]}>
                <StudentsPage />
              </ProtectedRoute>
            }
          />

          <Route
            path="attendance"
            element={
              <ProtectedRoute allowedRoles={["Admin", "Faculty"]}>
                <AttendanceMarking />
              </ProtectedRoute>
            }
          />

          <Route
            path="reports"
            element={
              <ProtectedRoute allowedRoles={["Admin", "Faculty"]}>
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
