import { useEffect, useState } from "react";
import { Link as RouterLink } from "react-router-dom";
import {
  Alert,
  Button,
  Chip,
  Stack,
  Typography,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import {
  exportConflictAnalyticsExcel,
  exportConflictAnalyticsPdf,
  getConflictAnalytics,
  type ConflictAnalyticsDashboardDto,
} from "../../../../services/schedulingService";

const downloadBlob = (blob: Blob, filename: string) => {
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
};

const ConflictAnalyticsPage = () => {
  const [data, setData] = useState<ConflictAnalyticsDashboardDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void (async () => {
      try {
        const res = await getConflictAnalytics();
        setData(res.data);
      } catch (e: unknown) {
        setError(e instanceof Error ? e.message : "Failed to load conflict analytics");
      }
    })();
  }, []);

  return (
    <Stack spacing={2}>
      <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />}>
          Hub
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Conflict Analytics
        </Typography>
        <Button
          variant="outlined"
          onClick={() =>
            void exportConflictAnalyticsExcel().then((r) => downloadBlob(r.data, "conflict-analytics.xlsx"))
          }
        >
          Export Excel
        </Button>
        <Button
          variant="outlined"
          onClick={() =>
            void exportConflictAnalyticsPdf().then((r) => downloadBlob(r.data, "conflict-analytics.pdf"))
          }
        >
          Export PDF
        </Button>
      </Stack>
      <Alert severity="info">Historical analytics only — no AI predictions, no optimizer.</Alert>
      {error && <Alert severity="error">{error}</Alert>}
      {data && (
        <>
          <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
            <Chip label={`Runs ${data.totalRuns}`} />
            <Chip label={`Findings ${data.totalHistoricalFindings}`} />
            <Chip label={`Resolution rate ${data.conflictResolutionRatePercent}%`} color="primary" />
            <Chip label={`Avg resolution ${data.averageResolutionTimeHours}h`} />
          </Stack>
          <Typography variant="h6">Top conflict types</Typography>
          <ResponsiveContainer width="100%" height={260}>
            <BarChart data={data.topConflictTypes}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="name" hide />
              <YAxis />
              <Tooltip />
              <Bar dataKey="count" fill="#1976d2" />
            </BarChart>
          </ResponsiveContainer>
          <Typography variant="h6">Weekly comparison</Typography>
          <ResponsiveContainer width="100%" height={260}>
            <LineChart data={data.weeklyComparison}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="dateUtc" tickFormatter={(v) => String(v).slice(0, 10)} />
              <YAxis />
              <Tooltip />
              <Legend />
              <Line type="monotone" dataKey="totalConflicts" stroke="#1976d2" name="Total" />
              <Line type="monotone" dataKey="criticalCount" stroke="#d32f2f" name="Critical" />
              <Line type="monotone" dataKey="warningCount" stroke="#ed6c02" name="Warning" />
            </LineChart>
          </ResponsiveContainer>
          <Typography variant="h6">Monthly comparison</Typography>
          <ResponsiveContainer width="100%" height={260}>
            <BarChart data={data.monthlyComparison}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="dateUtc" tickFormatter={(v) => String(v).slice(0, 7)} />
              <YAxis />
              <Tooltip />
              <Legend />
              <Bar dataKey="totalConflicts" fill="#455a64" name="Total" />
              <Bar dataKey="errorCount" fill="#ef6c00" name="Error" />
            </BarChart>
          </ResponsiveContainer>
          <Typography variant="subtitle1">Faculty / Room / Department trends (top)</Typography>
          <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
            {[
              ["Faculty", data.facultyConflictTrends],
              ["Room", data.roomConflictTrends],
              ["Department", data.departmentConflictTrends],
            ].map(([title, rows]) => (
              <Stack key={String(title)} spacing={0.5} sx={{ flex: 1 }}>
                <Typography variant="subtitle2">{String(title)}</Typography>
                {(rows as { name: string; count: number }[]).slice(0, 8).map((r) => (
                  <Chip key={r.name} size="small" label={`${r.name}: ${r.count}`} sx={{ justifyContent: "flex-start" }} />
                ))}
              </Stack>
            ))}
          </Stack>
        </>
      )}
    </Stack>
  );
};

export default ConflictAnalyticsPage;
