import { useEffect, useState } from "react";
import { CircularProgress, Box } from "@mui/material";
import { Navigate } from "react-router-dom";
import { useAuth } from "../../context/AuthContext";
import { getDashboardPreferences } from "../../services/enterpriseDashboardService";
import FacultyCommandCenterPage from "./FacultyCommandCenterPage";
import AdminOperationsDashboardPage from "./AdminOperationsDashboardPage";

/**
 * AI31.6 landing — Faculty → Command Center, Admin → Enterprise Operations.
 * Honors DB-persisted default landing page when set.
 */
const RoleAwareDashboardPage = () => {
  const { user } = useAuth();
  const role = (user?.role ?? "").toLowerCase();
  const isAdmin = role === "admin" || role === "superadmin";
  const [redirect, setRedirect] = useState<string | null>(null);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    const scope = isAdmin ? "Admin" : "Faculty";
    void getDashboardPreferences(scope)
      .then((r) => {
        const landing = r.data.defaultLandingPage;
        if (landing === "faculty-workspace") setRedirect("/faculty");
        else if (landing === "analytics") setRedirect("/dashboard/analytics");
        else if (landing === "health") setRedirect("/dashboard/health");
        else if (landing === "notifications") setRedirect("/dashboard/notifications");
        else if (landing === "admin-operations" && isAdmin) setRedirect(null);
        else if (landing === "command-center" && !isAdmin) setRedirect(null);
        else setRedirect(null);
      })
      .catch(() => setRedirect(null))
      .finally(() => setReady(true));
  }, [isAdmin]);

  if (!ready) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", p: 4 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (redirect) return <Navigate to={redirect} replace />;
  return isAdmin ? <AdminOperationsDashboardPage /> : <FacultyCommandCenterPage />;
};

export default RoleAwareDashboardPage;
