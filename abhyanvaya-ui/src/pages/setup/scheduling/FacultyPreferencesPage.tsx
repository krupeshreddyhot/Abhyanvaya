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
  FormGroup,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Tabs,
  TextField,
  Typography,
} from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import { Controller, useForm } from "react-hook-form";
import { PermissionKeys } from "../../../auth/permissionKeys";
import { useAuth } from "../../../context/AuthContext";
import {
  listCourses,
  listDepartments,
  listGroups,
  listSemesters,
  listStaff,
  listSubjectCatalog,
} from "../../../services/setupService";
import {
  PreferredTeachingMode,
  createFacultyTeachingPreference,
  deleteFacultyTeachingPreference,
  listAcademicYears,
  listBuildings,
  listCampuses,
  listFacultyTeachingPreferences,
  listFloors,
  searchRooms,
  updateFacultyTeachingPreference,
  type CreateFacultyTeachingPreferenceRequest,
  type FacultyTeachingPreferenceDto,
} from "../../../services/schedulingService";
import {
  DAY_LABELS,
  WEEKDAY_ORDER,
  errMsg,
  isDayFlagSet,
  parseOptionalSelectNumber,
  toggleDayFlag,
} from "./schedulingFormUtils";
import { PREFERRED_TEACHING_MODE_LABELS } from "./schedulingEnumLabels";

type PreferenceForm = CreateFacultyTeachingPreferenceRequest;

const emptyForm = (): PreferenceForm => ({
  staffId: 0,
  academicYearId: 0,
  preferredCampusId: null,
  preferredBuildingId: null,
  preferredFloorId: null,
  preferredRoomId: null,
  preferredSubjectId: null,
  preferredDepartmentId: null,
  preferredCourseId: null,
  preferredGroupId: null,
  preferredSemesterId: null,
  preferredFirstPeriod: null,
  preferredLastPeriod: null,
  preferredWorkingDaysFlags: 0,
  maximumContinuousClasses: 3,
  minimumBreakBetweenClasses: 1,
  preferredTeachingMode: PreferredTeachingMode.Any,
  priority: 0,
  remarks: "",
  isActive: true,
});

