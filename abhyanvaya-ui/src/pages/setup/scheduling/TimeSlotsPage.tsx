import { useCallback, useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
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
  Switch,
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
import {
  SessionKind,
  SlotKind,
  cloneTimeSlotSet,
  createTimeSlot,
  createTimeSlotSet,
  deleteTimeSlot,
  deleteTimeSlotSet,
  listAcademicYears,
  listTimeSlotSets,
  listTimeSlots,
  updateTimeSlot,
  updateTimeSlotSet,
  type CreateTimeSlotRequest,
  type CreateTimeSlotSetRequest,
  type TimeSlotDto,
  type TimeSlotSetDto,
} from "../../../services/schedulingService";
import { errMsg, formatTimeSpan, parseOptionalSelectNumber, toTimeSpan } from "./schedulingFormUtils";

const SLOT_KIND_LABELS: Record<number, string> = {
  [SlotKind.Period]: "Period",
  [SlotKind.Break]: "Break",
  [SlotKind.Lunch]: "Lunch",
  [SlotKind.WorkingSession]: "Working session",
};

const TimeSlotsPage = () => {
  const { hasPermission } = useAuth();
  const canManage = hasPermission(PermissionKeys.SchedulingManage);

  const [sets, setSets] = useState<TimeSlotSetDto[]>([]);
  const [selectedSetId, setSelectedSetId] = useState<number>(0);
  const [slots, setSlots] = useState<TimeSlotDto[]>([]);
  const [years, setYears] = useState<{ id: number; label: string }[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [setDialogOpen, setSetDialogOpen] = useState(false);
  const [slotDialogOpen, setSlotDialogOpen] = useState(false);
  const [cloneDialogOpen, setCloneDialogOpen] = useState(false);
  const [editingSetId, setEditingSetId] = useState(0);
  const [editingSlotId, setEditingSlotId] = useState(0);
  const [saving, setSaving] = useState(false);

  const setForm = useForm<CreateTimeSlotSetRequest>({
    defaultValues: { name: "", code: "", academicYearId: null, description: "", isDefault: false },
  });

  const slotForm = useForm<CreateTimeSlotRequest>({
    defaultValues: {
      timeSlotSetId: 0,
      periodNumber: 1,
      name: "",
      startTime: "09:00:00",
      endTime: "09:45:00",
      durationMinutes: 45,
      dayOfWeek: null,
      slotKind: SlotKind.Period,
      sessionKind: SessionKind.None,
    },
  });

  const cloneForm = useForm({ defaultValues: { sourceSetId: 0, name: "", code: "", academicYearId: null as number | null, isDefault: false } });

  const loadSets = useCallback(async () => {
    const res = await listTimeSlotSets();
    setSets(res.data);
    if (!selectedSetId && res.data[0]) setSelectedSetId(res.data[0].id);
  }, [selectedSetId]);

  const loadSlots = useCallback(async () => {
    if (!selectedSetId) {
      setSlots([]);
      return;
    }
    const res = await listTimeSlots(selectedSetId);
    setSlots(res.data);
  }, [selectedSetId]);

  useEffect(() => {
    void (async () => {
      setLoading(true);
      try {
        const y = await listAcademicYears();
        setYears(y.data.map((a) => ({ id: a.id, label: `${a.code} — ${a.name}` })));
        await loadSets();
      } catch (e) {
        setError(errMsg(e));
      } finally {
        setLoading(false);
      }
    })();
  }, [loadSets]);

  useEffect(() => {
    void loadSlots();
  }, [loadSlots]);

  const openSetDialog = (s?: TimeSlotSetDto) => {
    setEditingSetId(s?.id ?? 0);
    setForm.reset(
      s
        ? { name: s.name, code: s.code, academicYearId: s.academicYearId, description: s.description ?? "", isDefault: s.isDefault }
        : { name: "", code: "", academicYearId: null, description: "", isDefault: false },
    );
    setSetDialogOpen(true);
  };

  const saveSet = setForm.handleSubmit(async (values) => {
    setSaving(true);
    setError(null);
    try {
      const payload = { ...values, description: values.description || null };
      if (editingSetId) await updateTimeSlotSet(editingSetId, { ...payload, id: editingSetId });
      else await createTimeSlotSet(payload);
      setMessage("Time slot set saved.");
      setSetDialogOpen(false);
      await loadSets();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  });

  const openSlotDialog = (slot?: TimeSlotDto) => {
    setEditingSlotId(slot?.id ?? 0);
    slotForm.reset(
      slot
        ? { ...slot, startTime: slot.startTime, endTime: slot.endTime }
        : {
            timeSlotSetId: selectedSetId,
            periodNumber: slots.length + 1,
            name: "",
            startTime: "09:00:00",
            endTime: "09:45:00",
            durationMinutes: 45,
            dayOfWeek: null,
            slotKind: SlotKind.Period,
            sessionKind: SessionKind.None,
          },
    );
    setSlotDialogOpen(true);
  };

  const saveSlot = slotForm.handleSubmit(async (values) => {
    setSaving(true);
    setError(null);
    try {
      const payload = {
        ...values,
        timeSlotSetId: selectedSetId,
        startTime: toTimeSpan(values.startTime.slice(0, 5)),
        endTime: toTimeSpan(values.endTime.slice(0, 5)),
      };
      if (editingSlotId) await updateTimeSlot(editingSlotId, { ...payload, id: editingSlotId });
      else await createTimeSlot(payload);
      setMessage("Time slot saved.");
      setSlotDialogOpen(false);
      await loadSlots();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  });

  const saveClone = cloneForm.handleSubmit(async (values) => {
    setSaving(true);
    try {
      await cloneTimeSlotSet(values);
      setMessage("Time slot set cloned.");
      setCloneDialogOpen(false);
      await loadSets();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  });

  const handleDeleteSet = async (id: number) => {
    if (!window.confirm("Delete this time slot set?")) return;
    try {
      await deleteTimeSlotSet(id);
      setMessage("Set deleted.");
      if (selectedSetId === id) setSelectedSetId(0);
      await loadSets();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const handleDeleteSlot = async (id: number) => {
    if (!window.confirm("Delete this slot?")) return;
    try {
      await deleteTimeSlot(id);
      await loadSlots();
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
          Time slots
        </Typography>
      </Box>

      {message && <Alert severity="success">{message}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}

      {loading ? (
        <CircularProgress />
      ) : (
        <>
          <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", alignItems: "center" }}>
            <FormControl size="small" sx={{ minWidth: 220 }}>
              <InputLabel id="set-select">Time slot set</InputLabel>
              <Select labelId="set-select" label="Time slot set" value={selectedSetId || ""} onChange={(e) => setSelectedSetId(Number(e.target.value))}>
                {sets.map((s) => (
                  <MenuItem key={s.id} value={s.id}>
                    {s.code} — {s.name}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            {canManage && (
              <>
                <Button variant="contained" onClick={() => openSetDialog()}>
                  New set
                </Button>
                <Button variant="outlined" onClick={() => openSetDialog(sets.find((s) => s.id === selectedSetId))} disabled={!selectedSetId}>
                  Edit set
                </Button>
                <Button
                  variant="outlined"
                  onClick={() => {
                    cloneForm.reset({ sourceSetId: selectedSetId, name: "", code: "", academicYearId: null, isDefault: false });
                    setCloneDialogOpen(true);
                  }}
                  disabled={!selectedSetId}
                >
                  Clone set
                </Button>
                <Button variant="contained" onClick={() => openSlotDialog()} disabled={!selectedSetId}>
                  Add slot
                </Button>
              </>
            )}
          </Stack>

          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Period</TableCell>
                <TableCell>Name</TableCell>
                <TableCell>Kind</TableCell>
                <TableCell>Start</TableCell>
                <TableCell>End</TableCell>
                <TableCell>Duration</TableCell>
                {canManage && <TableCell align="right">Actions</TableCell>}
              </TableRow>
            </TableHead>
            <TableBody>
              {slots.map((s) => (
                <TableRow key={s.id} hover>
                  <TableCell>{s.periodNumber ?? "—"}</TableCell>
                  <TableCell>{s.name}</TableCell>
                  <TableCell>{SLOT_KIND_LABELS[s.slotKind] ?? s.slotKind}</TableCell>
                  <TableCell>{formatTimeSpan(s.startTime)}</TableCell>
                  <TableCell>{formatTimeSpan(s.endTime)}</TableCell>
                  <TableCell>{s.durationMinutes} min</TableCell>
                  {canManage && (
                    <TableCell align="right">
                      <Button size="small" onClick={() => openSlotDialog(s)}>
                        Edit
                      </Button>
                      <Button size="small" color="error" onClick={() => void handleDeleteSlot(s.id)}>
                        Delete
                      </Button>
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </>
      )}

      <Dialog open={setDialogOpen} onClose={() => !saving && setSetDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>{editingSetId ? "Edit set" : "New set"}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }} component="form" id="set-form" onSubmit={saveSet}>
            <Controller name="code" control={setForm.control} render={({ field }) => <TextField {...field} label="Code" fullWidth required />} />
            <Controller name="name" control={setForm.control} render={({ field }) => <TextField {...field} label="Name" fullWidth required />} />
            <Controller
              name="academicYearId"
              control={setForm.control}
              render={({ field }) => (
                <FormControl fullWidth>
                  <InputLabel id="year">Academic year (optional)</InputLabel>
                  <Select
                    labelId="year"
                    label="Academic year (optional)"
                    value={field.value ?? ""}
                    onChange={(e) => field.onChange(parseOptionalSelectNumber(e.target.value) || null)}
                  >
                    <MenuItem value="">None</MenuItem>
                    {years.map((y) => (
                      <MenuItem key={y.id} value={y.id}>
                        {y.label}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            />
            <Controller name="description" control={setForm.control} render={({ field }) => <TextField {...field} label="Description" fullWidth />} />
            <Controller
              name="isDefault"
              control={setForm.control}
              render={({ field }) => <FormControlLabel control={<Switch checked={field.value} onChange={(_, v) => field.onChange(v)} />} label="Default set" />}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          {editingSetId > 0 && canManage && (
            <Button color="error" onClick={() => void handleDeleteSet(editingSetId)} disabled={saving}>
              Delete
            </Button>
          )}
          <Box sx={{ flexGrow: 1 }} />
          <Button onClick={() => setSetDialogOpen(false)} disabled={saving}>
            Cancel
          </Button>
          <Button type="submit" form="set-form" variant="contained" disabled={saving}>
            Save
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={slotDialogOpen} onClose={() => !saving && setSlotDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>{editingSlotId ? "Edit slot" : "Add slot"}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }} component="form" id="slot-form" onSubmit={saveSlot}>
            <Controller name="name" control={slotForm.control} render={({ field }) => <TextField {...field} label="Name" fullWidth required />} />
            <Controller
              name="slotKind"
              control={slotForm.control}
              render={({ field }) => (
                <FormControl fullWidth>
                  <InputLabel id="kind">Slot kind</InputLabel>
                  <Select labelId="kind" label="Slot kind" value={field.value} onChange={(e) => field.onChange(Number(e.target.value))}>
                    {Object.entries(SLOT_KIND_LABELS).map(([k, v]) => (
                      <MenuItem key={k} value={Number(k)}>
                        {v}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            />
            <Controller
              name="periodNumber"
              control={slotForm.control}
              render={({ field }) => (
                <TextField
                  {...field}
                  value={field.value ?? ""}
                  label="Period number"
                  type="number"
                  fullWidth
                  onChange={(e) => field.onChange(e.target.value === "" ? null : Number(e.target.value))}
                />
              )}
            />
            <Controller
              name="startTime"
              control={slotForm.control}
              render={({ field }) => <TextField {...field} label="Start" type="time" fullWidth slotProps={{ inputLabel: { shrink: true } }} value={field.value.slice(0, 5)} onChange={(e) => field.onChange(`${e.target.value}:00`)} />}
            />
            <Controller
              name="endTime"
              control={slotForm.control}
              render={({ field }) => <TextField {...field} label="End" type="time" fullWidth slotProps={{ inputLabel: { shrink: true } }} value={field.value.slice(0, 5)} onChange={(e) => field.onChange(`${e.target.value}:00`)} />}
            />
            <Controller
              name="durationMinutes"
              control={slotForm.control}
              render={({ field }) => <TextField {...field} label="Duration (minutes)" type="number" fullWidth onChange={(e) => field.onChange(Number(e.target.value))} />}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setSlotDialogOpen(false)} disabled={saving}>
            Cancel
          </Button>
          <Button type="submit" form="slot-form" variant="contained" disabled={saving}>
            Save
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={cloneDialogOpen} onClose={() => !saving && setCloneDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>Clone time slot set</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }} component="form" id="clone-form" onSubmit={saveClone}>
            <Controller name="code" control={cloneForm.control} render={({ field }) => <TextField {...field} label="New code" fullWidth required />} />
            <Controller name="name" control={cloneForm.control} render={({ field }) => <TextField {...field} label="New name" fullWidth required />} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCloneDialogOpen(false)} disabled={saving}>
            Cancel
          </Button>
          <Button type="submit" form="clone-form" variant="contained" disabled={saving}>
            Clone
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default TimeSlotsPage;
