import { useCallback, useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import { Link as RouterLink, useNavigate } from "react-router-dom";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import ArchiveIcon from "@mui/icons-material/Archive";
import PublishIcon from "@mui/icons-material/Publish";
import {
  Timeline,
  TimelineConnector,
  TimelineContent,
  TimelineDot,
  TimelineItem,
  TimelineOppositeContent,
  TimelineSeparator,
} from "@mui/lab";
import { PermissionKeys } from "../../../../auth/permissionKeys";
import { useAuth } from "../../../../context/AuthContext";
import {
  archiveTimetable,
  freezeTimetable,
  getTimetableChangeHistory,
  listAcademicYears,
  listArchiveReasons,
  listTimetables,
  publishTimetable,
  unlockFrozenTimetable,
  TimetableChangeOperation,
  TimetableStatus,
  type ArchiveReasonDto,
  type TimetableChangeHistoryDto,
  type TimetableDto,
  type TimetablePublishReadinessResultDto,
} from "../../../../services/schedulingService";
import { errMsg, parseOptionalSelectNumber } from "../schedulingFormUtils";
import {
  getTimetablePublishReadiness,
  normalizePublishReadiness,
  parsePublishFailure,
} from "../publishReadiness";
import PublishReadinessPanel from "../PublishReadinessPanel";
import { TIMETABLE_STATUS_COLORS, TIMETABLE_STATUS_LABELS } from "../timetable/timetableUtils";
import { CHANGE_OPERATION_LABELS } from "./governanceEnumLabels";

const PublishingPage = () => {
  const navigate = useNavigate();
  const { hasPermission } = useAuth();
  const canPublish = hasPermission(PermissionKeys.SchedulingPublish);
  const canArchive = hasPermission(PermissionKeys.SchedulingArchive) || hasPermission(PermissionKeys.SchedulingArchiveManage);
  const canFreeze = hasPermission(PermissionKeys.SchedulingFreeze);
  const canUnlock = hasPermission(PermissionKeys.SchedulingUnlock);

  const [years, setYears] = useState<{ id: number; label: string }[]>([]);
  const [filterYearId, setFilterYearId] = useState<number | "">("");
  const [filterStatus, setFilterStatus] = useState<TimetableStatus | "">("");
  const [rows, setRows] = useState<TimetableDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [publishReadiness, setPublishReadiness] =
    useState<TimetablePublishReadinessResultDto | null>(null);
  const [publishReadinessLoading, setPublishReadinessLoading] = useState(false);
  const [publishReadinessError, setPublishReadinessError] = useState<string | null>(null);

  const [selected, setSelected] = useState<TimetableDto | null>(null);
  const [timeline, setTimeline] = useState<TimetableChangeHistoryDto[]>([]);
  const [timelineLoading, setTimelineLoading] = useState(false);

  const [actionOpen, setActionOpen] = useState(false);
  const [actionType, setActionType] = useState<"publish" | "archive" | "freeze" | "unlock">("publish");
  const [reason, setReason] = useState("");
  const [archiveReasons, setArchiveReasons] = useState<ArchiveReasonDto[]>([]);
  const [archiveReasonId, setArchiveReasonId] = useState<number | "">("");
  const [acting, setActing] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await listTimetables({
        academicYearId: filterYearId === "" ? undefined : filterYearId,
        status: filterStatus === "" ? undefined : filterStatus,
        includeArchived: true,
      });
      setRows(res.data);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  }, [filterYearId, filterStatus]);

  useEffect(() => {
    void listAcademicYears().then((res) => {
      setYears(res.data.map((y) => ({ id: y.id, label: `${y.code} — ${y.name}` })));
      const current = res.data.find((y) => y.isCurrent) ?? res.data[0];
      if (current) setFilterYearId(current.id);
    });
    void listArchiveReasons()
      .then((res) => setArchiveReasons(res.data))
      .catch(() => undefined);
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const loadTimeline = async (t: TimetableDto) => {
    setSelected(t);
    setTimelineLoading(true);
    setPublishReadiness(null);
    setPublishReadinessError(null);
    try {
      const res = await getTimetableChangeHistory(t.id, {
        operation: undefined,
      });
      const lifecycle = res.data.filter(
        (h) =>
          h.operation === TimetableChangeOperation.Publish ||
          h.operation === TimetableChangeOperation.Archive,
      );
      setTimeline(lifecycle);
      // UX preflight — POST publish remains authoritative.
      setPublishReadinessLoading(true);
      try {
        const readinessRes = await getTimetablePublishReadiness(t.id);
        setPublishReadiness(normalizePublishReadiness(readinessRes.data) ?? readinessRes.data);
      } catch (e) {
        setPublishReadinessError(errMsg(e));
      } finally {
        setPublishReadinessLoading(false);
      }
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setTimelineLoading(false);
    }
  };

  const refreshSelectedPublishReadiness = async () => {
    if (!selected) return;
    setPublishReadinessLoading(true);
    setPublishReadinessError(null);
    try {
      const readinessRes = await getTimetablePublishReadiness(selected.id);
      setPublishReadiness(normalizePublishReadiness(readinessRes.data) ?? readinessRes.data);
    } catch (e) {
      setPublishReadinessError(errMsg(e));
    } finally {
      setPublishReadinessLoading(false);
    }
  };

  const openAction = (t: TimetableDto, type: "publish" | "archive" | "freeze" | "unlock") => {
    setSelected(t);
    setActionType(type);
    setReason("");
    setArchiveReasonId(archiveReasons[0]?.id ?? "");
    setActionOpen(true);
  };

  const handleAction = async () => {
    if (!selected) return;
    setActing(true);
    setError(null);
    try {
      if (actionType === "publish") {
        await publishTimetable(selected.id, { reason: reason.trim() || null });
        setPublishReadiness(null);
        setPublishReadinessError(null);
        setMessage("Timetable published.");
      } else if (actionType === "archive") {
        await archiveTimetable(selected.id, {
          reason: reason.trim() || null,
          archiveReasonId: archiveReasonId === "" ? null : archiveReasonId,
          comments: reason.trim() || null,
        });
        setMessage("Timetable archived.");
      } else if (actionType === "freeze") {
        if (!reason.trim()) throw new Error("Freeze reason is required.");
        await freezeTimetable(selected.id, { reason: reason.trim() });
        setMessage("Timetable frozen.");
      } else {
        if (!reason.trim()) throw new Error("Unlock reason is required.");
        await unlockFrozenTimetable(selected.id, { reason: reason.trim() });
        setMessage("Timetable unlocked.");
      }
      setActionOpen(false);
      await load();
      await loadTimeline(selected);
    } catch (e) {
      if (actionType === "publish") {
        const failure = parsePublishFailure(e);
        if (failure.readiness) {
          setPublishReadiness(failure.readiness);
          setPublishReadinessError(null);
        } else {
          setPublishReadinessError(failure.message);
        }
        setError(failure.message);
      } else {
        setPublishReadinessError(null);
        setError(errMsg(e));
      }
    } finally {
      setActing(false);
    }
  };

  return (
    <Stack spacing={2}>
      <Box sx={{display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap"}}>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />} variant="text">
          Scheduling
        </Button>
        <Typography variant="h5" sx={{flexGrow: 1}}>
          Publishing
        </Typography>
      </Box>

      {error && (
        <Alert
          severity="error"
          onClose={() => {
            setError(null);
          }}
        >
          {error}
        </Alert>
      )}
      {message && <Alert severity="success" onClose={() => setMessage(null)}>{message}</Alert>}

      {selected && (
        <PublishReadinessPanel
          readiness={publishReadiness}
          loading={publishReadinessLoading}
          error={publishReadinessError}
          onRecheck={() => void refreshSelectedPublishReadiness()}
          recheckBusy={publishReadinessLoading}
          onViewEntry={(entryId) => {
            navigate(`/setup/scheduling/timetables/${selected.id}?entryId=${entryId}`);
          }}
        />
      )}

      <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
        <FormControl size="small" sx={{ minWidth: 200 }}>
          <InputLabel>Academic year</InputLabel>
          <Select
            label="Academic year"
            value={filterYearId}
            onChange={(e) => setFilterYearId(parseOptionalSelectNumber(e.target.value))}
          >
            <MenuItem value="">All years</MenuItem>
            {years.map((y) => (
              <MenuItem key={y.id} value={y.id}>{y.label}</MenuItem>
            ))}
          </Select>
        </FormControl>
        <FormControl size="small" sx={{ minWidth: 160 }}>
          <InputLabel>Status</InputLabel>
          <Select
            label="Status"
            value={filterStatus}
            onChange={(e) => setFilterStatus(parseOptionalSelectNumber(e.target.value) as TimetableStatus | "")}
          >
            <MenuItem value="">All</MenuItem>
            {Object.entries(TIMETABLE_STATUS_LABELS).map(([k, v]) => (
              <MenuItem key={k} value={Number(k)}>{v}</MenuItem>
            ))}
          </Select>
        </FormControl>
      </Stack>

      {loading ? (
        <Box sx={{display: "flex", justifyContent: "center", p: 4}}><CircularProgress /></Box>
      ) : (
        <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
          <Table size="small" sx={{ flex: 1 }}>
            <TableHead>
              <TableRow>
                <TableCell>Name</TableCell>
                <TableCell>Department</TableCell>
                <TableCell>Status</TableCell>
                <TableCell align="right">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {rows.map((r) => (
                <TableRow
                  key={r.id}
                  hover
                  selected={selected?.id === r.id}
                  onClick={() => void loadTimeline(r)}
                  sx={{ cursor: "pointer" }}
                >
                  <TableCell>{r.name}</TableCell>
                  <TableCell>{r.departmentName ?? "—"}</TableCell>
                  <TableCell>
                    <Stack direction="row" spacing={0.5} sx={{ alignItems: "center" }}>
                      <Chip
                        size="small"
                        label={TIMETABLE_STATUS_LABELS[r.status]}
                        color={TIMETABLE_STATUS_COLORS[r.status]}
                      />
                      {r.isFrozen && <Chip size="small" color="warning" label="Frozen" />}
                    </Stack>
                  </TableCell>
                  <TableCell align="right" onClick={(e) => e.stopPropagation()}>
                    {canPublish && r.status === TimetableStatus.Locked && (
                      <Button size="small" startIcon={<PublishIcon />} onClick={() => openAction(r, "publish")}>
                        Publish
                      </Button>
                    )}
                    {canFreeze && r.status === TimetableStatus.Published && !r.isFrozen && (
                      <Button size="small" onClick={() => openAction(r, "freeze")}>
                        Freeze
                      </Button>
                    )}
                    {canUnlock && r.isFrozen && (
                      <Button size="small" onClick={() => openAction(r, "unlock")}>
                        Unlock
                      </Button>
                    )}
                    {canArchive && r.status !== TimetableStatus.Archived && (
                      <Button size="small" startIcon={<ArchiveIcon />} onClick={() => openAction(r, "archive")}>
                        Archive
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>

          <Box sx={{width: { xs: "100%", md: 320 }, flexShrink: 0}}>
            <Typography variant="subtitle1" gutterBottom>
              Publishing timeline
            </Typography>
            {!selected ? (
              <Typography variant="body2" color="text.secondary">
                Select a timetable to view publish/archive history.
              </Typography>
            ) : timelineLoading ? (
              <CircularProgress size={24} />
            ) : timeline.length === 0 ? (
              <Typography variant="body2" color="text.secondary">
                No publish or archive events yet.
              </Typography>
            ) : (
              <Timeline position="right" sx={{ p: 0, m: 0 }}>
                {timeline.map((ev, i) => (
                  <TimelineItem key={ev.id}>
                    <TimelineOppositeContent color="text.secondary" sx={{ flex: 0.35, fontSize: "0.7rem" }}>
                      {new Date(ev.occurredUtc).toLocaleString()}
                    </TimelineOppositeContent>
                    <TimelineSeparator>
                      <TimelineDot
                        color={ev.operation === TimetableChangeOperation.Publish ? "primary" : "grey"}
                      />
                      {i < timeline.length - 1 && <TimelineConnector />}
                    </TimelineSeparator>
                    <TimelineContent>
                      <Typography variant="body2">{CHANGE_OPERATION_LABELS[ev.operation]}</Typography>
                      {ev.reason && (
                        <Typography variant="caption" color="text.secondary">
                          {ev.reason}
                        </Typography>
                      )}
                    </TimelineContent>
                  </TimelineItem>
                ))}
              </Timeline>
            )}
          </Box>
        </Stack>
      )}

      {selected?.isFrozen && (
        <Alert severity="warning">Timetable Frozen — editing is blocked until an Academic Admin unlocks it.</Alert>
      )}

      <Dialog open={actionOpen} onClose={() => setActionOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>
          {actionType === "publish"
            ? "Publish timetable"
            : actionType === "archive"
              ? "Archive timetable"
              : actionType === "freeze"
                ? "Freeze timetable"
                : "Unlock frozen timetable"}
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{mt: 1}}>
            {actionType === "archive" && (
              <FormControl fullWidth>
                <InputLabel>Archive reason</InputLabel>
                <Select
                  label="Archive reason"
                  value={archiveReasonId}
                  onChange={(e) => setArchiveReasonId(parseOptionalSelectNumber(e.target.value))}
                >
                  {archiveReasons.map((r) => (
                    <MenuItem key={r.id} value={r.id}>
                      {r.name}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
            )}
            <TextField
              label={
                actionType === "publish"
                  ? "Reason (optional)"
                  : actionType === "archive"
                    ? "Comments"
                    : "Reason (required)"
              }
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              fullWidth
              multiline
              rows={2}
              required={actionType === "freeze" || actionType === "unlock"}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setActionOpen(false)}>Cancel</Button>
          <Button variant="contained" disabled={acting} onClick={() => void handleAction()}>
            Confirm
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default PublishingPage;
