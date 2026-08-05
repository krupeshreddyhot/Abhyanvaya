import { useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import {
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import {
  getEnterpriseAnalytics,
  type EnterpriseOperationalAnalyticsDto,
} from "../../services/enterpriseDashboardService";

/** AI31.6.8 — Operational analytics with Excel/PDF export of composed series. */
const EnterpriseOperationalAnalyticsPage = () => {
  const [data, setData] = useState<EnterpriseOperationalAnalyticsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const load = async () => {
      try {
        const res = await getEnterpriseAnalytics();
        setData(res.data);
      } catch {
        setError("Unable to load operational analytics.");
      } finally {
        setLoading(false);
      }
    };
    void load();
  }, []);

  const csv = useMemo(() => {
    if (!data) return "";
    const rows = [["Series", "Label", "Value"]];
    for (const s of data.series) {
      for (const p of s.points) rows.push([s.title, p.label, String(p.value)]);
    }
    for (const d of data.departmentComparison) {
      rows.push(["Department", d.departmentName, String(d.pendingSessions)]);
    }
    return rows.map((r) => r.map((c) => `"${c.replaceAll('"', '""')}"`).join(",")).join("\n");
  }, [data]);

  const exportExcel = () => {
    const blob = new Blob([csv], { type: "text/csv;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `enterprise-analytics-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  };

  const exportPdf = () => {
    const w = window.open("", "_blank");
    if (!w) return;
    w.document.write(`<html><head><title>Operational Analytics</title></head><body>`);
    w.document.write(`<h1>Enterprise Operational Analytics</h1>`);
    w.document.write(`<pre>${csv.replaceAll("<", "&lt;")}</pre>`);
    w.document.write(`</body></html>`);
    w.document.close();
    w.print();
  };

  if (loading) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", p: 4 }}>
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box>
      <Stack
        direction={{ xs: "column", sm: "row" }}
        spacing={1}
        sx={{ justifyContent: "space-between", mb: 2 }}
      >
        <Box>
          <Typography variant="h4">Operational Analytics</Typography>
          <Typography variant="body2" color="text.secondary">
            Reuses existing analytics services — no duplicate SQL.
          </Typography>
        </Box>
        <Stack direction="row" spacing={1}>
          <Button variant="outlined" onClick={exportExcel}>
            Export Excel (CSV)
          </Button>
          <Button variant="outlined" onClick={exportPdf}>
            Export PDF
          </Button>
        </Stack>
      </Stack>
      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", md: "1fr 1fr" },
          gap: 2,
          mb: 3,
        }}
      >
        {(data?.series ?? []).map((s) => (
          <Paper key={s.code} sx={{ p: 2, height: 280 }}>
            <Typography variant="subtitle1" gutterBottom>
              {s.title}
            </Typography>
            <ResponsiveContainer width="100%" height="85%">
              <BarChart data={s.points}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="label" />
                <YAxis />
                <Tooltip />
                <Bar dataKey="value" fill="#2e7d32" />
              </BarChart>
            </ResponsiveContainer>
          </Paper>
        ))}
      </Box>

      <Typography variant="h6" gutterBottom>
        Department comparison
      </Typography>
      <Paper>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Department</TableCell>
              <TableCell align="right">Pending</TableCell>
              <TableCell align="right">Completed</TableCell>
              <TableCell align="right">Avg completion (min)</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {(data?.departmentComparison ?? []).map((d) => (
              <TableRow key={d.departmentName}>
                <TableCell>{d.departmentName}</TableCell>
                <TableCell align="right">{d.pendingSessions}</TableCell>
                <TableCell align="right">{d.completed}</TableCell>
                <TableCell align="right">{d.averageCompletionMinutes?.toFixed(1) ?? "—"}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Paper>
    </Box>
  );
};

export default EnterpriseOperationalAnalyticsPage;
