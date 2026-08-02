import { useCallback, useEffect, useMemo, useState } from "react";
import { Link as RouterLink } from "react-router-dom";
import {
  Alert,
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
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
  Line,
  LineChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
  Legend,
} from "recharts";
import {
  archiveSandboxScenario,
  compareSandboxScenarios,
  createSandboxScenario,
  deleteSandboxScenario,
  duplicateSandboxScenario,
  favoriteSandboxScenario,
  getOptimizationSandboxWorkspace,
  getSandboxScenarioDetail,
  pinSandboxScenario,
  replaySandboxScenario,
  saveSandboxScenario,
  type OptimizationScenarioDetailDto,
  type OptimizationWorkspaceDto,
  type ScenarioComparisonResultDto,
  type ScenarioSummaryDto,
} from "../../../../services/schedulingService";

const OptimizationWorkspacePage = () => {
  const [data, setData] = useState<OptimizationWorkspaceDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [name, setName] = useState("Sandbox scenario");
  const [detail, setDetail] = useState<OptimizationScenarioDetailDto | null>(null);
  const [compare, setCompare] = useState<ScenarioComparisonResultDto | null>(null);
  const [leftId, setLeftId] = useState("");
  const [rightId, setRightId] = useState("");
  const [tab, setTab] = useState<"list" | "metrics" | "history">("list");

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await getOptimizationSandboxWorkspace();
      setData(res.data);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to load sandbox workspace");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const scenarios = data?.scenarios ?? [];

  const statusText = (s: number) =>
    ({ 1: "Draft", 2: "Saved", 3: "Compared", 4: "Reviewed", 5: "Archived" }[s] ?? String(s));

  const openDetail = async (s: ScenarioSummaryDto) => {
    const res = await getSandboxScenarioDetail(s.scenarioId);
    setDetail(res.data);
    setTab("history");
  };

  const scoreChart = useMemo(
    () =>
      (data?.evolution.scoreEvolution ?? []).map((p) => ({
        date: String(p.dateUtc).slice(0, 10),
        score: p.value,
        label: p.label,
      })),
    [data],
  );

  const conflictChart = useMemo(
    () =>
      (data?.evolution.conflictEvolution ?? []).map((p) => ({
        date: String(p.dateUtc).slice(0, 10),
        conflicts: p.value,
        label: p.label,
      })),
    [data],
  );

  return (
    <Stack spacing={2}>
      <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />}>
          Hub
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Optimization Workspace
        </Typography>
        <Button component={RouterLink} to="/setup/scheduling/optimization/preview" variant="outlined">
          Preview
        </Button>
      </Stack>

      <Alert severity="info">
        Sandbox only — save, replay, compare, and collaborate on simulations. No Apply button. Production timetables are
        never modified.
      </Alert>
      {error && <Alert severity="error">{error}</Alert>}

      <Stack direction="row" spacing={1}>
        <Button variant={tab === "list" ? "contained" : "outlined"} onClick={() => setTab("list")}>
          Scenario list
        </Button>
        <Button variant={tab === "metrics" ? "contained" : "outlined"} onClick={() => setTab("metrics")}>
          Metrics evolution
        </Button>
        <Button variant={tab === "history" ? "contained" : "outlined"} onClick={() => setTab("history")} disabled={!detail}>
          Details / history
        </Button>
      </Stack>

      <Stack direction={{ xs: "column", md: "row" }} spacing={1}>
        <TextField size="small" label="New scenario name" value={name} onChange={(e) => setName(e.target.value)} />
        <Button
          variant="contained"
          disabled={loading}
          onClick={() =>
            void (async () => {
              await createSandboxScenario({ name, captureFromLatestSimulation: true });
              await load();
            })()
          }
        >
          Create scenario
        </Button>
      </Stack>

      {tab === "list" && (
        <>
          <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
            <Chip label={`Scenarios ${scenarios.length}`} />
            <Chip label={`Favorites ${data?.favorites.length ?? 0}`} variant="outlined" />
            <Chip label={`Templates ${data?.templates.length ?? 0}`} variant="outlined" />
            <Chip label="Apply disabled" color="warning" />
          </Stack>
          <Box sx={{ overflowX: "auto" }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Name</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell>Score</TableCell>
                  <TableCell>Conflicts</TableCell>
                  <TableCell>Tags</TableCell>
                  <TableCell>Actions</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {scenarios.map((s) => (
                  <TableRow key={s.scenarioId} hover>
                    <TableCell>
                      {s.isPinned ? "📌 " : ""}
                      {s.isFavorite ? "★ " : ""}
                      {s.name}
                      {s.isTemplate ? " (template)" : ""}
                    </TableCell>
                    <TableCell>{statusText(s.status)}</TableCell>
                    <TableCell>
                      {s.currentScore} → {s.projectedScore}
                    </TableCell>
                    <TableCell>{s.conflictCount}</TableCell>
                    <TableCell>{(s.tags ?? []).join(", ")}</TableCell>
                    <TableCell>
                      <Stack direction="row" spacing={0.5} flexWrap="wrap" useFlexGap>
                        <Button size="small" onClick={() => void openDetail(s)}>
                          Details
                        </Button>
                        <Button size="small" onClick={() => void saveSandboxScenario(s.scenarioId).then(load)}>
                          Save
                        </Button>
                        <Button size="small" onClick={() => void replaySandboxScenario(s.scenarioId).then(() => openDetail(s))}>
                          Replay
                        </Button>
                        <Button size="small" onClick={() => void favoriteSandboxScenario(s.scenarioId, true).then(load)}>
                          Favorite
                        </Button>
                        <Button size="small" onClick={() => void pinSandboxScenario(s.scenarioId, true).then(load)}>
                          Pin
                        </Button>
                        <Button
                          size="small"
                          onClick={() => void duplicateSandboxScenario({ scenarioId: s.scenarioId }).then(load)}
                        >
                          Duplicate
                        </Button>
                        <Button size="small" onClick={() => void archiveSandboxScenario(s.scenarioId).then(load)}>
                          Archive
                        </Button>
                        <Button size="small" color="error" onClick={() => void deleteSandboxScenario(s.scenarioId).then(load)}>
                          Delete
                        </Button>
                      </Stack>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Box>

          <Typography variant="h6">Compare scenarios</Typography>
          <Stack direction={{ xs: "column", md: "row" }} spacing={1}>
            <TextField
              select
              size="small"
              label="Left"
              value={leftId}
              onChange={(e) => setLeftId(e.target.value)}
              sx={{ minWidth: 220 }}
            >
              {scenarios.map((s) => (
                <MenuItem key={s.scenarioId} value={s.scenarioId}>
                  {s.name}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              select
              size="small"
              label="Right"
              value={rightId}
              onChange={(e) => setRightId(e.target.value)}
              sx={{ minWidth: 220 }}
            >
              {scenarios.map((s) => (
                <MenuItem key={s.scenarioId} value={s.scenarioId}>
                  {s.name}
                </MenuItem>
              ))}
            </TextField>
            <Button
              variant="outlined"
              disabled={!leftId || !rightId}
              onClick={() =>
                void compareSandboxScenarios({ leftScenarioId: leftId, rightScenarioId: rightId }).then((r) =>
                  setCompare(r.data),
                )
              }
            >
              Compare
            </Button>
          </Stack>
        </>
      )}

      {tab === "metrics" && (
        <>
          <Typography variant="body2" color="text.secondary">
            {data?.evolution.notes}
          </Typography>
          <Typography variant="h6">Score evolution</Typography>
          <ResponsiveContainer width="100%" height={260}>
            <LineChart data={scoreChart}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="date" />
              <YAxis />
              <Tooltip />
              <Legend />
              <Line type="monotone" dataKey="score" stroke="#1976d2" name="Score" />
            </LineChart>
          </ResponsiveContainer>
          <Typography variant="h6">Conflict evolution</Typography>
          <ResponsiveContainer width="100%" height={260}>
            <LineChart data={conflictChart}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="date" />
              <YAxis />
              <Tooltip />
              <Legend />
              <Line type="monotone" dataKey="conflicts" stroke="#d32f2f" name="Conflicts" />
            </LineChart>
          </ResponsiveContainer>
        </>
      )}

      {tab === "history" && detail && (
        <Stack spacing={1}>
          <Typography variant="h6">{detail.summary.name}</Typography>
          <Typography variant="body2">
            Status {statusText(detail.summary.status)} · Replays {detail.summary.replayCount} · Comparisons{" "}
            {detail.summary.comparisonCount} · Views {detail.summary.viewCount}
          </Typography>
          <Typography variant="subtitle1">Snapshots (immutable)</Typography>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>#</TableCell>
                <TableCell>Label</TableCell>
                <TableCell>Captured</TableCell>
                <TableCell>Immutable</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {detail.snapshots.map((s) => (
                <TableRow key={s.snapshotId}>
                  <TableCell>{s.sequence}</TableCell>
                  <TableCell>{s.label}</TableCell>
                  <TableCell>{s.capturedUtc}</TableCell>
                  <TableCell>{s.isImmutable ? "Yes" : "No"}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <Typography variant="subtitle1">History timeline</Typography>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>When</TableCell>
                <TableCell>Action</TableCell>
                <TableCell>Details</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {detail.history.map((h, i) => (
                <TableRow key={`${h.occurredUtc}-${i}`}>
                  <TableCell>{h.occurredUtc}</TableCell>
                  <TableCell>{h.actionName}</TableCell>
                  <TableCell>{h.details}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Stack>
      )}

      <Dialog open={!!compare} onClose={() => setCompare(null)} maxWidth="md" fullWidth>
        <DialogTitle>Scenario comparison</DialogTitle>
        <DialogContent dividers>
          {compare && (
            <Stack spacing={1}>
              <Typography>
                {compare.left.name} vs {compare.right.name}
              </Typography>
              <Typography variant="body2">{compare.differences.verdict}</Typography>
              <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
                <Chip label={`Projected Δ ${compare.differences.projectedScoreDelta}`} />
                <Chip label={`Conflict Δ ${compare.differences.conflictDelta}`} />
              </Stack>
              {compare.improvementHighlights.map((h) => (
                <Alert key={h} severity="success">
                  {h}
                </Alert>
              ))}
              <Alert severity="warning">No Apply — sandbox comparison only.</Alert>
            </Stack>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCompare(null)}>Close</Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default OptimizationWorkspacePage;
