import { useCallback, useEffect, useMemo, useState } from "react";
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
  listDepartments,
  listMasterCourses,
  listMasterGroups,
  listSemesters,
  listStaff,
  listSubjectCatalog,
} from "../../../services/setupService";
import {
  createSubjectAllocation,
  deleteSubjectAllocation,
  listAcademicYears,
  listSubjectAllocations,
  updateSubjectAllocation,
  type CreateSubjectAllocationRequest,
  type SubjectAllocationDto,
} from "../../../services/schedulingService";
import { errMsg, parseOptionalSelectNumber } from "./schedulingFormUtils";

const SubjectAllocationPage = () => {
  const { hasPermission } = useAuth();
  const canManage = hasPermission(PermissionKeys.SchedulingManage);

  const [rows, setRows] = useState<SubjectAllocationDto[]>([]);
  const [years, setYears] = useState<{ id: number; label: string }[]>([]);
  const [courses, setCourses] = useState<{ id: number; name: string }[]>([]);
  const [groups, setGroups] = useState<{ id: number; name: string; courseId: number }[]>([]);
  const [semesters, setSemesters] = useState<{ id: number; name: string; courseId: number; groupId: number | null }[]>([]);
  const [subjects, setSubjects] = useState<{ id: number; name: string }[]>([]);
  const [staff, setStaff] = useState<{ id: number; label: string }[]>([]);

  const [departments, setDepartments] = useState<{ id: number; name: string }[]>([]);

  const [filterYearId, setFilterYearId] = useState<number | "">("");
  const [filterDepartmentId, setFilterDepartmentId] = useState<number | "">("");
  const [filterCourseId, setFilterCourseId] = useState<number | "">("");
  const [filterGroupId, setFilterGroupId] = useState<number | "">("");
  const [filterSemesterId, setFilterSemesterId] = useState<number | "">("");
  const [filterStaffId, setFilterStaffId] = useState<number | "">("");

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingId, setEditingId] = useState(0);
  const [saving, setSaving] = useState(false);

  const form = useForm<CreateSubjectAllocationRequest>({
    defaultValues: {
      academicYearId: 0,
      subjectId: 0,
      staffId: 0,
      courseId: 0,
      groupId: 0,
      semesterId: 0,
      departmentId: 0,
      weeklyHours: 3,
      preferredRoomId: null,
      labRequired: false,
      aiAttendanceEnabled: false,
      attendanceMandatory: true,
      effectiveFrom: "",
      effectiveTo: null,
      notes: "",
    },
  });

  useEffect(() => {
    void (async () => {
      try {
        const [y, c, g, sem, sub, st, dept] = await Promise.all([
          listAcademicYears(),
          listMasterCourses(),
          listMasterGroups(),
          listSemesters(),
          listSubjectCatalog(),
          listStaff({ page: 1, pageSize: 500 }),
          listDepartments(undefined, true),
        ]);
        setYears(y.data.map((a) => ({ id: a.id, label: `${a.code} — ${a.name}` })));
        setCourses(c.data.map((x) => ({ id: x.id, name: x.name })));
        setGroups(g.data.map((x) => ({ id: x.id, name: x.name, courseId: x.courseId })));
        setSemesters(sem.data.map((x) => ({ id: x.id, name: x.name, courseId: x.courseId, groupId: x.groupId })));
        setSubjects(sub.data.map((x) => ({ id: x.tenantSubjectId, name: x.name })));
        setStaff(st.data.items.map((s) => ({ id: s.id, label: `${s.firstName} ${s.lastName}` })));
        setDepartments(dept.data.map((d) => ({ id: d.id, name: d.name })));
        const current = y.data.find((a) => a.isCurrent) ?? y.data[0];
        if (current) setFilterYearId(current.id);
      } catch (e) {
        setError(errMsg(e));
      }
    })();
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await listSubjectAllocations({
        academicYearId: filterYearId || undefined,
        staffId: filterStaffId || undefined,
        departmentId: filterDepartmentId || undefined,
      });
      setRows(res.data);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  }, [filterYearId, filterStaffId, filterDepartmentId]);

  useEffect(() => {
    void load();
  }, [load]);

  const filteredRows = useMemo(
    () =>
      rows.filter((r) => {
        if (filterDepartmentId && r.departmentId !== filterDepartmentId) return false;
        if (filterCourseId && r.courseId !== filterCourseId) return false;
        if (filterGroupId && r.groupId !== filterGroupId) return false;
        if (filterSemesterId && r.semesterId !== filterSemesterId) return false;
        return true;
      }),
    [rows, filterDepartmentId, filterCourseId, filterGroupId, filterSemesterId],
  );

  const openAdd = () => {
    setEditingId(0);
    form.reset({
      academicYearId: typeof filterYearId === "number" ? filterYearId : years[0]?.id ?? 0,
      subjectId: subjects[0]?.id ?? 0,
      staffId: staff[0]?.id ?? 0,
      courseId: courses[0]?.id ?? 0,
      groupId: groups[0]?.id ?? 0,
      semesterId: semesters[0]?.id ?? 0,
      departmentId: typeof filterDepartmentId === "number" ? filterDepartmentId : departments[0]?.id ?? 0,
      weeklyHours: 3,
      preferredRoomId: null,
      labRequired: false,
      aiAttendanceEnabled: false,
      attendanceMandatory: true,
      effectiveFrom: new Date().toISOString().slice(0, 10),
      effectiveTo: null,
      notes: "",
    });
    setDialogOpen(true);
  };

  const openEdit = (r: SubjectAllocationDto) => {
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
      if (editingId) await updateSubjectAllocation(editingId, { ...payload, id: editingId });
      else await createSubjectAllocation(payload);
      setMessage(editingId ? "Allocation updated." : "Allocation created.");
      setDialogOpen(false);
      await load();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  });

  const handleDelete = async (id: number) => {
    if (!window.confirm("Delete this allocation?")) return;
    try {
      await deleteSubjectAllocation(id);
      setMessage("Allocation deleted.");
      await load();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const labelFor = (id: number, list: { id: number; name?: string; label?: string }[]) =>
    list.find((x) => x.id === id)?.name ?? list.find((x) => x.id === id)?.label ?? id;

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />} variant="text">
          Scheduling
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Subject allocations
        </Typography>
        {canManage && (
          <Button variant="contained" onClick={openAdd}>
            Add allocation
          </Button>
        )}
      </Box>

      <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap" }} useFlexGap>
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
          <InputLabel id="fd">Department</InputLabel>
          <Select
            labelId="fd"
            label="Department"
            value={filterDepartmentId === "" ? "" : filterDepartmentId}
            onChange={(e) => {
              setFilterDepartmentId(parseOptionalSelectNumber(e.target.value));
              setFilterCourseId("");
              setFilterGroupId("");
              setFilterSemesterId("");
            }}
          >
            <MenuItem value="">All</MenuItem>
            {departments.map((d) => (
              <MenuItem key={d.id} value={d.id}>
                {d.name}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
        <FormControl size="small" sx={{ minWidth: 140 }}>
          <InputLabel id="fc">Course</InputLabel>
          <Select labelId="fc" label="Course" value={filterCourseId === "" ? "" : filterCourseId} onChange={(e) => setFilterCourseId(parseOptionalSelectNumber(e.target.value))} disabled={!filterDepartmentId}>
            <MenuItem value="">All</MenuItem>
            {courses.map((c) => (
              <MenuItem key={c.id} value={c.id}>
                {c.name}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
        <FormControl size="small" sx={{ minWidth: 140 }}>
          <InputLabel id="fg">Group</InputLabel>
          <Select labelId="fg" label="Group" value={filterGroupId === "" ? "" : filterGroupId} onChange={(e) => setFilterGroupId(parseOptionalSelectNumber(e.target.value))} disabled={!filterCourseId}>
            <MenuItem value="">All</MenuItem>
            {groups.filter((g) => !filterCourseId || g.courseId === filterCourseId).map((g) => (
              <MenuItem key={g.id} value={g.id}>
                {g.name}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
        <FormControl size="small" sx={{ minWidth: 140 }}>
          <InputLabel id="fs">Semester</InputLabel>
          <Select labelId="fs" label="Semester" value={filterSemesterId === "" ? "" : filterSemesterId} onChange={(e) => setFilterSemesterId(parseOptionalSelectNumber(e.target.value))} disabled={!filterGroupId}>
            <MenuItem value="">All</MenuItem>
            {semesters
              .filter((s) => (!filterCourseId || s.courseId === filterCourseId) && (!filterGroupId || s.groupId === filterGroupId))
              .map((s) => (
                <MenuItem key={s.id} value={s.id}>
                  {s.name}
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
      </Stack>

      {message && <Alert severity="success">{message}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}

      {loading ? (
        <CircularProgress />
      ) : (
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Department</TableCell>
              <TableCell>Subject</TableCell>
              <TableCell>Staff</TableCell>
              <TableCell>Course</TableCell>
              <TableCell>Group</TableCell>
              <TableCell>Semester</TableCell>
              <TableCell>Weekly hrs</TableCell>
              {canManage && <TableCell align="right">Actions</TableCell>}
            </TableRow>
          </TableHead>
          <TableBody>
            {filteredRows.map((r) => (
              <TableRow key={r.id} hover>
                <TableCell>{labelFor(r.departmentId, departments)}</TableCell>
                <TableCell>{labelFor(r.subjectId, subjects)}</TableCell>
                <TableCell>{labelFor(r.staffId, staff)}</TableCell>
                <TableCell>{labelFor(r.courseId, courses)}</TableCell>
                <TableCell>{labelFor(r.groupId, groups)}</TableCell>
                <TableCell>{labelFor(r.semesterId, semesters)}</TableCell>
                <TableCell>{r.weeklyHours}</TableCell>
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
        <DialogTitle>{editingId ? "Edit allocation" : "Add allocation"}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }} component="form" id="alloc-form" onSubmit={save}>
            <Controller
              name="academicYearId"
              control={form.control}
              render={({ field }) => (
                <FormControl fullWidth>
                  <InputLabel id="ay">Academic year</InputLabel>
                  <Select labelId="ay" label="Academic year" value={field.value} onChange={(e) => field.onChange(Number(e.target.value))}>
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
              name="departmentId"
              control={form.control}
              rules={{ required: true, min: 1 }}
              render={({ field }) => (
                <FormControl fullWidth required>
                  <InputLabel id="dept">Department</InputLabel>
                  <Select labelId="dept" label="Department" value={field.value} onChange={(e) => field.onChange(Number(e.target.value))}>
                    {departments.map((d) => (
                      <MenuItem key={d.id} value={d.id}>
                        {d.name}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            />
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <Controller
                name="subjectId"
                control={form.control}
                render={({ field }) => (
                  <FormControl fullWidth>
                    <InputLabel id="sub">Subject</InputLabel>
                    <Select labelId="sub" label="Subject" value={field.value} onChange={(e) => field.onChange(Number(e.target.value))}>
                      {subjects.map((s) => (
                        <MenuItem key={s.id} value={s.id}>
                          {s.name}
                        </MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                )}
              />
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
            </Stack>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <Controller
                name="courseId"
                control={form.control}
                render={({ field }) => (
                  <FormControl fullWidth>
                    <InputLabel id="course">Course</InputLabel>
                    <Select labelId="course" label="Course" value={field.value} onChange={(e) => field.onChange(Number(e.target.value))}>
                      {courses.map((c) => (
                        <MenuItem key={c.id} value={c.id}>
                          {c.name}
                        </MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                )}
              />
              <Controller
                name="groupId"
                control={form.control}
                render={({ field }) => (
                  <FormControl fullWidth>
                    <InputLabel id="group">Group</InputLabel>
                    <Select labelId="group" label="Group" value={field.value} onChange={(e) => field.onChange(Number(e.target.value))}>
                      {groups.map((g) => (
                        <MenuItem key={g.id} value={g.id}>
                          {g.name}
                        </MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                )}
              />
              <Controller
                name="semesterId"
                control={form.control}
                render={({ field }) => (
                  <FormControl fullWidth>
                    <InputLabel id="sem">Semester</InputLabel>
                    <Select labelId="sem" label="Semester" value={field.value} onChange={(e) => field.onChange(Number(e.target.value))}>
                      {semesters.map((s) => (
                        <MenuItem key={s.id} value={s.id}>
                          {s.name}
                        </MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                )}
              />
            </Stack>
            <Controller
              name="weeklyHours"
              control={form.control}
              render={({ field }) => <TextField {...field} label="Weekly hours" type="number" fullWidth onChange={(e) => field.onChange(Number(e.target.value))} />}
            />
            <Stack direction="row" spacing={2} sx={{ flexWrap: "wrap" }}>
              <Controller name="labRequired" control={form.control} render={({ field }) => <FormControlLabel control={<Checkbox checked={field.value} onChange={(_, v) => field.onChange(v)} />} label="Lab required" />} />
              <Controller name="aiAttendanceEnabled" control={form.control} render={({ field }) => <FormControlLabel control={<Checkbox checked={field.value} onChange={(_, v) => field.onChange(v)} />} label="AI attendance" />} />
              <Controller name="attendanceMandatory" control={form.control} render={({ field }) => <FormControlLabel control={<Checkbox checked={field.value} onChange={(_, v) => field.onChange(v)} />} label="Attendance mandatory" />} />
            </Stack>
            <Controller name="effectiveFrom" control={form.control} render={({ field }) => <TextField {...field} label="Effective from" type="date" fullWidth slotProps={{ inputLabel: { shrink: true } }} />} />
            <Controller
              name="effectiveTo"
              control={form.control}
              render={({ field }) => (
                <TextField
                  {...field}
                  value={field.value ?? ""}
                  label="Effective to"
                  type="date"
                  fullWidth
                  slotProps={{ inputLabel: { shrink: true } }}
                  onChange={(e) => field.onChange(e.target.value || null)}
                />
              )}
            />
            <Controller name="notes" control={form.control} render={({ field }) => <TextField {...field} label="Notes" fullWidth multiline minRows={2} />} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)} disabled={saving}>
            Cancel
          </Button>
          <Button type="submit" form="alloc-form" variant="contained" disabled={saving}>
            Save
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default SubjectAllocationPage;
