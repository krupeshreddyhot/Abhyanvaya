import { useCallback, useEffect, useMemo, useState } from "react";
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
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import ViewWeekIcon from "@mui/icons-material/ViewWeek";
import CalendarMonthIcon from "@mui/icons-material/CalendarMonth";
import TimelineIcon from "@mui/icons-material/Timeline";
import { Controller, useForm } from "react-hook-form";
import { PermissionKeys } from "../../../auth/permissionKeys";
import { useAuth } from "../../../context/AuthContext";
import {
  createRoomAvailability,
  deleteRoomAvailability,
  listAcademicYears,
  listRoomAvailability,
  searchRooms,
  updateRoomAvailability,
  type CreateRoomAvailabilityRequest,
  type RoomAvailabilityDto,
} from "../../../services/schedulingService";
import AvailabilityViews, { type AvailabilityViewMode } from "./AvailabilityViews";
import {
  addDays,
  daysBetween,
  formatDateOnly,
  parseDateOnly,
  type AvailabilityEntry,
} from "./availabilityDateUtils";
import { errMsg, parseOptionalSelectNumber } from "./schedulingFormUtils";
import { ROOM_AVAILABILITY_COLORS, ROOM_AVAILABILITY_LABELS } from "./schedulingEnumLabels";

const RoomAvailabilityPage = () => {
  const { hasPermission } = useAuth();
  const canManage = hasPermission(PermissionKeys.SchedulingRoomAvailabilityManage);

  const [rows, setRows] = useState<RoomAvailabilityDto[]>([]);
  const [years, setYears] = useState<{ id: number; label: string }[]>([]);
  const [rooms, setRooms] = useState<{ id: number; label: string }[]>([]);
  const [filterYearId, setFilterYearId] = useState<number | "">("");
  const [filterRoomId, setFilterRoomId] = useState<number | "">("");
  const [viewMode, setViewMode] = useState<AvailabilityViewMode>("weekly");
  const [weekAnchor, setWeekAnchor] = useState(() => new Date());
  const [monthAnchor, setMonthAnchor] = useState(() => new Date());
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingId, setEditingId] = useState(0);
  const [saving, setSaving] = useState(false);

  const form = useForm<CreateRoomAvailabilityRequest>({
    defaultValues: {
      roomId: 0,
      academicYearId: 0,
      availabilityType: 2,
      startDate: "",
      endDate: "",
      startSlotId: null,
      endSlotId: null,
      reason: "",
    },
  });

  useEffect(() => {
    void (async () => {
      try {
        const [y, rm] = await Promise.all([
          listAcademicYears(),
          searchRooms({ page: 1, pageSize: 500, isActive: true }),
        ]);
        setYears(y.data.map((a) => ({ id: a.id, label: `${a.code} — ${a.name}` })));
        setRooms(rm.data.items.map((r) => ({ id: r.id, label: `${r.code} — ${r.name}` })));
        const current = y.data.find((a) => a.isCurrent) ?? y.data[0];
        if (current) setFilterYearId(current.id);
      } catch (e) {
        setError(errMsg(e));
      }
    })();
  }, []);

  const load = useCallback(async () => {
    if (!filterYearId) return;
    setLoading(true);
    setError(null);
    try {
      const res = await listRoomAvailability({
        academicYearId: filterYearId,
        roomId: filterRoomId || undefined,
      });
      setRows(res.data);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  }, [filterYearId, filterRoomId]);

  useEffect(() => {
    void load();
  }, [load]);

  const calendarEntries: AvailabilityEntry[] = useMemo(
    () =>
      rows.map((r) => ({
        id: r.id,
        startDate: r.startDate,
        endDate: r.endDate,
        availabilityType: r.availabilityType,
        label: rooms.find((x) => x.id === r.roomId)?.label,
      })),
    [rows, rooms],
  );

  const openAdd = () => {
    setEditingId(0);
    form.reset({
      roomId: typeof filterRoomId === "number" ? filterRoomId : rooms[0]?.id ?? 0,
      academicYearId: typeof filterYearId === "number" ? filterYearId : years[0]?.id ?? 0,
      availabilityType: 2,
      startDate: new Date().toISOString().slice(0, 10),
      endDate: new Date().toISOString().slice(0, 10),
      startSlotId: null,
      endSlotId: null,
      reason: "",
    });
    setDialogOpen(true);
  };

  const openEdit = (entry: AvailabilityEntry) => {
    const r = rows.find((x) => x.id === entry.id);
    if (!r) return;
    setEditingId(r.id);
    form.reset({ ...r, reason: r.reason ?? "" });
    setDialogOpen(true);
  };

  const save = form.handleSubmit(async (values) => {
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      const payload = { ...values, reason: values.reason || null };
      if (editingId) await updateRoomAvailability(editingId, { ...payload, id: editingId });
      else await createRoomAvailability(payload);
      setMessage(editingId ? "Entry updated." : "Entry created.");
      setDialogOpen(false);
      await load();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  });

  const handleDelete = async (id: number) => {
    if (!window.confirm("Delete this availability entry?")) return;
    try {
      await deleteRoomAvailability(id);
      setMessage("Entry deleted.");
      await load();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const handleEntryMove = async (entry: AvailabilityEntry, targetDay: Date) => {
    const r = rows.find((x) => x.id === entry.id);
    if (!r) return;
    const oldStart = parseDateOnly(r.startDate);
    const oldEnd = parseDateOnly(r.endDate);
    const span = daysBetween(oldStart, oldEnd);
    const newStart = formatDateOnly(targetDay);
    const newEnd = formatDateOnly(addDays(targetDay, span));
    try {
      await updateRoomAvailability(r.id, { ...r, startDate: newStart, endDate: newEnd });
      setMessage("Entry moved.");
      await load();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const roomLabel = (id: number) => rooms.find((r) => r.id === id)?.label ?? id;

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />} variant="text">
          Scheduling
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Room availability
        </Typography>
        {canManage && (
          <Button variant="contained" onClick={openAdd}>
            Add entry
          </Button>
        )}
      </Box>

      <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", alignItems: "center" }} useFlexGap>
        <FormControl size="small" sx={{ minWidth: 160 }}>
          <InputLabel id="fy">Year</InputLabel>
          <Select labelId="fy" label="Year" value={filterYearId} onChange={(e) => setFilterYearId(Number(e.target.value))}>
            {years.map((y) => (
              <MenuItem key={y.id} value={y.id}>
                {y.label}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
        <FormControl size="small" sx={{ minWidth: 160 }}>
          <InputLabel id="fr">Room</InputLabel>
          <Select labelId="fr" label="Room" value={filterRoomId === "" ? "" : filterRoomId} onChange={(e) => setFilterRoomId(parseOptionalSelectNumber(e.target.value))}>
            <MenuItem value="">All</MenuItem>
            {rooms.map((r) => (
              <MenuItem key={r.id} value={r.id}>
                {r.label}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
        <ToggleButtonGroup size="small" exclusive value={viewMode} onChange={(_, v) => v && setViewMode(v)}>
          <ToggleButton value="weekly">
            <ViewWeekIcon fontSize="small" sx={{ mr: 0.5 }} /> Weekly
          </ToggleButton>
          <ToggleButton value="monthly">
            <CalendarMonthIcon fontSize="small" sx={{ mr: 0.5 }} /> Monthly
          </ToggleButton>
          <ToggleButton value="timeline">
            <TimelineIcon fontSize="small" sx={{ mr: 0.5 }} /> Timeline
          </ToggleButton>
        </ToggleButtonGroup>
      </Stack>

      {message && <Alert severity="success">{message}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}

      {loading ? (
        <CircularProgress />
      ) : (
        <>
          <AvailabilityViews
            entries={calendarEntries}
            viewMode={viewMode}
            weekAnchor={weekAnchor}
            monthAnchor={monthAnchor}
            onWeekAnchorChange={setWeekAnchor}
            onMonthAnchorChange={setMonthAnchor}
            typeLabels={ROOM_AVAILABILITY_LABELS}
            typeColors={ROOM_AVAILABILITY_COLORS}
            canManage={canManage}
            onEntryClick={openEdit}
            onEntryMove={canManage ? (e, d) => void handleEntryMove(e, d) : undefined}
          />

          <Typography variant="subtitle1">All entries</Typography>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Room</TableCell>
                <TableCell>Type</TableCell>
                <TableCell>Start</TableCell>
                <TableCell>End</TableCell>
                <TableCell>Reason</TableCell>
                {canManage && <TableCell align="right">Actions</TableCell>}
              </TableRow>
            </TableHead>
            <TableBody>
              {rows.map((r) => (
                <TableRow key={r.id} hover>
                  <TableCell>{roomLabel(r.roomId)}</TableCell>
                  <TableCell>{ROOM_AVAILABILITY_LABELS[r.availabilityType] ?? r.availabilityType}</TableCell>
                  <TableCell>{r.startDate}</TableCell>
                  <TableCell>{r.endDate}</TableCell>
                  <TableCell>{r.reason ?? "—"}</TableCell>
                  {canManage && (
                    <TableCell align="right">
                      <Button size="small" onClick={() => openEdit({ id: r.id, startDate: r.startDate, endDate: r.endDate, availabilityType: r.availabilityType })}>
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
        </>
      )}

      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>{editingId ? "Edit availability" : "Add availability"}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }} component="form" id="ra-form" onSubmit={save}>
            <Controller
              name="roomId"
              control={form.control}
              render={({ field }) => (
                <FormControl fullWidth>
                  <InputLabel id="room">Room</InputLabel>
                  <Select labelId="room" label="Room" value={field.value} onChange={(e) => field.onChange(Number(e.target.value))}>
                    {rooms.map((r) => (
                      <MenuItem key={r.id} value={r.id}>
                        {r.label}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            />
            <Controller
              name="availabilityType"
              control={form.control}
              render={({ field }) => (
                <FormControl fullWidth>
                  <InputLabel id="type">Type</InputLabel>
                  <Select labelId="type" label="Type" value={field.value} onChange={(e) => field.onChange(Number(e.target.value))}>
                    {Object.entries(ROOM_AVAILABILITY_LABELS).map(([k, v]) => (
                      <MenuItem key={k} value={Number(k)}>
                        {v}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            />
            <Controller name="startDate" control={form.control} render={({ field }) => <TextField {...field} label="Start date" type="date" fullWidth slotProps={{ inputLabel: { shrink: true } }} />} />
            <Controller name="endDate" control={form.control} render={({ field }) => <TextField {...field} label="End date" type="date" fullWidth slotProps={{ inputLabel: { shrink: true } }} />} />
            <Controller name="reason" control={form.control} render={({ field }) => <TextField {...field} label="Reason" fullWidth />} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)} disabled={saving}>
            Cancel
          </Button>
          <Button type="submit" form="ra-form" variant="contained" disabled={saving}>
            Save
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default RoomAvailabilityPage;
