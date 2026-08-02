import { useCallback, useEffect, useMemo, useState } from "react";
import { Link as RouterLink } from "react-router-dom";
import {
  Alert,
  Box,
  Button,
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
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import {
  approveOptimizationRun,
  getOptimizationDashboard,
  getOptimizationRun,
  rejectOptimizationRun,
  runOptimizationEngine,
  type OptimizationDashboardDto,
  type OptimizationExecutionResultDto,
  type OptimizationProgressDto,
} from "../../../../services/schedulingService";
import * as signalR from "@microsoft/signalr";

const statusText = (s: number) =>
  ({ 1: "Queued", 2: "Running", 3: "Completed", 4: "Failed", 5: "Approved", 6: "Rejected" }[s] ?? String(s));

const OptimizationDashboardPage = () => {
  const [data, setData] = useState<OptimizationDashboardDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [academicYearId, setAcademicYearId] = useState("");
  const [timetableId, setTimetableId] = useState("");
  const [versionName, setVersionName] = useState("");
  const [selectedRunId, setSelectedRunId] = useState("");
  const [detail, setDetail] = useState<OptimizationExecutionResultDto | null>(null);
  const [progress, setProgress] = useState<OptimizationProgressDto | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params = academicYearId ? { academicYearId: Number(academicYearId) } : undefined;
      const res = await getOptimizationDashboard(params);
      setData(res.data);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to load optimization dashboard");
    } finally {
      setLoading(false);
    }
  }, [academicYearId]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (!selectedRunId) return;
    const baseUrl = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/api\/?$/, "") ?? "";
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${baseUrl}/hubs/optimization`, { withCredentials: true })
      .withAutomaticReconnect()
      .build();

    connection.on("OptimizationProgress", (p: OptimizationProgressDto) => {
      if (p.runId === selectedRunId) setProgress(p);
    });
    connection.on("OptimizationCompleted", (p: { runId: string }) => {
      if (p.runId === selectedRunId) void load();
    });

    void connection
      .start()
      .then(() => connection.invoke("SubscribeRun", selectedRunId))
      .catch(() => {
        /* hub optional while offline */
      });

    return () => {
      void connection.stop();
    };
  }, [selectedRunId, load]);

  const runEngine = async () => {
    setError(null);
    setInfo(null);
    try {
      const res = await runOptimizationEngine({
        academicYearId: academicYearId ? Number(academicYearId) : undefined,
        timetableId: timetableId ? Number(timetableId) : undefined,
        scenarioName: "Enterprise optimization run",
      });
      setDetail(res.data);
      setSelectedRunId(res.data.runId);
      setInfo(`Pipeline completed. Sandbox scenario: ${res.data.sandboxScenarioId ?? "n/a"}`);
      await load();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Optimization run failed");
    }
  };

  const openRun = async (runId: string) => {
    setSelectedRunId(runId);
    const res = await getOptimizationRun(runId);
    setDetail(res.data);
  };

  const approve = async () => {
    if (!selectedRunId) return;
    setError(null);
    try {
      const res = await approveOptimizationRun({
        runId: selectedRunId,
        newVersionName: versionName || undefined,
        remarks: "Approved from optimization dashboard",
      });
      setInfo(res.data.message);
      await load();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Approval failed");
    }
  };

  const reject = async () => {
    if (!selectedRunId) return;
    await rejectOptimizationRun({ runId: selectedRunId, reason: "Rejected from dashboard" });
    setInfo("Optimization run rejected.");
    await load();
  };

  const strategyChart = useMemo(
    () => (data?.topStrategies ?? []).map((s) => `${s.strategyCode}: ${s.candidateCount}`).join(" · "),
    [data],
  );

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />} variant="text">
          Scheduling
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Optimization dashboard
        </Typography>
        <Button component={RouterLink} to="/setup/scheduling/optimization/workspace" variant="outlined">
          Sandbox workspace
        </Button>
      </Box>

      <Alert severity="info">
        Optimization never edits the published timetable. Approve creates a <strong>new draft</strong> schedule version
        only. Attendance APIs remain unchanged.
      </Alert>

      {error && <Alert severity="error">{error}</Alert>}
      {info && <Alert severity="success">{info}</Alert>}
      {loading && <LinearProgress />}

      <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
        <TextField
          label="Academic year id"
          size="small"
          value={academicYearId}
          onChange={(e) => setAcademicYearId(e.target.value)}
        />
        <TextField
          label="Timetable id"
          size="small"
          value={timetableId}
          onChange={(e) => setTimetableId(e.target.value)}
        />
        <Button variant="contained" onClick={() => void runEngine()}>
          Run pipeline
        </Button>
        <Button variant="outlined" onClick={() => void load()}>
          Refresh
        </Button>
      </Stack>

      {progress && (
        <Box>
          <Typography variant="subtitle2">
            {progress.currentStrategy} — {progress.progressPercent}% · score {progress.currentScore.toFixed(2)} · Δ{" "}
            {progress.improvementDelta.toFixed(2)}
          </Typography>
          <LinearProgress variant="determinate" value={progress.progressPercent} sx={{ mt: 1 }} />
          <Typography variant="caption" color="text.secondary">
            {progress.statusMessage} · elapsed {progress.elapsedMs}ms
            {progress.estimatedRemainingMs != null ? ` · ETA ${progress.estimatedRemainingMs}ms` : ""}
          </Typography>
        </Box>
      )}

      <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
        <Box sx={{ flex: 1 }}>
          <Typography variant="h6">Summary</Typography>
          <Typography variant="body2">Total runs: {data?.totalRuns ?? 0}</Typography>
          <Typography variant="body2">Completed: {data?.completedRuns ?? 0}</Typography>
          <Typography variant="body2">Approved: {data?.approvedRuns ?? 0}</Typography>
          <Typography variant="body2">Best score: {data?.bestScore?.toFixed(2) ?? "0"}</Typography>
          <Typography variant="body2">Avg improvement: {data?.averageImprovement?.toFixed(2) ?? "0"}</Typography>
          <Typography variant="body2">
            Avg conflict reduction: {data?.averageConflictReduction?.toFixed(2) ?? "0"}
          </Typography>
          <Typography variant="body2">Top strategies: {strategyChart || "—"}</Typography>
        </Box>
        <Box sx={{ flex: 2 }}>
          <Typography variant="h6">Recent runs</Typography>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Run</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Baseline</TableCell>
                <TableCell>Projected</TableCell>
                <TableCell>Δ</TableCell>
                <TableCell />
              </TableRow>
            </TableHead>
            <TableBody>
              {(data?.recentRuns ?? []).map((r) => (
                <TableRow key={r.runId} hover selected={r.runId === selectedRunId}>
                  <TableCell>{r.runId.slice(0, 8)}</TableCell>
                  <TableCell>{statusText(r.status)}</TableCell>
                  <TableCell>{r.baselineScore.toFixed(2)}</TableCell>
                  <TableCell>{r.projectedScore.toFixed(2)}</TableCell>
                  <TableCell>{r.improvementDelta.toFixed(2)}</TableCell>
                  <TableCell>
                    <Button size="small" onClick={() => void openRun(r.runId)}>
                      Open
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>
      </Stack>

      {detail && (
        <Box>
          <Typography variant="h6">Run detail</Typography>
          <Typography variant="body2">{detail.combinedResult.summary.statusMessage}</Typography>
          <Typography variant="body2">
            Candidates: {detail.combinedResult.summary.candidateCount} · Conflicts{" "}
            {detail.combinedResult.summary.baselineConflictCount} → {detail.combinedResult.summary.projectedConflictCount}
          </Typography>
          {detail.comparison && (
            <Alert severity="info" sx={{ my: 1 }}>
              Score {detail.comparison.originalScore.toFixed(2)} → {detail.comparison.optimizedScore.toFixed(2)} (
              {detail.comparison.scoreImprovement.toFixed(2)}). Conflict reduction:{" "}
              {detail.comparison.conflictReduction}. Faculty Δ {detail.comparison.facultySatisfactionDelta.toFixed(2)} ·
              Room Δ {detail.comparison.roomUsageDelta.toFixed(2)} · Travel Δ {detail.comparison.travelDelta.toFixed(2)} ·
              Breaks Δ {detail.comparison.breaksDelta.toFixed(2)}.
            </Alert>
          )}
          <Table size="small" sx={{ my: 1 }}>
            <TableHead>
              <TableRow>
                <TableCell>Strategy</TableCell>
                <TableCell>Candidates</TableCell>
                <TableCell>Score after</TableCell>
                <TableCell>Conflicts</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {detail.intermediateResults.map((s) => (
                <TableRow key={s.strategyCode}>
                  <TableCell>{s.strategyName}</TableCell>
                  <TableCell>{s.candidateCount}</TableCell>
                  <TableCell>{s.scoreAfter.toFixed(2)}</TableCell>
                  <TableCell>{s.conflictCountAfter}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <Stack direction="row" spacing={1} alignItems="center">
            <TextField
              size="small"
              label="New draft version name"
              value={versionName}
              onChange={(e) => setVersionName(e.target.value)}
              sx={{ minWidth: 280 }}
            />
            <Button variant="contained" color="success" onClick={() => void approve()}>
              Approve → new draft
            </Button>
            <Button variant="outlined" color="warning" onClick={() => void reject()}>
              Reject
            </Button>
            <TextField
              select
              size="small"
              label="Sandbox link"
              value={detail.sandboxScenarioId ?? ""}
              sx={{ minWidth: 220 }}
              helperText="Review in sandbox workspace"
            >
              <MenuItem value={detail.sandboxScenarioId ?? ""}>{detail.sandboxScenarioId ?? "none"}</MenuItem>
            </TextField>
          </Stack>
        </Box>
      )}
    </Stack>
  );
};

export default OptimizationDashboardPage;
