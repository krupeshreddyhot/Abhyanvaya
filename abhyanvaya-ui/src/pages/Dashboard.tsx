import { useEffect, useMemo, useState } from "react";
import { Alert, Box, CircularProgress, Paper, Typography } from "@mui/material";
import PeopleIcon from "@mui/icons-material/People";
import EventNoteIcon from "@mui/icons-material/EventNote";
import SchoolIcon from "@mui/icons-material/School";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import PercentIcon from "@mui/icons-material/Percent";
import PersonOffIcon from "@mui/icons-material/PersonOff";
import { getDashboardOverview, type DashboardOverviewDto } from "../services/dashboardService";
import { useAuth } from "../context/AuthContext";

const Dashboard = () => {
  const { user } = useAuth();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [data, setData] = useState<DashboardOverviewDto | null>(null);

  useEffect(() => {
    const load = async () => {
      setLoading(true);
      setError(null);
      try {
        const res = await getDashboardOverview();
        setData(res.data);
      } catch {
        setError("Unable to load dashboard data.");
      } finally {
        setLoading(false);
      }
    };
    void load();
  }, []);

  const stats = useMemo(() => {
    if (!data) return [];
    return [
      {
        title: "Total Students",
        value: data.totalStudents,
        icon: <PeopleIcon fontSize="large" />,
        color: "#1976d2",
      },
      {
        title: "Total Subjects",
        value: data.totalSubjects,
        icon: <SchoolIcon fontSize="large" />,
        color: "#ed6c02",
      },
      {
        title: "Overall Attendance %",
        value: `${data.overallPercentage.toFixed(2)}%`,
        icon: <PercentIcon fontSize="large" />,
        color: "#2e7d32",
      },
      {
        title: "Today Present",
        value: data.todayPresent,
        icon: <CheckCircleIcon fontSize="large" />,
        color: "#9c27b0",
      },
      {
        title: "Today Absent",
        value: data.todayAbsent,
        icon: <PersonOffIcon fontSize="large" />,
        color: "#d32f2f",
      },
      {
        title: "Total Attendance Entries",
        value: data.totalAttendance,
        icon: <EventNoteIcon fontSize="large" />,
        color: "#455a64",
      },
    ];
  }, [data]);

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Dashboard
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Role: {user?.role || "User"}
      </Typography>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      {loading && (
        <Box sx={{ display: "flex", justifyContent: "center", p: 3 }}>
          <CircularProgress />
        </Box>
      )}

      <Box
        sx={{
          opacity: loading ? 0.6 : 1,
          display: "grid",
          gridTemplateColumns: {
            xs: "1fr",
            sm: "1fr 1fr",
            md: "1fr 1fr 1fr",
          },
          gap: 3,
        }}
      >
        {stats.map((item, index) => (
          <Paper
            key={index}
            sx={{
              p: 3,
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              borderLeft: `5px solid ${item.color}`,
            }}
          >
            <Box>
              <Typography variant="subtitle2" color="text.secondary">
                {item.title}
              </Typography>

              <Typography variant="h5" sx={{ fontWeight: "bold" }}>
                {item.value}
              </Typography>
            </Box>

            <Box sx={{ color: item.color }}>{item.icon}</Box>
          </Paper>
        ))}
      </Box>
    </Box>
  );
};

export default Dashboard;
