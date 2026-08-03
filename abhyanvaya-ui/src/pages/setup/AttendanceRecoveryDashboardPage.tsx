import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import {
  adminRecoveryAction,
  exportAdminRecoveryCsv,
  getAdminHealthSnapshot,
  getAdminOperationalAnalytics,
  getAdminOperationsDashboard,
  getAdminRecoveryAnalytics,
  getAdminRecoveryDashboard,
  type AttendanceHealthSnapshot,
  type AttendanceOperationalAnalytics,
  type AttendanceOperationsDashboard,
  type AttendanceRecoveryAnalytics,
  type AttendanceRecoveryDashboard,
} from "../../services/attendanceRecoveryService";

const ChartBox = ({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) => (
  <Box sx={{ flex: "1 1 320px", minHeight: 280, p: 1, border: 1, borderColor: "divider", borderRadius: 1 }}>
    <Typography variant="subtitle2" sx={{ mb: 1 }}>
      {title}
    </Typography>
    {children}
  </Box>
);

const AttendanceRecoveryDashboardPage = () => {
  const [dash, setDash] = useState<AttendanceRecoveryDashboard | null>(null);
  const [analytics, setAnalytics] = useState<AttendanceRecoveryAnalytics | null>(null);
  const [ops, setOps] = useState<AttendanceOperationsDashboard | null>(null);
  const [opsAnalytics, setOpsAnalytics] = useState<AttendanceOperationalAnalytics | null>(null);
  const [health, setHealth] = useState<AttendanceHealthSnapshot | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    void (async () => {
      setLoading(true);
      try {
        const [d, a, o, oa, h] = await Promise.all([
          getAdminRecoveryDashboard(),
          getAdminRecoveryAnalytics(),
          getAdminOperationsDashboard(),
          getAdminOperationalAnalytics(),
          getAdminHealthSnapshot(),
        ]);
        setDash(d.data);
        setAnalytics(a.data);
        setOps(o.data);
        setOpsAnalytics(oa.data);
        setHealth(h.data);
      } catch {
        setError("Failed to load recovery operations dashboard.");
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  const exportExcel = async () => {
    const res = await exportAdminRecoveryCsv();
    const url = URL.createObjectURL(res.data);
    const a = document.createElement("a");
    a.href = url;
    a.download = "attendance-recovery.xls";
    a.click();
    URL.revokeObjectURL(url);
  };

  if (loading) return <CircularProgress />;

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup" startIcon={<ArrowBackIcon />}>
          Setup
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Attendance operations
        </Typography>
        <Button variant="outlined" onClick={() => void exportExcel()}>
          Export Excel
        </Button>
      </Box>
      {error && <Alert severity="error">{error}</Alert>}

      {dash && (
        <Stack direction="row" spacing={2} useFlexGap sx={{ flexWrap: "wrap" }}>
          {[
            ["Today", dash.todayCount],
            ["Yesterday", dash.yesterdayCount],
            ["Processing", dash.processingCount],
            ["Failed", dash.failedCount],
            ["Review pending", dash.reviewPendingCount],
            ["Finalization pending", dash.finalizationPendingCount],
            ["Expired", dash.expiredCount],
          ].map(([label, value]) => (
            <Box key={String(label)} sx={{ p: 2, border: 1, borderColor: "divider", borderRadius: 1, minWidth: 120 }}>
              <Typography variant="caption">{label}</Typography>
              <Typography variant="h5">{value}</Typography>
            </Box>
          ))}
        </Stack>
      )}

      {ops && (
        <Alert severity="info">
          Avg review {ops.averageReviewTimeMinutes?.toFixed(1) ?? "—"}m · Failure{" "}
          {ops.recognitionFailureRatePercent.toFixed(1)}% · Retry success {ops.retrySuccessRatePercent.toFixed(1)}% ·
          Finalization SLA {ops.finalizationSlaPercent.toFixed(1)}%
        </Alert>
      )}

      {health && (
        <Alert severity={health.alerts.some((a) => a.severity === "critical") ? "warning" : "success"}>
          Health: stalled recognition {health.recognitionStalled} · stalled review {health.reviewStalled} · abandoned{" "}
          {health.abandoned} · repeated failures {health.repeatedFailures} · large queues {health.largePendingQueues} ·
          long running {health.longRunning}
          {health.neverAutoCancels ? " · never auto-cancels" : ""}
        </Alert>
      )}

      <Stack direction={{ xs: "column", md: "row" }} spacing={2} useFlexGap sx={{ flexWrap: "wrap" }}>
        {ops && ops.sessionsByStatus.length > 0 && (
          <ChartBox title="Sessions by status">
            <ResponsiveContainer width="100%" height={240}>
              <BarChart data={ops.sessionsByStatus}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                <YAxis allowDecimals={false} />
                <Tooltip />
                <Bar dataKey="value" fill="#1565c0" name="Sessions" />
              </BarChart>
            </ResponsiveContainer>
          </ChartBox>
        )}
        {ops && ops.facultyProductivity.length > 0 && (
          <ChartBox title="Faculty productivity">
            <ResponsiveContainer width="100%" height={240}>
              <BarChart data={ops.facultyProductivity}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                <YAxis allowDecimals={false} />
                <Tooltip />
                <Bar dataKey="value" fill="#2e7d32" name="Completed" />
              </BarChart>
            </ResponsiveContainer>
          </ChartBox>
        )}
        {ops && ops.departmentDistribution.length > 0 && (
          <ChartBox title="Department distribution">
            <ResponsiveContainer width="100%" height={240}>
              <BarChart data={ops.departmentDistribution}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                <YAxis allowDecimals={false} />
                <Tooltip />
                <Bar dataKey="value" fill="#6d4c41" name="Sessions" />
              </BarChart>
            </ResponsiveContainer>
          </ChartBox>
        )}
        {ops && ops.roomDistribution.length > 0 && (
          <ChartBox title="Course / room distribution">
            <ResponsiveContainer width="100%" height={240}>
              <BarChart data={ops.roomDistribution}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                <YAxis allowDecimals={false} />
                <Tooltip />
                <Bar dataKey="value" fill="#00838f" name="Sessions" />
              </BarChart>
            </ResponsiveContainer>
          </ChartBox>
        )}
        {ops && ops.topBusyFaculty.length > 0 && (
          <ChartBox title="Top busy faculty">
            <ResponsiveContainer width="100%" height={240}>
              <BarChart data={ops.topBusyFaculty}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                <YAxis allowDecimals={false} />
                <Tooltip />
                <Bar dataKey="value" fill="#ef6c00" name="Load" />
              </BarChart>
            </ResponsiveContainer>
          </ChartBox>
        )}
        {opsAnalytics && opsAnalytics.dailyTrends.length > 0 && (
          <ChartBox title="Daily trends">
            <ResponsiveContainer width="100%" height={240}>
              <LineChart data={opsAnalytics.dailyTrends}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                <YAxis allowDecimals={false} />
                <Tooltip />
                <Line type="monotone" dataKey="value" stroke="#4527a0" strokeWidth={2} name="Started" />
              </LineChart>
            </ResponsiveContainer>
          </ChartBox>
        )}
        {analytics && analytics.pendingTrend.length > 0 && (
          <ChartBox title="Pending trend">
            <ResponsiveContainer width="100%" height={240}>
              <LineChart data={analytics.pendingTrend}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                <YAxis allowDecimals={false} />
                <Tooltip />
                <Line type="monotone" dataKey="value" stroke="#2e7d32" name="Pending" strokeWidth={2} />
              </LineChart>
            </ResponsiveContainer>
          </ChartBox>
        )}
      </Stack>

      {opsAnalytics && (
        <Alert severity="info">
          Operational analytics (read-only): started {opsAnalytics.sessionsStarted} · completed{" "}
          {opsAnalytics.sessionsCompleted} · retry {opsAnalytics.retryPercent.toFixed(1)}% · failure{" "}
          {opsAnalytics.failurePercent.toFixed(1)}% · resume {opsAnalytics.resumePercent.toFixed(1)}% · peak{" "}
          {opsAnalytics.peakUsageLabel ?? "—"} · avg recognition {opsAnalytics.averageRecognitionMinutes?.toFixed(1) ?? "—"}
          m · avg review {opsAnalytics.averageReviewMinutes?.toFixed(1) ?? "—"}m · avg finalize{" "}
          {opsAnalytics.averageFinalizationMinutes?.toFixed(1) ?? "—"}m
        </Alert>
      )}

      {ops && ops.longestRunningSessions.length > 0 && (
        <Box>
          <Typography variant="subtitle1" sx={{ mb: 1, fontWeight: 700 }}>
            Longest running sessions
          </Typography>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Subject</TableCell>
                <TableCell>Staff</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Priority</TableCell>
                <TableCell>Elapsed</TableCell>
                <TableCell align="right">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {ops.longestRunningSessions.map((s) => (
                <TableRow key={s.sessionId} hover>
                  <TableCell>{s.displayTitle || s.subjectName || s.subjectId}</TableCell>
                  <TableCell>{s.staffName ?? s.staffId ?? "—"}</TableCell>
                  <TableCell>{s.friendlyWorkflowLabel || s.workflowStatusName}</TableCell>
                  <TableCell>{s.priorityScore ?? "—"}</TableCell>
                  <TableCell>{s.elapsedMinutes.toFixed(0)}m</TableCell>
                  <TableCell align="right">
                    <Button size="small" onClick={() => void adminRecoveryAction(s.sessionId, "restore")}>
                      Restore
                    </Button>
                    <Button size="small" color="warning" onClick={() => void adminRecoveryAction(s.sessionId, "archive")}>
                      Archive
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>
      )}

      {dash && (
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Session</TableCell>
              <TableCell>Subject</TableCell>
              <TableCell>Staff</TableCell>
              <TableCell>Workflow</TableCell>
              <TableCell>Priority</TableCell>
              <TableCell>Elapsed</TableCell>
              <TableCell>Retries</TableCell>
              <TableCell>Failure</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {dash.sessions.map((s) => (
              <TableRow key={s.sessionId} hover>
                <TableCell>{s.sessionId.slice(0, 8)}…</TableCell>
                <TableCell>{s.displayTitle || s.subjectName || s.subjectId}</TableCell>
                <TableCell>{s.staffName ?? s.staffId ?? "—"}</TableCell>
                <TableCell>{s.friendlyWorkflowLabel || s.workflowStatusName}</TableCell>
                <TableCell>{s.priorityScore ?? "—"}</TableCell>
                <TableCell>{s.elapsedMinutes.toFixed(0)}m</TableCell>
                <TableCell>{s.retryCount}</TableCell>
                <TableCell>{s.failureReason ?? "—"}</TableCell>
                <TableCell align="right">
                  <Button size="small" onClick={() => void adminRecoveryAction(s.sessionId, "restore")}>
                    Restore
                  </Button>
                  <Button size="small" color="warning" onClick={() => void adminRecoveryAction(s.sessionId, "archive")}>
                    Archive
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </Stack>
  );
};

export default AttendanceRecoveryDashboardPage;
