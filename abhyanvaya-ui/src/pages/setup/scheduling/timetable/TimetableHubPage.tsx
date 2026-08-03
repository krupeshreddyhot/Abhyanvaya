import { useCallback, useEffect, useMemo, useState } from "react";
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
import { Link as RouterLink, useNavigate } from "react-router-dom";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import { PermissionKeys } from "../../../../auth/permissionKeys";
import { useAuth } from "../../../../context/AuthContext";
import { listDepartments } from "../../../../services/setupService";
import {
  createTimetable,
  deleteTimetable,
  listAcademicYears,
  listTimeSlotSets,
  listTimetables,
  TimetableStatus,
  type CreateTimetableRequest,
  type TimetableDto,
} from "../../../../services/schedulingService";
import { errMsg, parseOptionalSelectNumber } from "../schedulingFormUtils";
import { TIMETABLE_STATUS_COLORS, TIMETABLE_STATUS_LABELS } from "./timetableUtils";

const TimetableHubPage = () => {
  const navigate = useNavigate();
  const { hasPermission } = useAuth();
  const canManage = hasPermission(PermissionKeys.SchedulingTimetableManage);

  const [years, setYears] = useState<{ id: number; label: string }[]>([]);
  const [departments, setDepartments] = useState<{ id: number; name: string }[]>([]);
  const [slotSets, setSlotSets] = useState<{ id: number; label: string }[]>([]);
  const [rows, setRows] = useState<TimetableDto[]>([]);
  const [filterYearId, setFilterYearId] = useState<number | "">("");
  const [filterStatus, setFilterStatus] = useState<TimetableStatus | "">("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [form, setForm] = useState<CreateTimetableRequest>({
    name: "",
    code: "",
    academicYearId: 0,
    departmentId: null,
    timeSlotSetId: null,
    notes: "",
  });
  const [saving, setSaving] = useState(false);

  const loadRows = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await listTimetables({
        academicYearId: filterYearId === "" ? undefined : filterYearId,
        status: filterStatus === "" ? undefined : filterStatus,
      });
      setRows(res.data);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  }, [filterYearId, filterStatus]);

  useEffect(() => {
    void (async () => {
      try {
        const [y, d] = await Promise.all([
          listAcademicYears(),
          listDepartments(undefined, true),
        ]);
        setYears(y.data.map((a) => ({ id: a.id, label: `${a.code} — ${a.name}` })));
        setDepartments(d.data.map((x) => ({ id: x.id, name: x.name })));
        const current = y.data.find((a) => a.isCurrent) ?? y.data[0];
        if (current) {
          setFilterYearId(current.id);
          setForm((f) => ({ ...f, academicYearId: current.id }));
          const sets = await listTimeSlotSets(current.id);
          setSlotSets(sets.data.map((s) => ({ id: s.id, label: `${s.code} — ${s.name}` })));
        }
      } catch (e) {
        setError(errMsg(e));
      }
    })();
  }, []);

  useEffect(() => {
    void loadRows();
  }, [loadRows]);

  useEffect(() => {
    if (form.academicYearId) {
      void listTimeSlotSets(form.academicYearId).then((res) =>
        setSlotSets(res.data.map((s) => ({ id: s.id, label: `${s.code} — ${s.name}` }))),
      );
    }
  }, [form.academicYearId]);

  const draftCount = useMemo(() => rows.filter((r) => r.status === TimetableStatus.Draft).length, [rows]);
  const lockedCount = useMemo(() => rows.filter((r) => r.status === TimetableStatus.Locked).length, [rows]);

  const openCreate = () => {
    setForm({
      name: "",
      code: "",
      academicYearId: filterYearId === "" ? years[0]?.id ?? 0 : filterYearId,
      departmentId: null,
      timeSlotSetId: null,
      notes: "",
    });
    setDialogOpen(true);
  };

  const handleCreate = async () => {
    if (!form.name.trim() || !form.academicYearId) {
      setError("Name and academic year are required.");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      const res = await createTimetable({
        ...form,
        name: form.name.trim(),
        code: form.code?.trim() || null,
        notes: form.notes?.trim() || null,
      });
      setDialogOpen(false);
      setMessage("Timetable created.");
      navigate(`/setup/scheduling/timetables/${res.data.id}`);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (id: number) => {
    if (!window.confirm("Delete this timetable?")) return;
    try {
      await deleteTimetable(id);
      setMessage("Timetable deleted.");
      void loadRows();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  return (
    <Stack spacing={2} className="timetable-hub">
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }} className="no-print">
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />} variant="text">
          Scheduling
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Timetable designer
        </Typography>
        {canManage && (
          <Button variant="contained" startIcon={<AddIcon />} onClick={openCreate}>
            Create timetable
          </Button>
        )}
      </Box>

      {error && <Alert severity="error">{error}</Alert>}
      {message && (
        <Alert severity="success" onClose={() => setMessage(null)}>
          {message}
        </Alert>
      )}

      <Stack direction={{ xs: "column", sm: "row" }} spacing={2} className="no-print">
        <FormControl sx={{ minWidth: 200 }}>
          <InputLabel>Academic year</InputLabel>
          <Select
            label="Academic year"
            value={filterYearId}
            onChange={(e) => setFilterYearId(parseOptionalSelectNumber(e.target.value))}
          >
            {years.map((y) => (
              <MenuItem key={y.id} value={y.id}>
                {y.label}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
        <FormControl sx={{ minWidth: 160 }}>
          <InputLabel>Status</InputLabel>
          <Select
            label="Status"
            value={filterStatus}
            onChange={(e) => setFilterStatus(parseOptionalSelectNumber(e.target.value) as TimetableStatus | "")}
          >
            <MenuItem value="">All</MenuItem>
            <MenuItem value={TimetableStatus.Draft}>Draft</MenuItem>
            <MenuItem value={TimetableStatus.Locked}>Locked</MenuItem>
          </Select>
        </FormControl>
        <Chip label={`${draftCount} drafts`} color="warning" variant="outlined" />
        <Chip label={`${lockedCount} locked`} color="success" variant="outlined" />
      </Stack>

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", p: 4 }}>
          <CircularProgress />
        </Box>
      ) : (
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Name</TableCell>
              <TableCell>Year</TableCell>
              <TableCell>Department</TableCell>
              <TableCell>Time slot set</TableCell>
              <TableCell>Status</TableCell>
              <TableCell align="right">Entries</TableCell>
              <TableCell align="right" className="no-print">
                Actions
              </TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((r) => (
              <TableRow key={r.id} hover>
                <TableCell>{r.name}</TableCell>
                <TableCell>{r.academicYearName ?? r.academicYearId}</TableCell>
                <TableCell>{r.departmentName ?? "—"}</TableCell>
                <TableCell>{r.timeSlotSetName ?? "—"}</TableCell>
                <TableCell>
                  <Chip
                    size="small"
                    label={TIMETABLE_STATUS_LABELS[r.status] ?? r.status}
                    color={TIMETABLE_STATUS_COLORS[r.status] ?? "default"}
                  />
                </TableCell>
                <TableCell align="right">{r.entryCount}</TableCell>
                <TableCell align="right" className="no-print">
                  <IconButton
                    size="small"
                    title="Open designer"
                    onClick={() => navigate(`/setup/scheduling/timetables/${r.id}`)}
                  >
                    <OpenInNewIcon fontSize="small" />
                  </IconButton>
                  {canManage && r.status === TimetableStatus.Draft && (
                    <IconButton size="small" color="error" title="Delete" onClick={() => void handleDelete(r.id)}>
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  )}
                </TableCell>
              </TableRow>
            ))}
            {rows.length === 0 && (
              <TableRow>
                <TableCell colSpan={7} align="center">
                  No timetables found.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      )}

      <Dialog open={dialogOpen} onClose={() => setDialogOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Create timetable</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              label="Name"
              value={form.name}
              onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
              required
              fullWidth
            />
            <TextField
              label="Code"
              value={form.code ?? ""}
              onChange={(e) => setForm((f) => ({ ...f, code: e.target.value }))}
              fullWidth
            />
            <FormControl fullWidth required>
              <InputLabel>Academic year</InputLabel>
              <Select
                label="Academic year"
                value={form.academicYearId || ""}
                onChange={(e) =>
                  setForm((f) => ({ ...f, academicYearId: Number(e.target.value) }))
                }
              >
                {years.map((y) => (
                  <MenuItem key={y.id} value={y.id}>
                    {y.label}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <FormControl fullWidth>
              <InputLabel>Department (optional)</InputLabel>
              <Select
                label="Department (optional)"
                value={form.departmentId ?? ""}
                onChange={(e) => {
                  const v = parseOptionalSelectNumber(e.target.value);
                  setForm((f) => ({ ...f, departmentId: v === "" ? null : v }));
                }}
              >
                <MenuItem value="">None</MenuItem>
                {departments.map((d) => (
                  <MenuItem key={d.id} value={d.id}>
                    {d.name}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <FormControl fullWidth>
              <InputLabel>Time slot set</InputLabel>
              <Select
                label="Time slot set"
                value={form.timeSlotSetId ?? ""}
                onChange={(e) => {
                  const v = parseOptionalSelectNumber(e.target.value);
                  setForm((f) => ({ ...f, timeSlotSetId: v === "" ? null : v }));
                }}
              >
                <MenuItem value="">Default</MenuItem>
                {slotSets.map((s) => (
                  <MenuItem key={s.id} value={s.id}>
                    {s.label}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <TextField
              label="Notes"
              value={form.notes ?? ""}
              onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value }))}
              fullWidth
              multiline
              minRows={2}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={() => void handleCreate()} disabled={saving}>
            Create & open
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default TimetableHubPage;
