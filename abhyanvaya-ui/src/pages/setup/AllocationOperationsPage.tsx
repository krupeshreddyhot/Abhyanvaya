import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Stack,
  Tab,
  Tabs,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import { PermissionKeys } from "../../auth/permissionKeys";
import { useAuth } from "../../context/AuthContext";
import {
  approveAllocationScenario,
  archiveAllocationScenario,
  compareAllocationScenarios,
  getAllocationAnalytics,
  getAllocationOperations,
  getAllocationScenarioDetail,
  getAllocationScenarios,
  rejectAllocationScenario,
  replayAllocationScenario,
  reviewAllocationScenario,
  type AllocationAnalyticsDto,
  type AllocationHistoryRow,
  type AllocationMultiCompareReport,
  type AllocationOpsDashboardDto,
  type AllocationScenarioDetailDto,
} from "../../services/allocationOperationsService";

const errMsg = (e: unknown): string => {
  const d = (e as { response?: { data?: unknown } }).response?.data;
  if (typeof d === "string") return d;
  if (d && typeof d === "object" && "message" in d) return String((d as { message: string }).message);
  return "Request failed.";
};

const AllocationOperationsPage = () => {
  const { hasPermission } = useAuth();
  const canView = hasPermission(PermissionKeys.AllocationOperationsView) || hasPermission(PermissionKeys.SectionView);
  const canCompare = hasPermission(PermissionKeys.AllocationScenarioCompare);
  const canReplay = hasPermission(PermissionKeys.AllocationScenarioReplay);
  const canReview = hasPermission(PermissionKeys.AllocationScenarioReview);
  const canArchive = hasPermission(PermissionKeys.AllocationScenarioArchive);
  const canApprove = hasPermission(PermissionKeys.AllocationApprove);
  const canReject = hasPermission(PermissionKeys.AllocationReject);

  const [tab, setTab] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [ops, setOps] = useState<AllocationOpsDashboardDto | null>(null);
  const [scenarios, setScenarios] = useState<AllocationHistoryRow[]>([]);
  const [analytics, setAnalytics] = useState<AllocationAnalyticsDto | null>(null);
  const [selected, setSelected] = useState<string[]>([]);
  const [compare, setCompare] = useState<AllocationMultiCompareReport | null>(null);
  const [detail, setDetail] = useState<AllocationScenarioDetailDto | null>(null);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const [o, s, a] = await Promise.all([getAllocationOperations(), getAllocationScenarios(), getAllocationAnalytics("AcademicYear")]);
      setOps(o.data);
      setScenarios(s.data);
      setAnalytics(a.data);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const toggle = (id: string) => {
    setSelected((prev) => (prev.includes(id) ? prev.filter((x) => x !== id) : prev.length >= 3 ? prev : [...prev, id]));
  };

  const openDetail = async (id: string) => {
    try {
      const res = await getAllocationScenarioDetail(id);
      setDetail(res.data);
      setTab(1);
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const doCompare = async () => {
    if (selected.length < 1) return;
    try {
      const res = await compareAllocationScenarios(selected);
      setCompare(res.data);
      setMessage(res.data.summary);
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const doReplay = async (id: string) => {
    try {
      const res = await replayAllocationScenario(id);
      setMessage(`Replay created scenario ${res.data.scenarioId} (historical unchanged).`);
      await load();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const doReview = async (id: string) => {
    try {
      const res = await reviewAllocationScenario(id, "Reviewed in operations workspace");
      setMessage(res.data.message || "Scenario marked Reviewed.");
      await load();
      if (detail?.scenarioId === id) await openDetail(id);
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const doArchive = async (id: string) => {
    try {
      const res = await archiveAllocationScenario(id);
      setMessage(res.data.message || "Scenario archived.");
      await load();
      if (detail?.scenarioId === id) await openDetail(id);
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const doApprove = async (id: string) => {
    try {
      const res = await approveAllocationScenario(id);
      if (!res.data.success) {
        setError(res.data.message || "Approval blocked.");
        return;
      }
      setMessage(res.data.message || "Approved (draft only).");
      await load();
      if (detail?.scenarioId === id) await openDetail(id);
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const doReject = async (id: string) => {
    try {
      const res = await rejectAllocationScenario(id, "Rejected from operations workspace");
      setMessage(res.data.message || "Scenario rejected.");
      await load();
      if (detail?.scenarioId === id) await openDetail(id);
    } catch (e) {
      setError(errMsg(e));
    }
  };

  if (!canView) {
    return (
      <Box sx={{ p: 2 }}>
        <Alert severity="warning">Allocation.Operations.View permission required.</Alert>
      </Box>
    );
  }

  return (
    <Box sx={{ p: 2, maxWidth: 1200, mx: "auto" }}>
      <Button component={RouterLink} to="/setup/academic/allocation-context" startIcon={<ArrowBackIcon />} sx={{ mb: 1 }}>
        Allocation Context
      </Button>
      <Typography variant="h5" sx={{ fontWeight: 800, mb: 0.5 }}>
        Allocation Operations
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Enterprise review workspace. Latest Scenario ≠ Current Institutional Allocation. Human approval creates draft allocations only.
      </Typography>

      {error && (
        <Alert severity="error" sx={{ mb: 1.5 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}
      {message && (
        <Alert severity="success" sx={{ mb: 1.5 }} onClose={() => setMessage(null)}>
          {message}
        </Alert>
      )}

      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2 }}>
        <Tab label="Runs" />
        <Tab label="Scenarios" />
        <Tab label="Analytics" />
        <Tab label="Constraints" />
      </Tabs>

      {loading && <CircularProgress size={28} />}

      {ops && tab === 0 && (
        <Stack spacing={2}>
          <Alert severity={ops.mandatoryViolations === 0 ? "success" : "error"}>
            Mandatory Violations = {ops.mandatoryViolations} (must be 0 for approval) · Preferred warnings {ops.preferredWarnings} ·
            Informational findings {ops.informationalFindings}
          </Alert>
          <Typography variant="body2">
            Runs {ops.totalRuns} · Successful {ops.successfulRuns} · Failed {ops.failedRuns} · Cancelled {ops.cancelledRuns} · Timed
            Out {ops.timedOutRuns} · Running {ops.runningRuns}
          </Typography>
          <Typography variant="body2">
            Mandatory compliance {ops.mandatoryCompliance}% · Preferred compliance {ops.preferredCompliance}% · Avg score{" "}
            {ops.averageScore}
          </Typography>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Recent Allocation Runs
          </Typography>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Scenario</TableCell>
                <TableCell>Course/Group/Sem</TableCell>
                <TableCell>Score</TableCell>
                <TableCell>Lifecycle</TableCell>
                <TableCell>Execution</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(ops.recentRuns || []).map((r) => (
                <TableRow key={r.scenarioId} hover sx={{ cursor: "pointer" }} onClick={() => void openDetail(r.scenarioId)}>
                  <TableCell>{r.scenarioId.slice(0, 8)}…</TableCell>
                  <TableCell>
                    {r.courseId}/{r.groupId}/{r.semesterId}
                  </TableCell>
                  <TableCell>{r.score}</TableCell>
                  <TableCell>{r.lifecycleStatus}</TableCell>
                  <TableCell>{r.status}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Stack>
      )}

      {tab === 1 && (
        <Stack spacing={2}>
          <Stack direction="row" spacing={1}>
            <Button variant="contained" disabled={!canCompare || selected.length < 1} onClick={() => void doCompare()}>
              Compare
            </Button>
            <Button variant="outlined" component={RouterLink} to="/setup/academic/allocation-context">
              Open Context Explorer
            </Button>
          </Stack>

          {detail && (
            <Alert severity={detail.contextCurrent ? "success" : "warning"}>
              <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                Scenario {detail.scenarioId.slice(0, 8)}…
              </Typography>
              <Typography variant="body2">
                Status: {detail.lifecycleStatus} · Execution: {detail.status} · Score: {detail.totalScore} · Version:{" "}
                {detail.currentVersionNumber}
              </Typography>
              <Typography variant="body2">
                Scenario Context: v{detail.contextVersion} · Current Context: v{detail.currentContextVersion ?? detail.contextVersion}{" "}
                {detail.contextCurrent ? "✓ Current" : "⚠ Outdated"}
              </Typography>
              {!detail.contextCurrent && (
                <Typography variant="body2" sx={{ mt: 0.5 }}>
                  This scenario was created using an earlier academic configuration and must be rebuilt before approval.
                </Typography>
              )}
              <Typography variant="body2" sx={{ mt: 0.5 }}>
                Mandatory Constraints: {ops?.mandatoryCompliance ?? "—"}% · Preferred: {ops?.preferredCompliance ?? "—"}%
              </Typography>
              <Stack direction="row" spacing={1} sx={{ mt: 1 }}>
                <Button size="small" disabled={!canReview} onClick={() => void doReview(detail.scenarioId)}>
                  Review
                </Button>
                <Button
                  size="small"
                  color="success"
                  disabled={!canApprove || !detail.governance?.canApprove}
                  onClick={() => void doApprove(detail.scenarioId)}
                >
                  Approve
                </Button>
                <Button size="small" color="warning" disabled={!canReject} onClick={() => void doReject(detail.scenarioId)}>
                  Reject
                </Button>
                <Button size="small" color="inherit" disabled={!canArchive} onClick={() => void doArchive(detail.scenarioId)}>
                  Archive
                </Button>
                {!detail.contextCurrent && (
                  <Button size="small" component={RouterLink} to="/setup/academic/allocation-context">
                    Rebuild Scenario
                  </Button>
                )}
              </Stack>
              {(detail.versions || []).length > 0 && (
                <>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700, mt: 1.5 }}>
                    Version History (immutable)
                  </Typography>
                  {[...(detail.versions || [])]
                    .sort((a, b) => a.versionNumber - b.versionNumber)
                    .map((v) => (
                      <Typography key={v.versionNumber} variant="body2">
                        Version {v.versionNumber} — {v.operation || v.status} · actor {v.createdBy ?? "—"} ·{" "}
                        {new Date(v.createdAt).toLocaleString()} · context v{v.contextVersion}
                        {v.reason ? ` · ${v.reason}` : ""}
                      </Typography>
                    ))}
                </>
              )}
            </Alert>
          )}

          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Scenario Workspace
          </Typography>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Select</TableCell>
                <TableCell>Scenario</TableCell>
                <TableCell>Score</TableCell>
                <TableCell>Lifecycle</TableCell>
                <TableCell>Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {scenarios.map((s) => (
                <TableRow key={s.scenarioId}>
                  <TableCell>
                    <input type="checkbox" checked={selected.includes(s.scenarioId)} onChange={() => toggle(s.scenarioId)} />
                  </TableCell>
                  <TableCell>
                    <Button size="small" onClick={() => void openDetail(s.scenarioId)}>
                      {s.scenarioId.slice(0, 8)}…
                    </Button>
                  </TableCell>
                  <TableCell>{s.score}</TableCell>
                  <TableCell>{s.lifecycleStatus}</TableCell>
                  <TableCell>
                    <Button size="small" disabled={!canReplay} onClick={() => void doReplay(s.scenarioId)}>
                      Replay
                    </Button>
                    <Button size="small" disabled={!canReview} onClick={() => void doReview(s.scenarioId)}>
                      Review
                    </Button>
                    <Button size="small" disabled={!canArchive} onClick={() => void doArchive(s.scenarioId)}>
                      Archive
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          {compare && (
            <Alert severity="info">
              Original {compare.originalScore} · Best {compare.bestScenarioLabel} ({compare.bestScenarioId?.slice(0, 8)}…) · Improvement{" "}
              {compare.improvementVsOriginal} · {compare.summary}
            </Alert>
          )}
        </Stack>
      )}

      {tab === 2 && analytics && (
        <Stack spacing={1}>
          <Typography variant="body2">Period: {analytics.period}</Typography>
          <Typography variant="body2">
            Total {analytics.totalRuns} · Successful {analytics.successfulRuns} · Failed {analytics.failedRuns} · Cancelled{" "}
            {analytics.cancelledRuns} · Timed Out {analytics.timedOutRuns} · Running {analytics.runningRuns}
          </Typography>
          <Typography variant="body2">Success rate: {analytics.successRate}% (from actual status counts)</Typography>
          <Typography variant="body2">Students allocated: {analytics.studentsAllocated}</Typography>
          <Typography variant="body2">Average occupancy: {analytics.averageSectionOccupancy}%</Typography>
          <Typography variant="body2">Mandatory compliance: {analytics.mandatoryCompliance}%</Typography>
          <Typography variant="body2">Preferred compliance: {analytics.preferredCompliance}%</Typography>
          <Typography variant="body2">Informational findings: {analytics.informationalFindings}</Typography>
          <Typography variant="body2">Average score: {analytics.averageScore}</Typography>
          {ops?.heatmap && (
            <>
              <Typography variant="subtitle2" sx={{ fontWeight: 700, mt: 1 }}>
                {ops.heatmap.title || "Latest Scenario – Section Utilization"}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                {ops.heatmap.scopeNote}
              </Typography>
              {(ops.heatmap.cells || []).map((c) => (
                <Typography key={c.sectionId} variant="body2">
                  {c.sectionCode} {c.occupancyPercent}% ({c.band}) — {c.studentCount}/{c.maximumCapacity}
                </Typography>
              ))}
            </>
          )}
        </Stack>
      )}

      {tab === 3 && ops?.constraints && (
        <Stack spacing={1}>
          <Alert severity={ops.constraints.mandatoryViolations === 0 ? "success" : "error"}>
            Mandatory Compliance {ops.constraints.mandatoryCompliance}% · Mandatory Violations {ops.constraints.mandatoryViolations}
          </Alert>
          <Typography variant="body2">
            Preferred Compliance {ops.constraints.preferredCompliance}% · Preferred Violations {ops.constraints.preferredViolations} ·
            Informational Findings {ops.constraints.informationalFindings}
          </Typography>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Constraint</TableCell>
                <TableCell>Priority</TableCell>
                <TableCell>Result</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(ops.constraints.rows || []).map((r, i) => (
                <TableRow key={`${r.constraintCode}-${i}`}>
                  <TableCell>{r.constraintCode}</TableCell>
                  <TableCell>{r.priority}</TableCell>
                  <TableCell>{r.satisfied ? "Passed" : "Warning/Fail"}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Stack>
      )}
    </Box>
  );
};

export default AllocationOperationsPage;
