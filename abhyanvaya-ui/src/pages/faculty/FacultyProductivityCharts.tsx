import { Box, Stack, Typography } from "@mui/material";
import {
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
  Legend,
} from "recharts";
import type { FacultyProductivityDashboardDto } from "../../services/facultyWorkspaceService";

type Props = { data: FacultyProductivityDashboardDto };

const FacultyProductivityCharts = ({ data }: Props) => (
  <Stack spacing={2} role="region" aria-label="Faculty productivity charts">
    <Typography variant="body2">
      Classes today {data.classesToday} · Completed {data.attendanceCompleted} · Rate {data.attendanceRate}% · AI{" "}
      {data.aiUsage} · Accuracy {data.recognitionAccuracy?.toFixed(1) ?? "—"}%
    </Typography>
    <Box sx={{ height: 220 }} aria-label="Weekly workload chart">
      <Typography variant="subtitle2">Weekly workload</Typography>
      <ResponsiveContainer width="100%" height="90%">
        <BarChart data={data.weeklyWorkload}>
          <CartesianGrid strokeDasharray="3 3" />
          <XAxis dataKey="label" />
          <YAxis allowDecimals={false} />
          <Tooltip />
          <Legend />
          <Bar dataKey="value" name="Classes" fill="#1976d2" />
        </BarChart>
      </ResponsiveContainer>
    </Box>
    <Box sx={{ height: 220 }} aria-label="Monthly workload chart">
      <Typography variant="subtitle2">Monthly workload</Typography>
      <ResponsiveContainer width="100%" height="90%">
        <BarChart data={data.monthlyWorkload}>
          <CartesianGrid strokeDasharray="3 3" />
          <XAxis dataKey="label" />
          <YAxis allowDecimals={false} />
          <Tooltip />
          <Bar dataKey="value" name="Count" fill="#2e7d32" />
        </BarChart>
      </ResponsiveContainer>
    </Box>
    <Box sx={{ height: 220 }} aria-label="Room utilization chart">
      <Typography variant="subtitle2">Room utilization (today)</Typography>
      <ResponsiveContainer width="100%" height="90%">
        <BarChart data={data.roomUtilization}>
          <CartesianGrid strokeDasharray="3 3" />
          <XAxis dataKey="label" />
          <YAxis allowDecimals={false} />
          <Tooltip />
          <Bar dataKey="value" name="Slots" fill="#ed6c02" />
        </BarChart>
      </ResponsiveContainer>
    </Box>
  </Stack>
);

export default FacultyProductivityCharts;
