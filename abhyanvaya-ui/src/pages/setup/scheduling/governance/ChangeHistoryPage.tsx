import { useCallback, useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import DownloadIcon from "@mui/icons-material/Download";
import {
  Timeline,
  TimelineConnector,
  TimelineContent,
  TimelineDot,
  TimelineItem,
  TimelineOppositeContent,
  TimelineSeparator,
} from "@mui/lab";
import {
  exportTimetableChangeHistoryExcel,
  getTimetableChangeHistory,
  listTimetables,
  TimetableChangeOperation,
  type TimetableChangeHistoryDto,
  type TimetableDto,
} from "../../../../services/schedulingService";
import { errMsg, parseOptionalSelectNumber } from "../schedulingFormUtils";
import { downloadBlob } from "../timetable/timetableUtils";
import { CHANGE_OPERATION_LABELS } from "./governanceEnumLabels";

const ChangeHistoryPage = () => {
  const [timetables, setTimetables] = useState<TimetableDto[]>([]);
  const [timetableId, setTimetableId] = useState<number | "">("");
  const [operation, setOperation] = useState<TimetableChangeOperation | "">("");
  const [entryId, setEntryId] = useState<number | "">("");
  const [fromUtc, setFromUtc] = useState("");
  const [toUtc, setToUtc] = useState("");

  const [rows, setRows] = useState<TimetableChangeHistoryDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [exporting, setExporting] = useState(false);

  useEffect(() => {
    void listTimetables({ includeArchived: true }).then((res) => {
      setTimetables(res.data);
      if (res.data.length) setTimetableId(res.data[0].id);
    });
  }, []);

  const load = useCallback(async () => {
    if (timetableId === "") return;
    setLoading(true);
    setError(null);
    try {
      const res = await getTimetableChangeHistory(timetableId, {
        entryId: entryId === "" ? undefined : entryId,
        operation: operation === "" ? undefined : operation,
        fromUtc: fromUtc ? new Date(fromUtc).toISOString() : undefined,
        toUtc: toUtc ? new Date(toUtc).toISOString() : undefined,
      });
      setRows(res.data);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  }, [timetableId, entryId, operation, fromUtc, toUtc]);

  useEffect(() => {
    void load();
  }, [load]);

  const handleExport = async () => {
    if (timetableId === "") return;
    setExporting(true);
    setError(null);
    try {
      const res = await exportTimetableChangeHistoryExcel(timetableId, {
        entryId: entryId === "" ? undefined : entryId,
        operation: operation === "" ? undefined : operation,
        fromUtc: fromUtc ? new Date(fromUtc).toISOString() : undefined,
        toUtc: toUtc ? new Date(toUtc).toISOString() : undefined,
      });
      downloadBlob(res.data, `timetable-${timetableId}-history.xlsx`);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setExporting(false);
    }
  };

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />} variant="text">
          Scheduling
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Change history
        </Typography>
        <Button
          startIcon={<DownloadIcon />}
          variant="outlined"
          disabled={timetableId === "" || exporting}
          onClick={() => void handleExport()}
        >
          Export Excel
        </Button>
      </Box>

      {error && <Alert severity="error" onClose={() => setError(null)}>{error}</Alert>}

      <Stack direction={{ xs: "column", md: "row" }} spacing={2} sx={{ flexWrap: "wrap" }}>
        <FormControl size="small" sx={{ minWidth: 220 }}>
          <InputLabel>Timetable</InputLabel>
          <Select
            label="Timetable"
            value={timetableId}
            onChange={(e) => setTimetableId(parseOptionalSelectNumber(e.target.value))}
          >
            {timetables.map((t) => (
              <MenuItem key={t.id} value={t.id}>{t.name}</MenuItem>
            ))}
          </Select>
        </FormControl>
        <FormControl size="small" sx={{ minWidth: 140 }}>
          <InputLabel>Operation</InputLabel>
          <Select
            label="Operation"
            value={operation}
            onChange={(e) =>
              setOperation(parseOptionalSelectNumber(e.target.value) as TimetableChangeOperation | "")
            }
          >
            <MenuItem value="">All</MenuItem>
            {Object.entries(CHANGE_OPERATION_LABELS).map(([k, v]) => (
              <MenuItem key={k} value={Number(k)}>{v}</MenuItem>
            ))}
          </Select>
        </FormControl>
        <TextField
          size="small"
          label="Entry ID"
          type="number"
          value={entryId}
          onChange={(e) => setEntryId(parseOptionalSelectNumber(e.target.value))}
          sx={{ width: 120 }}
        />
        <TextField
          size="small"
          label="From"
          type="datetime-local"
          value={fromUtc}
          onChange={(e) => setFromUtc(e.target.value)}
          slotProps={{ inputLabel: { shrink: true } }}
        />
        <TextField
          size="small"
          label="To"
          type="datetime-local"
          value={toUtc}
          onChange={(e) => setToUtc(e.target.value)}
          slotProps={{ inputLabel: { shrink: true } }}
        />
        <Button variant="contained" onClick={() => void load()} disabled={timetableId === ""}>
          Apply filters
        </Button>
      </Stack>

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", p: 4 }}><CircularProgress /></Box>
      ) : rows.length === 0 ? (
        <Typography color="text.secondary">No change history for the selected filters.</Typography>
      ) : (
        <Timeline position="right">
          {rows.map((ev, i) => (
            <TimelineItem key={ev.id}>
              <TimelineOppositeContent color="text.secondary" sx={{ flex: 0.25, fontSize: "0.75rem" }}>
                {new Date(ev.occurredUtc).toLocaleString()}
              </TimelineOppositeContent>
              <TimelineSeparator>
                <TimelineDot variant="outlined" />
                {i < rows.length - 1 && <TimelineConnector />}
              </TimelineSeparator>
              <TimelineContent>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {CHANGE_OPERATION_LABELS[ev.operation]}
                  {ev.entryId != null && ` · Entry #${ev.entryId}`}
                </Typography>
                {ev.reason && (
                  <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                    {ev.reason}
                  </Typography>
                )}
                {ev.userId != null && (
                  <Typography variant="caption" color="text.secondary">
                    User #{ev.userId}
                  </Typography>
                )}
              </TimelineContent>
            </TimelineItem>
          ))}
        </Timeline>
      )}
    </Stack>
  );
};

export default ChangeHistoryPage;
