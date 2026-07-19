import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutlineOutlined";
import HourglassEmptyIcon from "@mui/icons-material/HourglassEmpty";
import GroupsOutlinedIcon from "@mui/icons-material/GroupsOutlined";
import TodayIcon from "@mui/icons-material/Today";
import { Box, Skeleton } from "@mui/material";
import StatCard from "../common/StatCard";
import { useEnrollmentDashboard } from "../../context/EnrollmentDashboardContext";

const EnrollmentStatistics = () => {
  const { dashboard, loading } = useEnrollmentDashboard();

  if (loading && !dashboard) {
    return (
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr 1fr", sm: "repeat(3, 1fr)", md: "repeat(5, 1fr)" },
          gap: 1.5,
        }}
      >
        {Array.from({ length: 5 }).map((_, i) => (
          <Skeleton key={i} variant="rounded" height={88} aria-hidden />
        ))}
      </Box>
    );
  }

  const stats = [
    { label: "Total Students", value: dashboard?.totalStudents ?? 0, icon: <GroupsOutlinedIcon fontSize="small" /> },
    { label: "Embedded", value: dashboard?.embedded ?? 0, icon: <CheckCircleOutlineIcon fontSize="small" /> },
    { label: "Pending", value: dashboard?.pending ?? 0, icon: <HourglassEmptyIcon fontSize="small" /> },
    {
      label: "Failed",
      value: dashboard?.failed ?? 0,
      icon: <ErrorOutlineIcon fontSize="small" />,
      valueColor: dashboard?.failed ? "error.main" : undefined,
    },
    { label: "Processed Today", value: dashboard?.processedToday ?? 0, icon: <TodayIcon fontSize="small" /> },
  ];

  return (
    <Box
      sx={{
        display: "grid",
        gridTemplateColumns: { xs: "1fr 1fr", sm: "repeat(3, 1fr)", md: "repeat(5, 1fr)" },
        gap: 1.5,
      }}
    >
      {stats.map((stat) => (
        <StatCard key={stat.label} label={stat.label} value={stat.value} icon={stat.icon} valueColor={stat.valueColor} />
      ))}
    </Box>
  );
};

export default EnrollmentStatistics;
