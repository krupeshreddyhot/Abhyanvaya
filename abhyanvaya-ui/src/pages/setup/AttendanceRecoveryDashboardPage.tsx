import { useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Checkbox,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  LinearProgress,
  MenuItem,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
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
import SessionTimeline from "../../components/attendance-recovery/SessionTimeline";
import {
  AttendanceBulkOperationKind,
  adminRecoveryAction,
  exportAdminRecoveryCsv,
  getAdminDepartmentOperations,
  getAdminEnterpriseOps,
  getAdminHealthSnapshot,
  getAdminOperationalAnalytics,
  getAdminOperationsDashboard,
  getAdminRecoveryAnalytics,
  getAdminRecoveryDashboard,
  getSessionTimeline,
  runAdminBulkOperation,
  type AttendanceHealthSnapshot,
  type AttendanceOperationalAnalytics,
  type AttendanceOperationsDashboard,
  type AttendanceRecoveryAnalytics,
  type AttendanceRecoveryDashboard,
  type DepartmentOperationsDashboard,
  type EnterpriseOpsDashboard,
  type SessionTimeline as SessionTimelineDto,
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
  const [enterprise, setEnterprise] = useState<EnterpriseOpsDashboard | null>(null);
  const [departments, setDepartments] = useState<DepartmentOperationsDashboard | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [bulkOp, setBulkOp] = useState<number>(AttendanceBulkOperationKind.NotifyFaculty);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [progressOpen, setProgressOpen] = useState(false);
  const [progressMsg, setProgressMsg] = useState("");
  const [timeline, setTimeline] = useState<SessionTimelineDto | null>(null);
  const [timelineSessionId, setTimelineSessionId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    void (async () => {
      setLoading(true);
      try {
        const [d, a, o, oa, h, e, dep] = await Promise.all([
          getAdminRecoveryDashboard(),
          getAdminRecoveryAnalytics(),
          getAdminOperationsDashboard(),
          getAdminOperationalAnalytics(),
          getAdminHealthSnapshot(),
          getAdminEnterpriseOps().catch(() => null),
          getAdminDepartmentOperations().catch(() => null),
        ]);
        setDash(d.data);
        setAnalytics(a.data);
        setOps(o.data);
        setOpsAnalytics(oa.data);
        setHealth(h.data);
        setEnterprise(e?.data ?? null);
        setDepartments(dep?.data ?? null);
      } catch {
        setError("Failed to load recovery operations dashboard.");
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  const allSessionIds = useMemo(
    () => (dash?.sessions ?? []).map((s) => s.sessionId),
    [dash],
  );

  const toggle = (id: string) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const exportExcel = async () => {
    const res = await exportAdminRecoveryCsv();
    const url = URL.createObjectURL(res.data);
    const a = document.createElement("a");
    a.href = url;
    a.download = "attendance-recovery.xls";
    a.click();
    URL.revokeObjectURL(url);
  };

  const runBulk = async () => {
    setConfirmOpen(false);
    setProgressOpen(true);
    setProgressMsg("Running bulk operation…");
    try {
      const res = await runAdminBulkOperation(bulkOp, [...selected]);
      setProgressMsg(
        `${res.data.operation}: ok ${res.data.succeededCount} · skipped ${res.data.skippedCount} · failed ${res.data.failedCount}`,
      );
      setSelected(new Set());
    } catch {
      setProgressMsg("Bulk operation failed.");
    }
  };

  const openTimeline = async (sessionId: string) => {
    setTimelineSessionId(sessionId);
    try {
      const res = await getSessionTimeline(sessionId);
      setTimeline(res.data);
    } catch {
      setTimeline(null);
    }
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
          {enterprise?.averageReviewTimeMinutes != null
            ? ` · Enterprise avg review ${enterprise.averageReviewTimeMinutes.toFixed(1)}m`
            : ""}
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

      <Box sx={{ p: 2, border: 1, borderColor: "divider", borderRadius: 1 }}>
        <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 1 }}>
          Bulk operations (admin assist)
        </Typography>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={1} sx={{ alignItems: { sm: "center" } }}>
          <TextField
            select
            size="small"
            label="Operation"
            value={bulkOp}
            onChange={(e) => setBulkOp(Number(e.target.value))}
            sx={{ minWidth: 240 }}
          >
            <MenuItem value={AttendanceBulkOperationKind.NotifyFaculty}>Notify faculty</MenuItem>
            <MenuItem value={AttendanceBulkOperationKind.ArchiveExpired}>Archive expired</MenuItem>
            <MenuItem value={AttendanceBulkOperationKind.ExportSessions}>Export sessions</MenuItem>
            <MenuItem value={AttendanceBulkOperationKind.RetryFailedRecognition}>Retry failed recognition</MenuItem>
            <MenuItem value={AttendanceBulkOperationKind.MarkReviewed}>Mark reviewed</MenuItem>
            <MenuItem value={AttendanceBulkOperationKind.CloseCompleted}>Close completed</MenuItem>
          </TextField>
          <Button size="small" onClick={() => setSelected(new Set(allSessionIds))}>
            Select all ({allSessionIds.length})
          </Button>
          <Button size="small" onClick={() => setSelected(new Set())}>
            Clear
          </Button>
          <Button
            variant="contained"
            disabled={selected.size === 0}
            onClick={() => setConfirmOpen(true)}
          >
            Run on {selected.size}
          </Button>
        </Stack>
        <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 1 }}>
          Never auto-finalizes attendance · Never retries successful sessions
        </Typography>
      </Box>

      <Stack direction={{ xs: "column", md: "row" }} spacing={2} useFlexGap sx={{ flexWrap: "wrap" }}>
        {enterprise && enterprise.slaDistribution.length > 0 && (
          <ChartBox title="SLA distribution">
            <ResponsiveContainer width="100%" height={240}>
              <BarChart data={enterprise.slaDistribution}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                <YAxis allowDecimals={false} />
                <Tooltip />
                <Bar dataKey="value" fill="#c62828" name="Sessions" />
              </BarChart>
            </ResponsiveContainer>
          </ChartBox>
        )}
        {enterprise && enterprise.facultySla.length > 0 && (
          <ChartBox title="Faculty SLA (at risk / breach)">
            <ResponsiveContainer width="100%" height={240}>
              <BarChart data={enterprise.facultySla}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                <YAxis allowDecimals={false} />
                <Tooltip />
                <Bar dataKey="value" fill="#ef6c00" name="Delayed" />
              </BarChart>
            </ResponsiveContainer>
          </ChartBox>
        )}
        {enterprise && enterprise.failureTrend.length > 0 && (
          <ChartBox title="Failure trend">
            <ResponsiveContainer width="100%" height={240}>
              <LineChart data={enterprise.failureTrend}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                <YAxis allowDecimals={false} />
                <Tooltip />
                <Line type="monotone" dataKey="value" stroke="#b71c1c" strokeWidth={2} name="Failed" />
              </LineChart>
            </ResponsiveContainer>
          </ChartBox>
        )}
        {enterprise && enterprise.departmentHeatmap.length > 0 && (
          <ChartBox title="Department heatmap (pending+review)">
            <ResponsiveContainer width="100%" height={240}>
              <BarChart data={enterprise.departmentHeatmap}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                <YAxis allowDecimals={false} />
                <Tooltip />
                <Bar dataKey="value" fill="#6d4c41" name="Load" />
              </BarChart>
            </ResponsiveContainer>
          </ChartBox>
        )}
        {enterprise && enterprise.dailyHeatmap.length > 0 && (
          <ChartBox title="Daily heatmap (hour)">
            <ResponsiveContainer width="100%" height={240}>
              <BarChart data={enterprise.dailyHeatmap}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                <YAxis allowDecimals={false} />
                <Tooltip />
                <Bar dataKey="value" fill="#4527a0" name="Sessions" />
              </BarChart>
            </ResponsiveContainer>
          </ChartBox>
        )}
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
        {departments && departments.pendingTrend.length > 0 && (
          <ChartBox title="Department pending trend">
            <ResponsiveContainer width="100%" height={240}>
              <LineChart data={departments.pendingTrend}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                <YAxis allowDecimals={false} />
                <Tooltip />
                <Line type="monotone" dataKey="value" stroke="#2e7d32" strokeWidth={2} name="Started" />
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

      {departments && departments.departments.length > 0 && (
        <Box>
          <Typography variant="subtitle1" sx={{ mb: 1, fontWeight: 700 }}>
            Department operations summary
          </Typography>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Department</TableCell>
                <TableCell>Pending</TableCell>
                <TableCell>Completed</TableCell>
                <TableCell>Failed</TableCell>
                <TableCell>Recognition</TableCell>
                <TableCell>Needs review</TableCell>
                <TableCell>Faculty</TableCell>
                <TableCell>Avg complete</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {departments.departments.map((d) => (
                <TableRow key={`${d.departmentId}-${d.departmentName}`} hover>
                  <TableCell>{d.departmentName}</TableCell>
                  <TableCell>{d.pendingSessions}</TableCell>
                  <TableCell>{d.completed}</TableCell>
                  <TableCell>{d.failed}</TableCell>
                  <TableCell>{d.recognitionRunning}</TableCell>
                  <TableCell>{d.needsReview}</TableCell>
                  <TableCell>{d.facultyCount}</TableCell>
                  <TableCell>{d.averageCompletionMinutes?.toFixed(0) ?? "—"}m</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>
      )}

      {enterprise && enterprise.topDelayedSessions.length > 0 && (
        <Box>
          <Typography variant="subtitle1" sx={{ mb: 1, fontWeight: 700 }}>
            Top delayed sessions
          </Typography>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Subject</TableCell>
                <TableCell>Staff</TableCell>
                <TableCell>SLA</TableCell>
                <TableCell>Age</TableCell>
                <TableCell align="right">Timeline</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {enterprise.topDelayedSessions.map((s) => (
                <TableRow key={s.sessionId} hover>
                  <TableCell>{s.displayTitle || s.subjectName || s.subjectId}</TableCell>
                  <TableCell>{s.staffName ?? s.staffId ?? "—"}</TableCell>
                  <TableCell>
                    {s.slaLevel} · {s.slaStatus}
                  </TableCell>
                  <TableCell>{s.elapsedDisplay || `${s.ageMinutes?.toFixed(0) ?? s.elapsedMinutes.toFixed(0)}m`}</TableCell>
                  <TableCell align="right">
                    <Button size="small" onClick={() => void openTimeline(s.sessionId)}>
                      Timeline
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>
      )}

      {opsAnalytics && (
        <Alert severity="info">
          Operational analytics (read-only): started {opsAnalytics.sessionsStarted} · completed{" "}
          {opsAnalytics.sessionsCompleted} · retry {opsAnalytics.retryPercent.toFixed(1)}% · failure{" "}
          {opsAnalytics.failurePercent.toFixed(1)}% · resume {opsAnalytics.resumePercent.toFixed(1)}% · peak{" "}
          {opsAnalytics.peakUsageLabel ?? "—"} · avg recognition {opsAnalytics.averageRecognitionMinutes?.toFixed(1) ?? "—"}
          m · avg review {opsAnalytics.averageReviewMinutes?.toFixed(1) ?? "—"}m · avg finalize{" "}
          {opsAnalytics.averageFinalizationMinutes?.toFixed(1) ?? "—"}m
          {enterprise ? ` · retry success ${enterprise.retrySuccessPercent.toFixed(1)}%` : ""}
        </Alert>
      )}

      {dash && (
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell padding="checkbox" />
              <TableCell>Session</TableCell>
              <TableCell>Subject</TableCell>
              <TableCell>Staff</TableCell>
              <TableCell>Workflow</TableCell>
              <TableCell>SLA</TableCell>
              <TableCell>Priority</TableCell>
              <TableCell>Elapsed</TableCell>
              <TableCell>Retries</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {dash.sessions.map((s) => (
              <TableRow key={s.sessionId} hover selected={selected.has(s.sessionId)}>
                <TableCell padding="checkbox">
                  <Checkbox checked={selected.has(s.sessionId)} onChange={() => toggle(s.sessionId)} />
                </TableCell>
                <TableCell>{s.sessionId.slice(0, 8)}…</TableCell>
                <TableCell>{s.displayTitle || s.subjectName || s.subjectId}</TableCell>
                <TableCell>{s.staffName ?? s.staffId ?? "—"}</TableCell>
                <TableCell>{s.friendlyWorkflowLabel || s.workflowStatusName}</TableCell>
                <TableCell>
                  {s.slaLevel ?? "—"} {s.slaStatus ? `· ${s.slaStatus}` : ""}
                </TableCell>
                <TableCell>{s.priorityScore ?? "—"}</TableCell>
                <TableCell>{s.elapsedDisplay || `${s.elapsedMinutes.toFixed(0)}m`}</TableCell>
                <TableCell>{s.retryCount}</TableCell>
                <TableCell align="right">
                  <Button size="small" onClick={() => void openTimeline(s.sessionId)}>
                    Timeline
                  </Button>
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

      <Dialog open={confirmOpen} onClose={() => setConfirmOpen(false)}>
        <DialogTitle>Confirm bulk operation</DialogTitle>
        <DialogContent>
          <Typography>
            Run this administrator assist action on {selected.size} session(s)? This never finalizes attendance
            automatically and never retries successful sessions.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={() => void runBulk()}>
            Confirm
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={progressOpen} onClose={() => setProgressOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>Bulk operation progress</DialogTitle>
        <DialogContent>
          <LinearProgress sx={{ mb: 2 }} />
          <Typography>{progressMsg}</Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setProgressOpen(false)}>Close</Button>
        </DialogActions>
      </Dialog>

      <Dialog open={!!timelineSessionId} onClose={() => setTimelineSessionId(null)} fullWidth maxWidth="md">
        <DialogTitle>Session timeline {timelineSessionId ? timelineSessionId.slice(0, 8) : ""}</DialogTitle>
        <DialogContent>
          <SessionTimeline timeline={timeline} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setTimelineSessionId(null)}>Close</Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default AttendanceRecoveryDashboardPage;
