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
import { listStaff } from "../../../services/setupService";
import {
  createFacultyAvailability,
  deleteFacultyAvailability,
  listAcademicYears,
  listFacultyAvailability,
  updateFacultyAvailability,
  type CreateFacultyAvailabilityRequest,
  type FacultyAvailabilityDto,
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
import { FACULTY_AVAILABILITY_COLORS, FACULTY_AVAILABILITY_LABELS } from "./schedulingEnumLabels";

const FacultyAvailabilityPage = () => {
  const { hasPermission } = useAuth();
  const canManage = hasPermission(PermissionKeys.SchedulingFacultyAvailabilityManage);

  const [rows, setRows] = useState<FacultyAvailabilityDto[]>([]);
  const [years, setYears] = useState<{ id: number; label: string }[]>([]);
  const [staff, setStaff] = useState<{ id: number; label: string }[]>([]);
  const [filterYearId, setFilterYearId] = useState<number | "">("");
  const [filterStaffId, setFilterStaffId] = useState<number | "">("");
  const [viewMode, setViewMode] = useState<AvailabilityViewMode>("weekly");
  const [weekAnchor, setWeekAnchor] = useState(() => new Date());
  const [monthAnchor, setMonthAnchor] = useState(() => new Date());
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingId, setEditingId] = useState(0);
  const [saving, setSaving] = useState(false);

  const form = useForm<CreateFacultyAvailabilityRequest>({
    defaultValues: {
      staffId: 0,
      academicYearId: 0,
      availabilityType: 2,
      startDate: "",
      endDate: "",
      startSlotId: null,
      endSlotId: null,
      reason: "",
      remarks: "",
    },
  });

  useEffect(() => {
    void (async () => {
      try {
        const [y, st] = await Promise.all([
          listAcademicYears(),
          listStaff({ page: 1, pageSize: 500 }),
        ]);
        setYears(y.data.map((a) => ({ id: a.id, label: `${a.code} — ${a.name}` })));
        setStaff(st.data.items.map((s) => ({ id: s.id, label: `${s.firstName} ${s.lastName}` })));
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
      const res = await listFacultyAvailability({
        academicYearId: filterYearId,
        staffId: filterStaffId || undefined,
      });
      setRows(res.data);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  }, [filterYearId, filterStaffId]);

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
        label: staff.find((s) => s.id === r.staffId)?.label,
      })),
    [rows, staff],
  );

  const openAdd = () => {
    setEditingId(0);
    form.reset({
      staffId: typeof filterStaffId === "number" ? filterStaffId : staff[0]?.id ?? 0,
      academicYearId: typeof filterYearId === "number" ? filterYearId : years[0]?.id ?? 0,
      availabilityType: 2,
      startDate: new Date().toISOString().slice(0, 10),
      endDate: new Date().toISOString().slice(0, 10),
      startSlotId: null,
      endSlotId: null,
      reason: "",
      remarks: "",
    });
    setDialogOpen(true);
  };

  const openEdit = (entry: AvailabilityEntry) => {
    const r = rows.find((x) => x.id === entry.id);
    if (!r) return;
    setEditingId(r.id);
    form.reset({ ...r, reason: r.reason ?? "", remarks: r.remarks ?? "" });
    setDialogOpen(true);
  };

  const save = form.handleSubmit(async (values) => {
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      const payload = { ...values, reason: values.reason || null, remarks: values.remarks || null };
      if (editingId) await updateFacultyAvailability(editingId, { ...payload, id: editingId });
      else await createFacultyAvailability(payload);
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
      await deleteFacultyAvailability(id);
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
      await updateFacultyAvailability(r.id, {
        ...r,
        startDate: newStart,
        endDate: newEnd,
      });
      setMessage("Entry moved.");
      await load();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const staffLabel = (id: number) => staff.find((s) => s.id === id)?.label ?? id;

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />} variant="text">
          Scheduling
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Faculty availability
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
          <InputLabel id="fst">Staff</InputLabel>
          <Select labelId="fst" label="Staff" value={filterStaffId === "" ? "" : filterStaffId} onChange={(e) => setFilterStaffId(parseOptionalSelectNumber(e.target.value))}>
            <MenuItem value="">All</MenuItem>
            {staff.map((s) => (
              <MenuItem key={s.id} value={s.id}>
                {s.label}
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
            typeLabels={FACULTY_AVAILABILITY_LABELS}
            typeColors={FACULTY_AVAILABILITY_COLORS}
            canManage={canManage}
            onEntryClick={openEdit}
            onEntryMove={canManage ? (e, d) => void handleEntryMove(e, d) : undefined}
          />

          <Typography variant="subtitle1">All entries</Typography>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Staff</TableCell>
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
                  <TableCell>{staffLabel(r.staffId)}</TableCell>
                  <TableCell>{FACULTY_AVAILABILITY_LABELS[r.availabilityType] ?? r.availabilityType}</TableCell>
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
          <Stack spacing={2} sx={{ mt: 1 }} component="form" id="fa-form" onSubmit={save}>
            <Controller
              name="staffId"
              control={form.control}
              render={({ field }) => (
                <FormControl fullWidth>
                  <InputLabel id="staff">Staff</InputLabel>
                  <Select labelId="staff" label="Staff" value={field.value} onChange={(e) => field.onChange(Number(e.target.value))}>
                    {staff.map((s) => (
                      <MenuItem key={s.id} value={s.id}>
                        {s.label}
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
                    {Object.entries(FACULTY_AVAILABILITY_LABELS).map(([k, v]) => (
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
            <Controller name="remarks" control={form.control} render={({ field }) => <TextField {...field} label="Remarks" fullWidth multiline minRows={2} />} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)} disabled={saving}>
            Cancel
          </Button>
          <Button type="submit" form="fa-form" variant="contained" disabled={saving}>
            Save
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default FacultyAvailabilityPage;
