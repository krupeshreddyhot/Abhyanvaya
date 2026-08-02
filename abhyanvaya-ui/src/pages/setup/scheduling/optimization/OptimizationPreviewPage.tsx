import { useCallback, useEffect, useState } from "react";
import { Link as RouterLink } from "react-router-dom";
import {
  Alert,
  Box,
  Button,
  Chip,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
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
  getOptimizationPreview,
  runOptimizationSimulation,
  type OptimizationPreviewDto,
} from "../../../../services/schedulingService";

const OptimizationPreviewPage = () => {
  const [data, setData] = useState<OptimizationPreviewDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await getOptimizationPreview();
      setData(res.data);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to load optimization preview");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const resimulate = async () => {
    setLoading(true);
    setError(null);
    try {
      await runOptimizationSimulation({ scenarioName: "Manual preview" });
      await load();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Simulation failed");
      setLoading(false);
    }
  };

  const sim = data?.simulation;
  const dimensionChart =
    sim?.baselineScore.dimensions.map((d) => ({
      name: d.dimensionName,
      current: d.weightedScore,
      projected:
        sim.projectedScoreDetail.dimensions.find((x) => x.dimension === d.dimension)?.weightedScore ?? d.weightedScore,
    })) ?? [];

  return (
    <Stack spacing={2}>
      <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />}>
          Hub
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Optimization Preview
        </Typography>
        <Button variant="outlined" onClick={() => void resimulate()} disabled={loading}>
          Re-simulate
        </Button>
      </Stack>

      <Alert severity="info">
        Phase 2B.6 readiness only — preview, score, and compare. There is no Apply action and no optimizer
        implementation.
      </Alert>
      {error && <Alert severity="error">{error}</Alert>}

      {sim && (
        <>
          <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
            <Chip label={`Current score ${sim.currentScore}`} color="primary" />
            <Chip label={`Projected score ${sim.projectedScore}`} color="secondary" />
            <Chip label={`Delta ${sim.scoreDelta}`} variant="outlined" />
            <Chip label={`Conflicts ${sim.currentConflictCount} → ${sim.projectedConflictCount}`} />
            <Chip label={`Status ${sim.status}`} variant="outlined" />
            <Chip label="Apply disabled" color="warning" />
          </Stack>

          <Typography variant="body2" color="text.secondary">
            {sim.message}
          </Typography>

          <Typography variant="h6">Score dimensions</Typography>
          <ResponsiveContainer width="100%" height={280}>
            <BarChart data={dimensionChart}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="name" hide />
              <YAxis />
              <Tooltip />
              <Bar dataKey="current" fill="#1976d2" name="Current" />
              <Bar dataKey="projected" fill="#455a64" name="Projected" />
            </BarChart>
          </ResponsiveContainer>

          <Typography variant="h6">Metrics</Typography>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Metric</TableCell>
                <TableCell>Value</TableCell>
                <TableCell>Unit</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {sim.metrics.map((m) => (
                <TableRow key={m.metricName}>
                  <TableCell>{m.metricName}</TableCell>
                  <TableCell>{m.value}</TableCell>
                  <TableCell>{m.unit}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>

          {data?.conflictSnapshot && (
            <>
              <Typography variant="h6">Conflicts snapshot</Typography>
              <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
                <Chip label={`Total ${data.conflictSnapshot.latestSummary.totalConflicts}`} />
                <Chip label={`Faculty ${data.conflictSnapshot.facultyConflicts}`} />
                <Chip label={`Room ${data.conflictSnapshot.roomConflicts}`} />
                <Chip label={`Student ${data.conflictSnapshot.studentConflicts}`} />
                <Chip label={`Calendar ${data.conflictSnapshot.calendarConflicts}`} />
              </Stack>
            </>
          )}

          {(data?.heatMaps?.length ?? 0) > 0 && (
            <>
              <Typography variant="h6">Heat maps</Typography>
              {(data?.heatMaps ?? []).slice(0, 2).map((map) => (
                <Box key={`${map.kind}-${map.entityId ?? 0}`}>
                  <Typography variant="subtitle2">{map.kind}</Typography>
                  <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
                    {Object.entries(map.loadDistribution ?? {}).map(([k, v]) => (
                      <Chip key={k} size="small" label={`${k}: ${v}`} />
                    ))}
                  </Stack>
                </Box>
              ))}
            </>
          )}

          <Typography variant="h6">Comparison</Typography>
          <Alert severity="warning">
            Current vs projected are identical in Phase 2B.6 because optimizers are not implemented. Future Phase 3
            strategies will populate projected candidates after simulation → preview → user approval → apply.
          </Alert>

          {data?.telemetry && (
            <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
              <Chip size="small" label={`Simulations ${data.telemetry.simulationCount}`} />
              <Chip size="small" label={`Exec ${data.telemetry.executionTimeMs}ms`} />
              <Chip size="small" label={`Score ${data.telemetry.scoringTimeMs}ms`} />
              <Chip size="small" label={`Rejected ${data.telemetry.rejectedSimulations}`} />
              <Chip size="small" label={`Accepted* ${data.telemetry.acceptedSimulations}`} />
            </Stack>
          )}
          <Typography variant="caption" color="text.secondary">
            *Accepted means queued for a future apply pipeline only — never mutates the live timetable here.
          </Typography>
        </>
      )}
    </Stack>
  );
};

export default OptimizationPreviewPage;
