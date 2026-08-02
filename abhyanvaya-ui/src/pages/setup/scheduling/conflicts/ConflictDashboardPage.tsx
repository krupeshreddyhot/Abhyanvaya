import { useEffect, useState } from "react";
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
import { getConflictDashboard, type ConflictDashboardDto, type HeatMapDto } from "../../../../services/schedulingService";

const colourSx = (colour: string) => {
  switch (colour) {
    case "Red":
      return { bgcolor: "#ef5350", color: "#fff" };
    case "Orange":
      return { bgcolor: "#ff9800", color: "#fff" };
    case "Yellow":
      return { bgcolor: "#ffeb3b", color: "#333" };
    default:
      return { bgcolor: "#66bb6a", color: "#fff" };
  }
};

const HeatMapPanel = ({ map }: { map: HeatMapDto }) => (
  <Box>
    <Typography variant="subtitle1" gutterBottom>
      {map.kind} heat map
    </Typography>
    <Stack direction="row" spacing={1} mb={1}>
      {Object.entries(map.loadDistribution ?? {}).map(([k, v]) => (
        <Chip key={k} size="small" label={`${k}: ${v}`} sx={colourSx(k)} />
      ))}
    </Stack>
    <Box sx={{ overflowX: "auto" }}>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Day</TableCell>
            <TableCell>Slot</TableCell>
            <TableCell>Load</TableCell>
            <TableCell>Colour</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {(map.cells ?? []).slice(0, 40).map((c) => (
            <TableRow key={`${c.dayOfWeek}-${c.timeSlotId}`}>
              <TableCell>{c.dayOfWeek}</TableCell>
              <TableCell>{c.timeSlotName ?? c.timeSlotId}</TableCell>
              <TableCell>{c.loadCount}</TableCell>
              <TableCell>
                <Chip size="small" label={c.colour} sx={colourSx(c.colour)} />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </Box>
  </Box>
);

const ConflictDashboardPage = () => {
  const [data, setData] = useState<ConflictDashboardDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void (async () => {
      try {
        const res = await getConflictDashboard();
        setData(res.data);
      } catch (e: unknown) {
        setError(e instanceof Error ? e.message : "Failed to load conflict dashboard");
      }
    })();
  }, []);

  return (
    <Stack spacing={2}>
      <Stack direction="row" spacing={1} alignItems="center">
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />}>
          Hub
        </Button>
        <Typography variant="h5">Conflict Dashboard</Typography>
        <Button component={RouterLink} to="/setup/scheduling/conflicts/workspace" variant="outlined">
          Open workspace
        </Button>
      </Stack>
      <Alert severity="info">Validation status only — no optimizer or auto-fix in Phase 2B.</Alert>
      {error && <Alert severity="error">{error}</Alert>}
      {data && (
        <>
          <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
            <Chip label={`Status: ${data.validationStatus}`} color="primary" />
            <Chip label={`Total ${data.latestSummary.totalConflicts}`} />
            <Chip label={`Faculty ${data.facultyConflicts}`} />
            <Chip label={`Room ${data.roomConflicts}`} />
            <Chip label={`Student ${data.studentConflicts}`} />
            <Chip label={`Calendar ${data.calendarConflicts}`} />
          </Stack>
          <Typography variant="h6">Categories</Typography>
          <Stack direction="row" spacing={1}>
            {Object.entries(data.conflictCategories ?? {}).map(([k, v]) => (
              <Chip key={k} label={`${k}: ${v}`} variant="outlined" />
            ))}
          </Stack>
          <Typography variant="h6">Warning trends</Typography>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>When (UTC)</TableCell>
                <TableCell>Warnings</TableCell>
                <TableCell>Errors</TableCell>
                <TableCell>Critical</TableCell>
                <TableCell>Total</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(data.warningTrends ?? []).map((t, i) => (
                <TableRow key={i}>
                  <TableCell>{new Date(t.dateUtc).toLocaleString()}</TableCell>
                  <TableCell>{t.warningCount}</TableCell>
                  <TableCell>{t.errorCount}</TableCell>
                  <TableCell>{t.criticalCount}</TableCell>
                  <TableCell>{t.totalConflicts}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <Typography variant="h6">Heat maps</Typography>
          <Stack spacing={3}>
            {(data.heatMaps ?? []).map((m) => (
              <HeatMapPanel key={m.kind} map={m} />
            ))}
          </Stack>
        </>
      )}
    </Stack>
  );
};

export default ConflictDashboardPage;
