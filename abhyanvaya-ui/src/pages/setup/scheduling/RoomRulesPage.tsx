import { useCallback, useEffect, useState } from "react";
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
  FormControl,
  FormControlLabel,
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
import { Controller, useForm } from "react-hook-form";
import { PermissionKeys } from "../../../auth/permissionKeys";
import { useAuth } from "../../../context/AuthContext";
import { listMasterCourses } from "../../../services/setupService";
import {
  RoomType,
  createRoomRule,
  deleteRoomRule,
  listAcademicYears,
  listRoomRules,
  updateRoomRule,
  type CreateRoomAllocationRuleRequest,
  type RoomAllocationRuleDto,
} from "../../../services/schedulingService";
import { errMsg, parseOptionalSelectNumber } from "./schedulingFormUtils";

const ROOM_TYPE_LABELS: Record<number, string> = {
  [RoomType.Classroom]: "Classroom",
  [RoomType.ComputerLab]: "Computer lab",
  [RoomType.ScienceLab]: "Science lab",
  [RoomType.CommerceLab]: "Commerce lab",
  [RoomType.Seminar]: "Seminar",
  [RoomType.Auditorium]: "Auditorium",
  [RoomType.Other]: "Other",
};

const RoomRulesPage = () => {
  const { hasPermission } = useAuth();
  const canManage = hasPermission(PermissionKeys.SchedulingManage);

  const [rows, setRows] = useState<RoomAllocationRuleDto[]>([]);
  const [years, setYears] = useState<{ id: number; label: string }[]>([]);
  const [courses, setCourses] = useState<{ id: number; name: string }[]>([]);
  const [filterYearId, setFilterYearId] = useState<number | "">("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingId, setEditingId] = useState(0);
  const [saving, setSaving] = useState(false);

  const form = useForm<CreateRoomAllocationRuleRequest>({
    defaultValues: {
      name: "",
      academicYearId: null,
      roomType: null,
      minCapacity: null,
      maxCapacity: null,
      departmentId: null,
      courseId: null,
      requireComputerLab: false,
      requireScienceLab: false,
      requireCommerceLab: false,
      requireAiCamera: false,
      requireProjector: false,
      requireSmartBoard: false,
      preferredRoomId: null,
      priority: 100,
      notes: "",
    },
  });

  useEffect(() => {
    void (async () => {
      try {
        const [y, c] = await Promise.all([listAcademicYears(), listMasterCourses()]);
        setYears(y.data.map((a) => ({ id: a.id, label: `${a.code} — ${a.name}` })));
        setCourses(c.data.map((x) => ({ id: x.id, name: x.name })));
      } catch (e) {
        setError(errMsg(e));
      }
    })();
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await listRoomRules(filterYearId || undefined);
      setRows(res.data);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  }, [filterYearId]);

  useEffect(() => {
    void load();
  }, [load]);

  const openAdd = () => {
    setEditingId(0);
    form.reset({
      name: "",
      academicYearId: typeof filterYearId === "number" ? filterYearId : null,
      roomType: null,
      minCapacity: null,
      maxCapacity: null,
      departmentId: null,
      courseId: null,
      requireComputerLab: false,
      requireScienceLab: false,
      requireCommerceLab: false,
      requireAiCamera: false,
      requireProjector: false,
      requireSmartBoard: false,
      preferredRoomId: null,
      priority: 100,
      notes: "",
    });
    setDialogOpen(true);
  };

  const openEdit = (r: RoomAllocationRuleDto) => {
    setEditingId(r.id);
    form.reset({ ...r, notes: r.notes ?? "" });
    setDialogOpen(true);
  };

  const save = form.handleSubmit(async (values) => {
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      const payload = { ...values, notes: values.notes || null };
      if (editingId) await updateRoomRule(editingId, { ...payload, id: editingId });
      else await createRoomRule(payload);
      setMessage(editingId ? "Rule updated." : "Rule created.");
      setDialogOpen(false);
      await load();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  });

  const handleDelete = async (id: number) => {
    if (!window.confirm("Delete this rule?")) return;
    try {
      await deleteRoomRule(id);
      setMessage("Rule deleted.");
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
          Room rules
        </Typography>
        {canManage && (
          <Button variant="contained" onClick={openAdd}>
            Add rule
          </Button>
        )}
      </Box>

      <FormControl size="small" sx={{ minWidth: 220 }}>
        <InputLabel id="year">Academic year</InputLabel>
        <Select labelId="year" label="Academic year" value={filterYearId === "" ? "" : filterYearId} onChange={(e) => setFilterYearId(parseOptionalSelectNumber(e.target.value))}>
          <MenuItem value="">All years</MenuItem>
          {years.map((y) => (
            <MenuItem key={y.id} value={y.id}>
              {y.label}
            </MenuItem>
          ))}
        </Select>
      </FormControl>

      {message && <Alert severity="success">{message}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}

      {loading ? (
        <CircularProgress />
      ) : (
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Name</TableCell>
              <TableCell>Room type</TableCell>
              <TableCell>Capacity</TableCell>
              <TableCell>Priority</TableCell>
              {canManage && <TableCell align="right">Actions</TableCell>}
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((r) => (
              <TableRow key={r.id} hover>
                <TableCell>{r.name}</TableCell>
                <TableCell>{r.roomType != null ? ROOM_TYPE_LABELS[r.roomType] ?? r.roomType : "Any"}</TableCell>
                <TableCell>
                  {r.minCapacity ?? "—"} – {r.maxCapacity ?? "—"}
                </TableCell>
                <TableCell>{r.priority}</TableCell>
                {canManage && (
                  <TableCell align="right">
                    <Button size="small" onClick={() => openEdit(r)}>
                      Edit
                    </Button>
                    <Button size="small" color="error" onClick={() => void handleDelete(r.id)}>
                      Delete
                    </Button>
                  </TableCell>
                )}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} fullWidth maxWidth="md">
        <DialogTitle>{editingId ? "Edit rule" : "Add rule"}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }} component="form" id="rule-form" onSubmit={save}>
            <Controller name="name" control={form.control} render={({ field }) => <TextField {...field} label="Name" fullWidth required />} />
            <Controller
              name="academicYearId"
              control={form.control}
              render={({ field }) => (
                <FormControl fullWidth>
                  <InputLabel id="ay">Academic year</InputLabel>
                  <Select
                    labelId="ay"
                    label="Academic year"
                    value={field.value ?? ""}
                    onChange={(e) => field.onChange(parseOptionalSelectNumber(e.target.value) || null)}
                  >
                    <MenuItem value="">Any</MenuItem>
                    {years.map((y) => (
                      <MenuItem key={y.id} value={y.id}>
                        {y.label}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            />
            <Controller
              name="roomType"
              control={form.control}
              render={({ field }) => (
                <FormControl fullWidth>
                  <InputLabel id="rt">Room type</InputLabel>
                  <Select
                    labelId="rt"
                    label="Room type"
                    value={field.value ?? ""}
                    onChange={(e) => field.onChange(parseOptionalSelectNumber(e.target.value) || null)}
                  >
                    <MenuItem value="">Any</MenuItem>
                    {Object.entries(ROOM_TYPE_LABELS).map(([k, v]) => (
                      <MenuItem key={k} value={Number(k)}>
                        {v}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            />
            <Stack direction="row" spacing={2}>
              <Controller
                name="minCapacity"
                control={form.control}
                render={({ field }) => (
                  <TextField
                    {...field}
                    value={field.value ?? ""}
                    label="Min capacity"
                    type="number"
                    fullWidth
                    onChange={(e) => field.onChange(parseOptionalSelectNumber(e.target.value) || null)}
                  />
                )}
              />
              <Controller
                name="maxCapacity"
                control={form.control}
                render={({ field }) => (
                  <TextField
                    {...field}
                    value={field.value ?? ""}
                    label="Max capacity"
                    type="number"
                    fullWidth
                    onChange={(e) => field.onChange(parseOptionalSelectNumber(e.target.value) || null)}
                  />
                )}
              />
              <Controller
                name="priority"
                control={form.control}
                render={({ field }) => <TextField {...field} label="Priority" type="number" fullWidth onChange={(e) => field.onChange(Number(e.target.value))} />}
              />
            </Stack>
            <Controller
              name="courseId"
              control={form.control}
              render={({ field }) => (
                <FormControl fullWidth>
                  <InputLabel id="course">Course</InputLabel>
                  <Select
                    labelId="course"
                    label="Course"
                    value={field.value ?? ""}
                    onChange={(e) => field.onChange(parseOptionalSelectNumber(e.target.value) || null)}
                  >
                    <MenuItem value="">Any</MenuItem>
                    {courses.map((c) => (
                      <MenuItem key={c.id} value={c.id}>
                        {c.name}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            />
            <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap" }}>
              <Controller name="requireComputerLab" control={form.control} render={({ field }) => <FormControlLabel control={<Checkbox checked={field.value} onChange={(_, v) => field.onChange(v)} />} label="Computer lab" />} />
              <Controller name="requireScienceLab" control={form.control} render={({ field }) => <FormControlLabel control={<Checkbox checked={field.value} onChange={(_, v) => field.onChange(v)} />} label="Science lab" />} />
              <Controller name="requireCommerceLab" control={form.control} render={({ field }) => <FormControlLabel control={<Checkbox checked={field.value} onChange={(_, v) => field.onChange(v)} />} label="Commerce lab" />} />
              <Controller name="requireAiCamera" control={form.control} render={({ field }) => <FormControlLabel control={<Checkbox checked={field.value} onChange={(_, v) => field.onChange(v)} />} label="AI camera" />} />
              <Controller name="requireProjector" control={form.control} render={({ field }) => <FormControlLabel control={<Checkbox checked={field.value} onChange={(_, v) => field.onChange(v)} />} label="Projector" />} />
              <Controller name="requireSmartBoard" control={form.control} render={({ field }) => <FormControlLabel control={<Checkbox checked={field.value} onChange={(_, v) => field.onChange(v)} />} label="Smart board" />} />
            </Stack>
            <Controller name="notes" control={form.control} render={({ field }) => <TextField {...field} label="Notes" fullWidth multiline minRows={2} />} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)} disabled={saving}>
            Cancel
          </Button>
          <Button type="submit" form="rule-form" variant="contained" disabled={saving}>
            Save
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default RoomRulesPage;
