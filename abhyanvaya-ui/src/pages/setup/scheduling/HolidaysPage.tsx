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
import {
  HolidayType,
  createHoliday,
  deleteHoliday,
  listAcademicYears,
  listHolidayTypes,
  listHolidays,
  updateHoliday,
  type AcademicYearDto,
  type CreateHolidayRequest,
  type HolidayDto,
  type HolidayTypeCatalogDto,
} from "../../../services/schedulingService";
import { errMsg, parseOptionalSelectNumber } from "./schedulingFormUtils";

const HOLIDAY_TYPE_LABELS: Record<number, string> = {
  [HolidayType.National]: "National",
  [HolidayType.University]: "University",
  [HolidayType.College]: "College",
  [HolidayType.Exam]: "Exam",
  [HolidayType.Unexpected]: "Unexpected",
};

const defaultHolidayForm = (yearId: number): CreateHolidayRequest => ({
  academicYearId: yearId,
  name: "",
  date: "",
  holidayType: HolidayType.National,
  description: "",
  holidayTypeCatalogId: null,
  isWorkingDayOverride: false,
  requiresRescheduling: false,
  colour: null,
  priority: null,
});

const HolidaysPage = () => {
  const { hasPermission } = useAuth();
  const canManage = hasPermission(PermissionKeys.SchedulingManage);

  const [years, setYears] = useState<AcademicYearDto[]>([]);
  const [catalogTypes, setCatalogTypes] = useState<HolidayTypeCatalogDto[]>([]);
  const [yearId, setYearId] = useState<number>(0);
  const [rows, setRows] = useState<HolidayDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingId, setEditingId] = useState(0);
  const [saving, setSaving] = useState(false);

  const form = useForm<CreateHolidayRequest>({ defaultValues: defaultHolidayForm(0) });

  useEffect(() => {
    void (async () => {
      try {
        const [y, types] = await Promise.all([listAcademicYears(), listHolidayTypes(true)]);
        setYears(y.data);
        setCatalogTypes(types.data);
        const current = y.data.find((yr) => yr.isCurrent) ?? y.data[0];
        if (current) setYearId(current.id);
      } catch (e) {
        setError(errMsg(e));
      }
    })();
  }, []);

  const load = useCallback(async () => {
    if (!yearId) {
      setRows([]);
      setLoading(false);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const res = await listHolidays(yearId);
      setRows(res.data);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  }, [yearId]);

  useEffect(() => {
    void load();
  }, [load]);

  const catalogLabel = (id: number | null) =>
    id ? (catalogTypes.find((t) => t.id === id)?.name ?? String(id)) : "—";

  const openAdd = () => {
    setEditingId(0);
    form.reset(defaultHolidayForm(yearId));
    setDialogOpen(true);
  };

  const openEdit = (r: HolidayDto) => {
    setEditingId(r.id);
    form.reset({
      academicYearId: r.academicYearId,
      name: r.name,
      date: r.date,
      holidayType: r.holidayType,
      description: r.description ?? "",
      holidayTypeCatalogId: r.holidayTypeCatalogId,
      isWorkingDayOverride: r.isWorkingDayOverride,
      requiresRescheduling: r.requiresRescheduling,
      colour: r.colour,
      priority: r.priority,
    });
    setDialogOpen(true);
  };

  const applyCatalogDefaults = (catalogId: number | null) => {
    if (!catalogId) return;
    const cat = catalogTypes.find((t) => t.id === catalogId);
    if (!cat) return;
    form.setValue("colour", cat.colour);
    form.setValue("priority", cat.priority);
  };

  const save = form.handleSubmit(async (values) => {
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      const payload = {
        ...values,
        description: values.description || null,
        colour: values.colour || null,
      };
      if (editingId) {
        await updateHoliday(editingId, { ...payload, id: editingId });
        setMessage("Holiday updated.");
      } else {
        await createHoliday(payload);
        setMessage("Holiday created.");
      }
      setDialogOpen(false);
      await load();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  });

  const handleDelete = async (id: number) => {
    if (!window.confirm("Delete this holiday?")) return;
    try {
      await deleteHoliday(id);
      setMessage("Holiday deleted.");
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
          Holidays
        </Typography>
        {canManage && yearId > 0 && (
          <Button variant="contained" onClick={openAdd}>
            Add holiday
          </Button>
        )}
      </Box>

      <FormControl size="small" sx={{ minWidth: 240 }}>
        <InputLabel id="year-label">Academic year</InputLabel>
        <Select labelId="year-label" label="Academic year" value={yearId || ""} onChange={(e) => setYearId(Number(e.target.value))}>
          {years.map((y) => (
            <MenuItem key={y.id} value={y.id}>
              {y.code} — {y.name}
            </MenuItem>
          ))}
        </Select>
      </FormControl>

      {message && <Alert severity="success">{message}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", p: 3 }}>
          <CircularProgress />
        </Box>
      ) : (
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Date</TableCell>
              <TableCell>Name</TableCell>
              <TableCell>Type</TableCell>
              <TableCell>Catalog type</TableCell>
              <TableCell>Priority</TableCell>
              <TableCell>Description</TableCell>
              {canManage && <TableCell align="right">Actions</TableCell>}
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((r) => (
              <TableRow key={r.id} hover>
                <TableCell>{r.date}</TableCell>
                <TableCell>{r.name}</TableCell>
                <TableCell>{HOLIDAY_TYPE_LABELS[r.holidayType] ?? r.holidayType}</TableCell>
                <TableCell>{catalogLabel(r.holidayTypeCatalogId)}</TableCell>
                <TableCell>{r.priority ?? "—"}</TableCell>
                <TableCell>{r.description ?? "—"}</TableCell>
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

      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>{editingId ? "Edit holiday" : "Add holiday"}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }} component="form" id="holiday-form" onSubmit={save}>
            <Controller
              name="name"
              control={form.control}
              rules={{ required: true }}
              render={({ field }) => <TextField {...field} label="Name" fullWidth required />}
            />
            <Controller
              name="date"
              control={form.control}
              rules={{ required: true }}
              render={({ field }) => (
                <TextField {...field} label="Date" type="date" fullWidth required slotProps={{ inputLabel: { shrink: true } }} />
              )}
            />
            <Controller
              name="holidayType"
              control={form.control}
              render={({ field }) => (
                <FormControl fullWidth>
                  <InputLabel id="type-label">Holiday type</InputLabel>
                  <Select labelId="type-label" label="Holiday type" value={field.value} onChange={(e) => field.onChange(Number(e.target.value))}>
                    {Object.entries(HOLIDAY_TYPE_LABELS).map(([k, v]) => (
                      <MenuItem key={k} value={Number(k)}>
                        {v}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            />
            <Controller
              name="holidayTypeCatalogId"
              control={form.control}
              render={({ field }) => (
                <FormControl fullWidth>
                  <InputLabel id="catalog-label">Holiday type catalog</InputLabel>
                  <Select
                    labelId="catalog-label"
                    label="Holiday type catalog"
                    value={field.value === null ? "" : field.value}
                    onChange={(e) => {
                      const v = parseOptionalSelectNumber(e.target.value);
                      const next = v === "" ? null : v;
                      field.onChange(next);
                      applyCatalogDefaults(next);
                    }}
                  >
                    <MenuItem value="">None</MenuItem>
                    {catalogTypes.map((t) => (
                      <MenuItem key={t.id} value={t.id}>
                        {t.code} — {t.name}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            />
            <Controller
              name="colour"
              control={form.control}
              render={({ field }) => (
                <TextField
                  label="Colour"
                  type="color"
                  fullWidth
                  value={field.value ?? "#1976d2"}
                  onChange={(e) => field.onChange(e.target.value)}
                  slotProps={{ inputLabel: { shrink: true } }}
                />
              )}
            />
            <Controller
              name="priority"
              control={form.control}
              render={({ field }) => (
                <TextField
                  label="Priority"
                  type="number"
                  fullWidth
                  value={field.value ?? ""}
                  onChange={(e) => field.onChange(e.target.value ? Number(e.target.value) : null)}
                />
              )}
            />
            <Controller
              name="isWorkingDayOverride"
              control={form.control}
              render={({ field }) => (
                <FormControlLabel control={<Checkbox checked={field.value} onChange={(_, v) => field.onChange(v)} />} label="Working day override" />
              )}
            />
            <Controller
              name="requiresRescheduling"
              control={form.control}
              render={({ field }) => (
                <FormControlLabel control={<Checkbox checked={field.value} onChange={(_, v) => field.onChange(v)} />} label="Requires rescheduling" />
              )}
            />
            <Controller
              name="description"
              control={form.control}
              render={({ field }) => <TextField {...field} label="Description" fullWidth multiline minRows={2} />}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)} disabled={saving}>
            Cancel
          </Button>
          <Button type="submit" form="holiday-form" variant="contained" disabled={saving}>
            Save
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default HolidaysPage;
