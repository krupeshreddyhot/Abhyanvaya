import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutlineOutlined";
import HourglassEmptyIcon from "@mui/icons-material/HourglassEmpty";
import GroupsOutlinedIcon from "@mui/icons-material/GroupsOutlined";
import TodayIcon from "@mui/icons-material/Today";
import PhotoCameraOutlinedIcon from "@mui/icons-material/PhotoCameraOutlined";
import { Box, Skeleton, Typography } from "@mui/material";
import StatCard from "../common/StatCard";
import { useEnrollmentDashboard } from "../../context/EnrollmentDashboardContext";

const EnrollmentStatistics = () => {
  const { dashboard, loading } = useEnrollmentDashboard();

  if (loading && !dashboard) {
    return (
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr 1fr", sm: "repeat(3, 1fr)", md: "repeat(6, 1fr)" },
          gap: 1.5,
        }}
      >
        {Array.from({ length: 6 }).map((_, i) => (
          <Skeleton key={i} variant="rounded" height={88} aria-hidden />
        ))}
      </Box>
    );
  }

  const stats = [
    { label: "Total Students", value: dashboard?.totalStudents ?? 0, icon: <GroupsOutlinedIcon fontSize="small" /> },
    { label: "Embedded", value: dashboard?.embedded ?? 0, icon: <CheckCircleOutlineIcon fontSize="small" /> },
    {
      label: "No Embedding",
      value: dashboard?.uploadedWithoutEmbedding ?? 0,
      icon: <PhotoCameraOutlinedIcon fontSize="small" />,
      valueColor: dashboard?.uploadedWithoutEmbedding ? "warning.main" : undefined,
    },
    { label: "Pending", value: dashboard?.pending ?? 0, icon: <HourglassEmptyIcon fontSize="small" /> },
    { label: "Failed (All Batches)", value: dashboard?.failed ?? 0, icon: <ErrorOutlineIcon fontSize="small" />, valueColor: dashboard?.failed ? "error.main" : undefined },
    { label: "Processed Today", value: dashboard?.processedToday ?? 0, icon: <TodayIcon fontSize="small" /> },
  ];

  return (
    <Box
      sx={{
        display: "grid",
        gridTemplateColumns: { xs: "1fr 1fr", sm: "repeat(3, 1fr)", md: "repeat(6, 1fr)" },
        gap: 1.5,
      }}
    >
      <Typography
        variant="caption"
        color="text.secondary"
        sx={{ gridColumn: "1 / -1", mb: -0.5 }}
      >
        College totals across all enrollment batches (not limited to the running batch)
      </Typography>
      {stats.map((stat) => (
        <StatCard key={stat.label} label={stat.label} value={stat.value} icon={stat.icon} valueColor={stat.valueColor} />
      ))}
    </Box>
  );
};

export default EnrollmentStatistics;