const FacultyPreferencesPage = () => {
  const { hasPermission } = useAuth();
  const canManage = hasPermission(PermissionKeys.SchedulingFacultyPreferencesManage);

  const [rows, setRows] = useState<FacultyTeachingPreferenceDto[]>([]);
  const [years, setYears] = useState<{ id: number; label: string }[]>([]);
  const [staff, setStaff] = useState<{ id: number; label: string }[]>([]);
  const [campuses, setCampuses] = useState<{ id: number; label: string }[]>([]);
  const [buildings, setBuildings] = useState<{ id: number; label: string }[]>([]);
  const [floors, setFloors] = useState<{ id: number; label: string }[]>([]);
  const [rooms, setRooms] = useState<{ id: number; label: string }[]>([]);
  const [subjects, setSubjects] = useState<{ id: number; label: string }[]>([]);
  const [departments, setDepartments] = useState<{ id: number; label: string }[]>([]);
  const [courses, setCourses] = useState<{ id: number; label: string }[]>([]);
  const [groups, setGroups] = useState<{ id: number; label: string }[]>([]);
  const [semesters, setSemesters] = useState<{ id: number; label: string }[]>([]);
  const [filterYearId, setFilterYearId] = useState<number | "">("");
  const [filterStaffId, setFilterStaffId] = useState<number | "">("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingId, setEditingId] = useState(0);
  const [tab, setTab] = useState(0);
  const [saving, setSaving] = useState(false);

  const form = useForm<PreferenceForm>({ defaultValues: emptyForm() });

  const watchCampusId = form.watch("preferredCampusId");
  const watchBuildingId = form.watch("preferredBuildingId");
  const watchFloorId = form.watch("preferredFloorId");

  useEffect(() => {
    void (async () => {
      try {
        const [y, st, c, d, co, g, se, sub] = await Promise.all([
          listAcademicYears(),
          listStaff({ page: 1, pageSize: 500 }),
          listCampuses(),
          listDepartments(undefined, true),
          listCourses(),
          listGroups(),
          listSemesters(),
          listSubjectCatalog(),
        ]);
        setYears(y.data.map((a) => ({ id: a.id, label: `${a.code} — ${a.name}` })));
        setStaff(st.data.items.map((s) => ({ id: s.id, label: `${s.firstName} ${s.lastName}` })));
        setCampuses(c.data.map((x) => ({ id: x.id, label: x.name })));
        setDepartments(d.data.map((x) => ({ id: x.id, label: x.name })));
        setCourses(co.data.map((x) => ({ id: x.id, label: x.name })));
        setGroups(g.data.map((x) => ({ id: x.id, label: x.name })));
        setSemesters(se.data.map((x) => ({ id: x.id, label: x.name })));
        setSubjects(
          sub.data.map((s) => ({
            id: s.tenantSubjectId,
            label: `${s.code ?? ""} ${s.name}`.trim(),
          })),
        );
        const current = y.data.find((a) => a.isCurrent) ?? y.data[0];
        if (current) setFilterYearId(current.id);
      } catch (e) {
        setError(errMsg(e));
      }
    })();
  }, []);

  useEffect(() => {
    void (async () => {
      if (!watchCampusId) {
        setBuildings([]);
        return;
      }
      const res = await listBuildings(watchCampusId);
      setBuildings(res.data.map((x) => ({ id: x.id, label: x.name })));
    })();
  }, [watchCampusId]);

  useEffect(() => {
    void (async () => {
      if (!watchBuildingId) {
        setFloors([]);
        return;
      }
      const res = await listFloors(watchBuildingId);
      setFloors(res.data.map((x) => ({ id: x.id, label: x.name })));
    })();
  }, [watchBuildingId]);

  useEffect(() => {
    void (async () => {
      if (!watchFloorId) {
        setRooms([]);
        return;
      }
      const res = await searchRooms({ floorId: watchFloorId, pageSize: 200 });
      setRooms(res.data.items.map((x) => ({ id: x.id, label: `${x.code} — ${x.name}` })));
    })();
  }, [watchFloorId]);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await listFacultyTeachingPreferences({
        academicYearId: filterYearId === "" ? undefined : filterYearId,
        staffId: filterStaffId === "" ? undefined : filterStaffId,
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

  const staffLabel = (id: number) => staff.find((s) => s.id === id)?.label ?? String(id);

  const openAdd = () => {
    setEditingId(0);
    setTab(0);
    form.reset({
      ...emptyForm(),
      academicYearId: filterYearId === "" ? 0 : filterYearId,
      staffId: staff[0]?.id ?? 0,
    });
    setDialogOpen(true);
  };

  const openEdit = (r: FacultyTeachingPreferenceDto) => {
    setEditingId(r.id);
    setTab(0);
    form.reset({
      staffId: r.staffId,
      academicYearId: r.academicYearId,
      preferredCampusId: r.preferredCampusId,
      preferredBuildingId: r.preferredBuildingId,
      preferredFloorId: r.preferredFloorId,
      preferredRoomId: r.preferredRoomId,
      preferredSubjectId: r.preferredSubjectId,
      preferredDepartmentId: r.preferredDepartmentId,
      preferredCourseId: r.preferredCourseId,
      preferredGroupId: r.preferredGroupId,
      preferredSemesterId: r.preferredSemesterId,
      preferredFirstPeriod: r.preferredFirstPeriod,
      preferredLastPeriod: r.preferredLastPeriod,
      preferredWorkingDaysFlags: r.preferredWorkingDaysFlags,
      maximumContinuousClasses: r.maximumContinuousClasses,
      minimumBreakBetweenClasses: r.minimumBreakBetweenClasses,
      preferredTeachingMode: r.preferredTeachingMode,
      priority: r.priority,
      remarks: r.remarks ?? "",
      isActive: r.isActive,
    });
    setDialogOpen(true);
  };

  const save = form.handleSubmit(async (values) => {
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      const payload = { ...values, remarks: values.remarks || null };
      if (editingId) {
        await updateFacultyTeachingPreference(editingId, { ...payload, id: editingId });
        setMessage("Preference updated.");
      } else {
        await createFacultyTeachingPreference(payload);
        setMessage("Preference created.");
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
    if (!window.confirm("Delete this faculty teaching preference?")) return;
    try {
      await deleteFacultyTeachingPreference(id);
      setMessage("Preference deleted.");
      await load();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const optionalSelect = (
    name:
      | "preferredCampusId"
      | "preferredBuildingId"
      | "preferredFloorId"
      | "preferredRoomId"
      | "preferredSubjectId"
      | "preferredDepartmentId"
      | "preferredCourseId"
      | "preferredGroupId"
      | "preferredSemesterId",
    label: string,
    options: { id: number; label: string }[],
  ) => (
    <Controller
      key={name}
      name={name}
      control={form.control}
      render={({ field }) => (
        <FormControl fullWidth>
          <InputLabel id={`${name}-label`}>{label}</InputLabel>
          <Select
            labelId={`${name}-label`}
            label={label}
            value={field.value === null || field.value === undefined ? "" : field.value}
            onChange={(e) => {
              const v = parseOptionalSelectNumber(e.target.value);
              field.onChange(v === "" ? null : v);
            }}
          >
            <MenuItem value="">None</MenuItem>
            {options.map((o) => (
              <MenuItem key={o.id} value={o.id}>
                {o.label}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
      )}
    />
  );

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />} variant="text">
          Scheduling
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Faculty preferences
        </Typography>
        {canManage && (
          <Button variant="contained" onClick={openAdd}>
            Add preference
          </Button>
        )}
      </Box>

      <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
        <FormControl size="small" sx={{ minWidth: 220 }}>
          <InputLabel id="fy">Academic year</InputLabel>
          <Select
            labelId="fy"
            label="Academic year"
            value={filterYearId}
            onChange={(e) => setFilterYearId(parseOptionalSelectNumber(e.target.value))}
          >
            <MenuItem value="">All</MenuItem>
            {years.map((y) => (
              <MenuItem key={y.id} value={y.id}>
                {y.label}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
        <FormControl size="small" sx={{ minWidth: 220 }}>
          <InputLabel id="fs">Faculty</InputLabel>
          <Select
            labelId="fs"
            label="Faculty"
            value={filterStaffId}
            onChange={(e) => setFilterStaffId(parseOptionalSelectNumber(e.target.value))}
          >
            <MenuItem value="">All</MenuItem>
            {staff.map((s) => (
              <MenuItem key={s.id} value={s.id}>
                {s.label}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
      </Stack>

      {message && <Alert severity="success">{message}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}

      {loading ? (
        <CircularProgress />
      ) : (
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Faculty</TableCell>
              <TableCell>Year</TableCell>
              <TableCell>Mode</TableCell>
              <TableCell>Priority</TableCell>
              <TableCell>Active</TableCell>
              {canManage && <TableCell align="right">Actions</TableCell>}
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((r) => (
              <TableRow key={r.id} hover>
                <TableCell>{staffLabel(r.staffId)}</TableCell>
                <TableCell>{years.find((y) => y.id === r.academicYearId)?.label ?? r.academicYearId}</TableCell>
                <TableCell>{PREFERRED_TEACHING_MODE_LABELS[r.preferredTeachingMode] ?? r.preferredTeachingMode}</TableCell>
                <TableCell>{r.priority}</TableCell>
                <TableCell>{r.isActive ? "Yes" : "No"}</TableCell>
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
        <DialogTitle>{editingId ? "Edit faculty preference" : "Add faculty preference"}</DialogTitle>
        <DialogContent>
          <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2 }}>
            <Tab label="General" />
            <Tab label="Location" />
            <Tab label="Subjects" />
            <Tab label="Time" />
            <Tab label="Advanced" />
          </Tabs>
          <Stack spacing={2} component="form" id="pref-form" onSubmit={save}>
            {tab === 0 && (
              <>
                <Controller
                  name="staffId"
                  control={form.control}
                  render={({ field }) => (
                    <FormControl fullWidth required>
                      <InputLabel id="staff">Faculty</InputLabel>
                      <Select labelId="staff" label="Faculty" value={field.value} onChange={(e) => field.onChange(Number(e.target.value))}>
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
                  name="academicYearId"
                  control={form.control}
                  render={({ field }) => (
                    <FormControl fullWidth required>
                      <InputLabel id="year">Academic year</InputLabel>
                      <Select labelId="year" label="Academic year" value={field.value} onChange={(e) => field.onChange(Number(e.target.value))}>
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
                  name="preferredTeachingMode"
                  control={form.control}
                  render={({ field }) => (
                    <FormControl fullWidth>
                      <InputLabel id="mode">Preferred teaching mode</InputLabel>
                      <Select labelId="mode" label="Preferred teaching mode" value={field.value} onChange={(e) => field.onChange(Number(e.target.value))}>
                        {Object.entries(PREFERRED_TEACHING_MODE_LABELS).map(([k, v]) => (
                          <MenuItem key={k} value={Number(k)}>
                            {v}
                          </MenuItem>
                        ))}
                      </Select>
                    </FormControl>
                  )}
                />
                <Controller name="priority" control={form.control} render={({ field }) => <TextField {...field} label="Priority" type="number" fullWidth onChange={(e) => field.onChange(Number(e.target.value))} />} />
                <Controller name="remarks" control={form.control} render={({ field }) => <TextField {...field} label="Remarks" fullWidth multiline minRows={2} />} />
                <Controller name="isActive" control={form.control} render={({ field }) => <FormControlLabel control={<Checkbox checked={field.value} onChange={(_, v) => field.onChange(v)} />} label="Active" />} />
              </>
            )}
            {tab === 1 && (
              <>
                {optionalSelect("preferredCampusId", "Preferred campus", campuses)}
                {optionalSelect("preferredBuildingId", "Preferred building", buildings)}
                {optionalSelect("preferredFloorId", "Preferred floor", floors)}
                {optionalSelect("preferredRoomId", "Preferred room", rooms)}
              </>
            )}
            {tab === 2 && (
              <>
                {optionalSelect("preferredSubjectId", "Preferred subject", subjects)}
                {optionalSelect("preferredDepartmentId", "Preferred department", departments)}
                {optionalSelect("preferredCourseId", "Preferred course", courses)}
                {optionalSelect("preferredGroupId", "Preferred group", groups)}
                {optionalSelect("preferredSemesterId", "Preferred semester", semesters)}
              </>
            )}
            {tab === 3 && (
              <>
                <Controller
                  name="preferredFirstPeriod"
                  control={form.control}
                  render={({ field }) => (
                    <TextField
                      label="Preferred first period"
                      type="number"
                      fullWidth
                      value={field.value ?? ""}
                      onChange={(e) => field.onChange(e.target.value ? Number(e.target.value) : null)}
                    />
                  )}
                />
                <Controller
                  name="preferredLastPeriod"
                  control={form.control}
                  render={({ field }) => (
                    <TextField
                      label="Preferred last period"
                      type="number"
                      fullWidth
                      value={field.value ?? ""}
                      onChange={(e) => field.onChange(e.target.value ? Number(e.target.value) : null)}
                    />
                  )}
                />
                <Controller
                  name="preferredWorkingDaysFlags"
                  control={form.control}
                  render={({ field }) => (
                    <FormGroup row>
                      {WEEKDAY_ORDER.map((dow) => (
                        <FormControlLabel
                          key={dow}
                          control={
                            <Checkbox
                              checked={isDayFlagSet(field.value, dow)}
                              onChange={(_, checked) => field.onChange(toggleDayFlag(field.value, dow, checked))}
                            />
                          }
                          label={DAY_LABELS[dow]}
                        />
                      ))}
                    </FormGroup>
                  )}
                />
              </>
            )}
            {tab === 4 && (
              <>
                <Controller name="maximumContinuousClasses" control={form.control} render={({ field }) => <TextField {...field} label="Maximum continuous classes" type="number" fullWidth required onChange={(e) => field.onChange(Number(e.target.value))} />} />
                <Controller name="minimumBreakBetweenClasses" control={form.control} render={({ field }) => <TextField {...field} label="Minimum break between classes (minutes)" type="number" fullWidth onChange={(e) => field.onChange(Number(e.target.value))} />} />
              </>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)} disabled={saving}>
            Cancel
          </Button>
          <Button type="submit" form="pref-form" variant="contained" disabled={saving}>
            Save
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default FacultyPreferencesPage;
