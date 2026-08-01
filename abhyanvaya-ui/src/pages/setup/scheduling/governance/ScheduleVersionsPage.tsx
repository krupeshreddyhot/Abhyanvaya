import { useCallback, useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Drawer,
  FormControl,
  FormControlLabel,
  IconButton,
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
import { Link as RouterLink } from "react-router-dom";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import AddIcon from "@mui/icons-material/Add";
import ArchiveIcon from "@mui/icons-material/Archive";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import HistoryIcon from "@mui/icons-material/History";
import StarIcon from "@mui/icons-material/Star";
import { PermissionKeys } from "../../../../auth/permissionKeys";
import { useAuth } from "../../../../context/AuthContext";
import {
  archiveScheduleVersion,
  clonePreviousScheduleVersion,
  createScheduleVersion,
  duplicateScheduleVersion,
  getScheduleVersionHistory,
  listAcademicYears,
  listScheduleVersions,
  listTimeSlotSets,
  markCurrentScheduleVersion,
  ScheduleVersionStatus,
  type CreateScheduleVersionRequest,
  type DuplicateScheduleVersionRequest,
  type ScheduleVersionDto,
  type ScheduleVersionHistoryDto,
} from "../../../../services/schedulingService";
import { listDepartments } from "../../../../services/setupService";
import { errMsg, parseOptionalSelectNumber } from "../schedulingFormUtils";
import {
  SCHEDULE_VERSION_STATUS_COLORS,
  SCHEDULE_VERSION_STATUS_LABELS,
} from "./governanceEnumLabels";
import CompareVersionsDialog from "./CompareVersionsDialog";

const ScheduleVersionsPage = () => {
  const { hasPermission } = useAuth();
  const canManage = hasPermission(PermissionKeys.SchedulingVersionManage);
  const canArchive = hasPermission(PermissionKeys.SchedulingArchive);
  const canCompare = hasPermission(PermissionKeys.SchedulingVersionCompareView);
  const canExportCompare = hasPermission(PermissionKeys.SchedulingVersionCompareExport);
  const [compareOpen, setCompareOpen] = useState(false);

  const [years, setYears] = useState<{ id: number; label: string }[]>([]);
  const [departments, setDepartments] = useState<{ id: number; name: string }[]>([]);
  const [slotSets, setSlotSets] = useState<{ id: number; label: string }[]>([]);
  const [filterYearId, setFilterYearId] = useState<number | "">("");
  const [filterStatus, setFilterStatus] = useState<ScheduleVersionStatus | "">("");
  const [includeArchived, setIncludeArchived] = useState(false);
  const [rows, setRows] = useState<ScheduleVersionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const [createOpen, setCreateOpen] = useState(false);
  const [duplicateOpen, setDuplicateOpen] = useState(false);
  const [clonePrevOpen, setClonePrevOpen] = useState(false);
  const [createForm, setCreateForm] = useState<CreateScheduleVersionRequest>({
    academicYearId: 0,
    versionName: "",
    remarks: "",
    createEmptyTimetable: false,
    timetableName: "",
    departmentId: null,
    timeSlotSetId: null,
  });
  const [dupForm, setDupForm] = useState<DuplicateScheduleVersionRequest>({
    sourceVersionId: 0,
    versionName: "",
    remarks: "",
    cloneAllTimetables: true,
  });
  const [clonePrevName, setClonePrevName] = useState("");
  const [saving, setSaving] = useState(false);

  const [historyOpen, setHistoryOpen] = useState(false);
  const [historyRows, setHistoryRows] = useState<ScheduleVersionHistoryDto[]>([]);
  const [historyLoading, setHistoryLoading] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await listScheduleVersions({
        academicYearId: filterYearId === "" ? undefined : filterYearId,
        status: filterStatus === "" ? undefined : filterStatus,
        includeArchived,
      });
      setRows(res.data);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  }, [filterYearId, filterStatus, includeArchived]);

  useEffect(() => {
    void (async () => {
      const [yRes, dRes, sRes] = await Promise.all([
        listAcademicYears(),
        listDepartments(undefined, true),
        listTimeSlotSets(),
      ]);
      setYears(yRes.data.map((y) => ({ id: y.id, label: `${y.code} — ${y.name}` })));
      setDepartments(dRes.data.map((d) => ({ id: d.id, name: d.name })));
      setSlotSets(sRes.data.map((s) => ({ id: s.id, label: s.name })));
      const current = yRes.data.find((y) => y.isCurrent) ?? yRes.data[0];
      if (current) {
        setFilterYearId(current.id);
        setCreateForm((f) => ({ ...f, academicYearId: current.id }));
      }
    })();
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const openHistory = async () => {
    if (filterYearId === "") return;
    setHistoryOpen(true);
    setHistoryLoading(true);
    try {
      const res = await getScheduleVersionHistory(filterYearId);
      setHistoryRows(res.data);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setHistoryLoading(false);
    }
  };

  const handleCreate = async () => {
    setSaving(true);
    setError(null);
    try {
      await createScheduleVersion(createForm);
      setCreateOpen(false);
      setMessage("Version created.");
      await load();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  };

  const handleDuplicate = async () => {
    setSaving(true);
    setError(null);
    try {
      await duplicateScheduleVersion(dupForm);
      setDuplicateOpen(false);
      setMessage("Version duplicated.");
      await load();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  };

  const handleClonePrevious = async () => {
    if (filterYearId === "" || !clonePrevName.trim()) return;
    setSaving(true);
    setError(null);
    try {
      await clonePreviousScheduleVersion({
        academicYearId: filterYearId,
        versionName: clonePrevName.trim(),
      });
      setClonePrevOpen(false);
      setClonePrevName("");
      setMessage("Cloned from previous version.");
      await load();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  };

  const handleMarkCurrent = async (id: number) => {
    try {
      await markCurrentScheduleVersion(id);
      setMessage("Marked as current version.");
      await load();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const handleArchive = async (id: number) => {
    try {
      await archiveScheduleVersion(id);
      setMessage("Version archived.");
      await load();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />} variant="text">
          Scheduling
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Schedule versions
        </Typography>
        <Button startIcon={<HistoryIcon />} onClick={() => void openHistory()} disabled={filterYearId === ""}>
          History
        </Button>
        {canCompare && (
          <Button variant="outlined" onClick={() => setCompareOpen(true)}>
            Compare versions
          </Button>
        )}
        {canManage && (
          <>
            <Button startIcon={<ContentCopyIcon />} onClick={() => setClonePrevOpen(true)}>
              Clone previous
            </Button>
            <Button startIcon={<AddIcon />} variant="contained" onClick={() => setCreateOpen(true)}>
              New version
            </Button>
          </>
        )}
      </Box>

      {error && <Alert severity="error" onClose={() => setError(null)}>{error}</Alert>}
      {message && <Alert severity="success" onClose={() => setMessage(null)}>{message}</Alert>}

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
            onChange={(e) => setFilterStatus(parseOptionalSelectNumber(e.target.value) as ScheduleVersionStatus | "")}
          >
            <MenuItem value="">All</MenuItem>
            {Object.entries(SCHEDULE_VERSION_STATUS_LABELS).map(([k, v]) => (
              <MenuItem key={k} value={Number(k)}>{v}</MenuItem>
            ))}
          </Select>
        </FormControl>
        <FormControlLabel
          control={<Checkbox checked={includeArchived} onChange={(e) => setIncludeArchived(e.target.checked)} />}
          label="Include archived"
        />
      </Stack>

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", p: 4 }}><CircularProgress /></Box>
      ) : (
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>#</TableCell>
              <TableCell>Name</TableCell>
              <TableCell>Year</TableCell>
              <TableCell>Status</TableCell>
              <TableCell>Current</TableCell>
              <TableCell>Timetables</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((r) => (
              <TableRow key={r.id} hover>
                <TableCell>{r.versionNumber}</TableCell>
                <TableCell>{r.versionName}</TableCell>
                <TableCell>{r.academicYearName ?? r.academicYearId}</TableCell>
                <TableCell>
                  <Chip
                    size="small"
                    label={SCHEDULE_VERSION_STATUS_LABELS[r.status]}
                    color={SCHEDULE_VERSION_STATUS_COLORS[r.status]}
                  />
                </TableCell>
                <TableCell>{r.isCurrent ? "Yes" : "—"}</TableCell>
                <TableCell>{r.timetableCount}</TableCell>
                <TableCell align="right">
                  {canManage && !r.isCurrent && r.status !== ScheduleVersionStatus.Archived && (
                    <IconButton size="small" title="Mark current" onClick={() => void handleMarkCurrent(r.id)}>
                      <StarIcon fontSize="small" />
                    </IconButton>
                  )}
                  {canManage && (
                    <IconButton
                      size="small"
                      title="Duplicate"
                      onClick={() => {
                        setDupForm({ sourceVersionId: r.id, versionName: `${r.versionName} (copy)`, remarks: "", cloneAllTimetables: true });
                        setDuplicateOpen(true);
                      }}
                    >
                      <ContentCopyIcon fontSize="small" />
                    </IconButton>
                  )}
                  {canArchive && r.status !== ScheduleVersionStatus.Archived && (
                    <IconButton size="small" title="Archive" onClick={() => void handleArchive(r.id)}>
                      <ArchiveIcon fontSize="small" />
                    </IconButton>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <Dialog open={createOpen} onClose={() => setCreateOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Create schedule version</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <FormControl fullWidth>
              <InputLabel>Academic year</InputLabel>
              <Select
                label="Academic year"
                value={createForm.academicYearId || ""}
                onChange={(e) => setCreateForm((f) => ({ ...f, academicYearId: Number(e.target.value) }))}
              >
                {years.map((y) => (
                  <MenuItem key={y.id} value={y.id}>{y.label}</MenuItem>
                ))}
              </Select>
            </FormControl>
            <TextField
              label="Version name"
              value={createForm.versionName}
              onChange={(e) => setCreateForm((f) => ({ ...f, versionName: e.target.value }))}
              fullWidth
            />
            <TextField
              label="Remarks"
              value={createForm.remarks ?? ""}
              onChange={(e) => setCreateForm((f) => ({ ...f, remarks: e.target.value }))}
              fullWidth
              multiline
              rows={2}
            />
            <FormControlLabel
              control={
                <Checkbox
                  checked={createForm.createEmptyTimetable ?? false}
                  onChange={(e) => setCreateForm((f) => ({ ...f, createEmptyTimetable: e.target.checked }))}
                />
              }
              label="Create empty timetable"
            />
            {createForm.createEmptyTimetable && (
              <>
                <TextField
                  label="Timetable name"
                  value={createForm.timetableName ?? ""}
                  onChange={(e) => setCreateForm((f) => ({ ...f, timetableName: e.target.value }))}
                  fullWidth
                />
                <FormControl fullWidth>
                  <InputLabel>Department</InputLabel>
                  <Select
                    label="Department"
                    value={createForm.departmentId ?? ""}
                    onChange={(e) =>
                      setCreateForm((f) => ({
                        ...f,
                        departmentId: parseOptionalSelectNumber(e.target.value) || null,
                      }))
                    }
                  >
                    <MenuItem value="">None</MenuItem>
                    {departments.map((d) => (
                      <MenuItem key={d.id} value={d.id}>{d.name}</MenuItem>
                    ))}
                  </Select>
                </FormControl>
                <FormControl fullWidth>
                  <InputLabel>Time slot set</InputLabel>
                  <Select
                    label="Time slot set"
                    value={createForm.timeSlotSetId ?? ""}
                    onChange={(e) =>
                      setCreateForm((f) => ({
                        ...f,
                        timeSlotSetId: parseOptionalSelectNumber(e.target.value) || null,
                      }))
                    }
                  >
                    <MenuItem value="">None</MenuItem>
                    {slotSets.map((s) => (
                      <MenuItem key={s.id} value={s.id}>{s.label}</MenuItem>
                    ))}
                  </Select>
                </FormControl>
              </>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCreateOpen(false)}>Cancel</Button>
          <Button variant="contained" disabled={saving || !createForm.versionName.trim()} onClick={() => void handleCreate()}>
            Create
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={duplicateOpen} onClose={() => setDuplicateOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Duplicate version</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              label="New version name"
              value={dupForm.versionName}
              onChange={(e) => setDupForm((f) => ({ ...f, versionName: e.target.value }))}
              fullWidth
            />
            <TextField
              label="Remarks"
              value={dupForm.remarks ?? ""}
              onChange={(e) => setDupForm((f) => ({ ...f, remarks: e.target.value }))}
              fullWidth
            />
            <FormControlLabel
              control={
                <Checkbox
                  checked={dupForm.cloneAllTimetables ?? true}
                  onChange={(e) => setDupForm((f) => ({ ...f, cloneAllTimetables: e.target.checked }))}
                />
              }
              label="Clone all timetables"
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDuplicateOpen(false)}>Cancel</Button>
          <Button variant="contained" disabled={saving || !dupForm.versionName.trim()} onClick={() => void handleDuplicate()}>
            Duplicate
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={clonePrevOpen} onClose={() => setClonePrevOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Clone previous version</DialogTitle>
        <DialogContent>
          <TextField
            label="New version name"
            value={clonePrevName}
            onChange={(e) => setClonePrevName(e.target.value)}
            fullWidth
            sx={{ mt: 1 }}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setClonePrevOpen(false)}>Cancel</Button>
          <Button variant="contained" disabled={saving || !clonePrevName.trim()} onClick={() => void handleClonePrevious()}>
            Clone
          </Button>
        </DialogActions>
      </Dialog>

      <Drawer anchor="right" open={historyOpen} onClose={() => setHistoryOpen(false)}>
        <Box sx={{ width: 360, p: 2 }}>
          <Typography variant="h6" gutterBottom>Version history</Typography>
          {historyLoading ? (
            <CircularProgress size={24} />
          ) : (
            <Stack spacing={1}>
              {historyRows.map((h) => (
                <Box key={h.versionId} sx={{ borderBottom: 1, borderColor: "divider", pb: 1 }}>
                  <Typography variant="subtitle2">
                    v{h.versionNumber} — {h.versionName}
                  </Typography>
                  <Chip size="small" label={SCHEDULE_VERSION_STATUS_LABELS[h.status]} sx={{ my: 0.5 }} />
                  <Typography variant="caption" sx={{ display: "block" }} color="text.secondary">
                    Created {new Date(h.createdDate).toLocaleString()}
                  </Typography>
                  {h.publishedDate && (
                    <Typography variant="caption" sx={{ display: "block" }} color="text.secondary">
                      Published {new Date(h.publishedDate).toLocaleString()}
                    </Typography>
                  )}
                </Box>
              ))}
            </Stack>
          )}
        </Box>
      </Drawer>

      <CompareVersionsDialog
        open={compareOpen}
        versions={rows}
        canExport={canExportCompare}
        onClose={() => setCompareOpen(false)}
      />
    </Stack>
  );
};

export default ScheduleVersionsPage;
