import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert,
  Autocomplete,
  Box,
  Button,
  Checkbox,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  FormControl,
  InputLabel,
  ListItemText,
  MenuItem,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TablePagination,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import AddIcon from "@mui/icons-material/Add";
import { PermissionKeys } from "../../auth/permissionKeys";
import { useAuth } from "../../context/AuthContext";
import {
  createStaff,
  deleteStaff,
  getStaff,
  getStaffSetupMetadata,
  listDepartments,
  listGendersAdmin,
  listStaff,
  listSubjectCatalog,
  updateStaff,
  type CreateStaffPayload,
  type DepartmentRow,
  type IdName,
  type LookupItem,
  type StaffListItem,
  type StaffSetupMetadata,
  type SubjectCatalogRow,
} from "../../services/setupService";

const errMsg = (e: unknown): string => {
  const d = (e as { response?: { data?: unknown } }).response?.data;
  if (typeof d === "string") return d;
  return "Request failed.";
};

const toDateInput = (iso: string | null | undefined): string => {
  if (!iso) return "";
  return iso.slice(0, 10);
};

const fromDateInput = (s: string): string | null => {
  const t = s.trim();
  return t === "" ? null : t;
};

const isTeachingType = (meta: StaffSetupMetadata | null, staffTypeId: number): boolean => {
  if (!meta || !staffTypeId) return false;
  const t = meta.staffTypes.find((x) => x.id === staffTypeId);
  return (t?.code ?? "").toUpperCase() === "TEACHING";
};

type DeptAssignRow = { departmentId: number; departmentRoleLookupIds: number[] };

const emptyForm = (): {
  collegeId: number;
  staffCode: string;
  staffTypeId: number;
  personTitleId: number | "";
  designationId: number;
  qualificationId: number | "";
  genderId: number | "";
  employmentStatusId: number | "";
  firstName: string;
  lastName: string;
  phone: string;
  altPhone: string;
  email: string;
  website: string;
  dateOfJoining: string;
  contractEndDate: string;
  dateOfBirth: string;
  deptAssignments: DeptAssignRow[];
  collegeRoleLookupIds: number[];
  subjectIds: number[];
} => ({
  collegeId: 0,
  staffCode: "",
  staffTypeId: 0,
  personTitleId: "",
  designationId: 0,
  qualificationId: "",
  genderId: "",
  employmentStatusId: "",
  firstName: "",
  lastName: "",
  phone: "",
  altPhone: "",
  email: "",
  website: "",
  dateOfJoining: "",
  contractEndDate: "",
  dateOfBirth: "",
  deptAssignments: [],
  collegeRoleLookupIds: [],
  subjectIds: [],
});

const StaffPage = () => {
  const { hasAnyPermission } = useAuth();
  const canManageLookups = hasAnyPermission([PermissionKeys.SetupLookupsManage]);

  const [meta, setMeta] = useState<StaffSetupMetadata | null>(null);
  const [genders, setGenders] = useState<IdName[]>([]);
  const [subjectCatalog, setSubjectCatalog] = useState<SubjectCatalogRow[]>([]);
  const [deptOptions, setDeptOptions] = useState<DepartmentRow[]>([]);

  const [items, setItems] = useState<StaffListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(25);
  const [search, setSearch] = useState("");
  const [listLoading, setListLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [editingId, setEditingId] = useState(0);
  const [form, setForm] = useState(() => emptyForm());

  const teaching = useMemo(() => isTeachingType(meta, form.staffTypeId), [meta, form.staffTypeId]);

  const loadMeta = useCallback(async () => {
    try {
      const [m, g] = await Promise.all([getStaffSetupMetadata(), listGendersAdmin()]);
      setMeta(m.data);
      setGenders(g.data);
    } catch (e) {
      setError(errMsg(e));
    }
  }, []);

  const loadDepartmentsForCollege = useCallback(async (collegeId: number) => {
    if (!collegeId) {
      setDeptOptions([]);
      return;
    }
    try {
      const res = await listDepartments(collegeId);
      setDeptOptions(res.data);
    } catch {
      setDeptOptions([]);
    }
  }, []);

  const loadCatalog = useCallback(async () => {
    try {
      const res = await listSubjectCatalog();
      setSubjectCatalog(res.data);
    } catch {
      setSubjectCatalog([]);
    }
  }, []);

  const loadList = useCallback(async () => {
    setListLoading(true);
    setError(null);
    try {
      const res = await listStaff({
        search: search.trim() || undefined,
        page: page + 1,
        pageSize,
      });
      setItems(res.data.items);
      setTotal(res.data.total);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setListLoading(false);
    }
  }, [search, page, pageSize]);

  useEffect(() => {
    void loadMeta();
  }, [loadMeta]);

  useEffect(() => {
    void loadList();
  }, [loadList]);

  useEffect(() => {
    if (dialogOpen && form.collegeId) void loadDepartmentsForCollege(form.collegeId);
  }, [dialogOpen, form.collegeId, loadDepartmentsForCollege]);

  useEffect(() => {
    if (dialogOpen && teaching) void loadCatalog();
  }, [dialogOpen, teaching, loadCatalog]);

  const openAdd = () => {
    setEditingId(0);
    const f = emptyForm();
    if (meta?.colleges.length === 1) f.collegeId = meta.colleges[0].id;
    if (meta?.staffTypes[0]) f.staffTypeId = meta.staffTypes[0].id;
    if (meta?.designations[0]) f.designationId = meta.designations[0].id;
    setForm(f);
    setDialogOpen(true);
  };

  const openEdit = async (row: StaffListItem) => {
    setError(null);
    try {
      const res = await getStaff(row.id);
      const s = res.data;
      setEditingId(s.id);
      setForm({
        collegeId: s.collegeId,
        staffCode: s.staffCode ?? "",
        staffTypeId: s.staffTypeId,
        personTitleId: s.personTitleId ?? "",
        designationId: s.designationId,
        qualificationId: s.qualificationId ?? "",
        genderId: s.genderId ?? "",
        employmentStatusId: s.employmentStatusId ?? "",
        firstName: s.firstName,
        lastName: s.lastName,
        phone: s.phone ?? "",
        altPhone: s.altPhone ?? "",
        email: s.email ?? "",
        website: s.website ?? "",
        dateOfJoining: toDateInput(s.dateOfJoining),
        contractEndDate: toDateInput(s.contractEndDate),
        dateOfBirth: toDateInput(s.dateOfBirth),
        deptAssignments: s.departments.map((d) => ({
          departmentId: d.departmentId,
          departmentRoleLookupIds: [...d.departmentRoleLookupIds],
        })),
        collegeRoleLookupIds: [...s.collegeRoleLookupIds],
        subjectIds: [...s.subjectIds],
      });
      setDialogOpen(true);
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const buildPayload = (): CreateStaffPayload | null => {
    if (!form.collegeId || !form.staffTypeId || !form.designationId) return null;
    if (!form.firstName.trim() || !form.lastName.trim()) return null;

    const deps = form.deptAssignments
      .filter((d) => d.departmentId > 0)
      .map((d) => ({
        departmentId: d.departmentId,
        departmentRoleLookupIds: d.departmentRoleLookupIds,
      }));

    const ids = new Set(deps.map((d) => d.departmentId));
    if (ids.size !== deps.length) {
      setError("Each department can only appear once in assignments.");
      return null;
    }

    return {
      collegeId: form.collegeId,
      staffCode: form.staffCode.trim() === "" ? null : form.staffCode.trim(),
      staffTypeId: form.staffTypeId,
      personTitleId: form.personTitleId === "" ? null : form.personTitleId,
      designationId: form.designationId,
      qualificationId: form.qualificationId === "" ? null : form.qualificationId,
      genderId: form.genderId === "" ? null : form.genderId,
      employmentStatusId: form.employmentStatusId === "" ? null : form.employmentStatusId,
      firstName: form.firstName.trim(),
      lastName: form.lastName.trim(),
      phone: form.phone.trim() === "" ? null : form.phone.trim(),
      altPhone: form.altPhone.trim() === "" ? null : form.altPhone.trim(),
      email: form.email.trim() === "" ? null : form.email.trim(),
      website: form.website.trim() === "" ? null : form.website.trim(),
      dateOfJoining: fromDateInput(form.dateOfJoining),
      contractEndDate: fromDateInput(form.contractEndDate),
      dateOfBirth: fromDateInput(form.dateOfBirth),
      departments: teaching ? deps : null,
      collegeRoleLookupIds: teaching ? form.collegeRoleLookupIds : null,
      subjectIds: teaching ? form.subjectIds : null,
    };
  };

  const save = async () => {
    setError(null);
    setMessage(null);
    const payload = buildPayload();
    if (!payload) {
      setError("College, staff type, designation, first name, and last name are required.");
      return;
    }
    if (!teaching && (form.deptAssignments.length > 0 || form.collegeRoleLookupIds.length || form.subjectIds.length)) {
      setError("Clear teaching assignments or switch to a teaching staff type.");
      return;
    }

    setSaving(true);
    try {
      if (editingId) await updateStaff(editingId, payload);
      else await createStaff(payload);
      setMessage(editingId ? "Staff updated." : "Staff created.");
      setDialogOpen(false);
      await loadList();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  };

  const remove = async (row: StaffListItem) => {
    if (!window.confirm(`Remove staff "${row.firstName} ${row.lastName}"?`)) return;
    setError(null);
    try {
      await deleteStaff(row.id);
      setMessage("Staff removed.");
      await loadList();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const updateDeptRow = (index: number, patch: Partial<DeptAssignRow>) => {
    setForm((f) => {
      const next = [...f.deptAssignments];
      next[index] = { ...next[index], ...patch };
      return { ...f, deptAssignments: next };
    });
  };

  const subjectOptions = useMemo(
    () =>
      subjectCatalog.map((s) => ({
        id: s.id,
        label: `${s.name} — ${s.courseName} / ${s.groupName} / ${s.semesterName}`,
      })),
    [subjectCatalog],
  );

  const selectedSubjects = useMemo(
    () => subjectOptions.filter((o) => form.subjectIds.includes(o.id)),
    [subjectOptions, form.subjectIds],
  );

  const renderLookupSelect = (
    label: string,
    value: number | "",
    options: LookupItem[],
    onChange: (id: number | "") => void,
    required?: boolean,
  ) => (
    <FormControl fullWidth required={required} size="small">
      <InputLabel id={`lbl-${label}`}>{label}</InputLabel>
      <Select
        labelId={`lbl-${label}`}
        label={label}
        value={value === "" || value === 0 ? "" : value}
        onChange={(e) => {
          const v = e.target.value as string | number;
          const s = String(v);
          onChange(s === "" ? "" : Number(s));
        }}
      >
        {!required && <MenuItem value="">—</MenuItem>}
        {options.map((o) => (
          <MenuItem key={o.id} value={o.id}>
            {o.name}
          </MenuItem>
        ))}
      </Select>
    </FormControl>
  );

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup" startIcon={<ArrowBackIcon />} variant="text">
          Catalog
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Staff
        </Typography>
        {canManageLookups && (
          <Button component={RouterLink} to="/setup/staff-lookups" variant="outlined" size="small">
            Lookup values
          </Button>
        )}
        <Button variant="contained" startIcon={<AddIcon />} onClick={openAdd} disabled={!meta?.colleges.length}>
          Add staff
        </Button>
      </Box>

      {message && <Alert severity="success">{message}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}

      <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ alignItems: { sm: "center" } }}>
        <TextField
          size="small"
          label="Search"
          placeholder="Name or staff code"
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            setPage(0);
          }}
          sx={{ minWidth: 220 }}
        />
      </Stack>

      {listLoading ? (
        <Box sx={{ display: "flex", justifyContent: "center", p: 3 }}>
          <CircularProgress />
        </Box>
      ) : (
        <>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Code</TableCell>
                <TableCell>Name</TableCell>
                <TableCell>Type</TableCell>
                <TableCell>Designation</TableCell>
                <TableCell>Email</TableCell>
                <TableCell align="right">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {items.map((r) => (
                <TableRow key={r.id} hover>
                  <TableCell>{r.staffCode ?? "—"}</TableCell>
                  <TableCell>
                    {r.firstName} {r.lastName}
                  </TableCell>
                  <TableCell>{r.staffTypeName}</TableCell>
                  <TableCell>{r.designationName}</TableCell>
                  <TableCell>{r.email ?? "—"}</TableCell>
                  <TableCell align="right">
                    <Button size="small" onClick={() => void openEdit(r)}>
                      Edit
                    </Button>{" "}
                    <Button size="small" color="error" onClick={() => void remove(r)}>
                      Delete
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <TablePagination
            component="div"
            count={total}
            page={page}
            onPageChange={(_, p) => setPage(p)}
            rowsPerPage={pageSize}
            onRowsPerPageChange={(e) => {
              setPageSize(Number(e.target.value));
              setPage(0);
            }}
            rowsPerPageOptions={[10, 25, 50]}
          />
        </>
      )}

      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} fullWidth maxWidth="md">
        <DialogTitle>{editingId ? "Edit staff" : "Add staff"}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <Typography variant="subtitle2" color="text.secondary">
              College & role
            </Typography>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <FormControl fullWidth required size="small" disabled={!!editingId}>
                <InputLabel>College</InputLabel>
                <Select
                  label="College"
                  value={form.collegeId || ""}
                  onChange={(e) =>
                    setForm((f) => ({
                      ...f,
                      collegeId: Number(e.target.value),
                      deptAssignments: [],
                    }))
                  }
                >
                  {(meta?.colleges ?? []).map((c) => (
                    <MenuItem key={c.id} value={c.id}>
                      {c.name} ({c.code})
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
              {meta &&
                renderLookupSelect(
                  "Staff type",
                  form.staffTypeId,
                  meta.staffTypes,
                  (id) => setForm((f) => ({ ...f, staffTypeId: typeof id === "number" ? id : 0 })),
                  true,
                )}
              {meta &&
                renderLookupSelect(
                  "Designation",
                  form.designationId,
                  meta.designations,
                  (id) => setForm((f) => ({ ...f, designationId: typeof id === "number" ? id : 0 })),
                  true,
                )}
            </Stack>

            <Typography variant="subtitle2" color="text.secondary">
              Person
            </Typography>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <TextField
                size="small"
                label="Staff code"
                value={form.staffCode}
                onChange={(e) => setForm((f) => ({ ...f, staffCode: e.target.value }))}
                fullWidth
                helperText="Unique per college when set"
              />
              {meta &&
                renderLookupSelect("Title", form.personTitleId, meta.personTitles, (id) =>
                  setForm((f) => ({ ...f, personTitleId: id })),
                )}
            </Stack>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <TextField
                size="small"
                label="First name"
                value={form.firstName}
                onChange={(e) => setForm((f) => ({ ...f, firstName: e.target.value }))}
                fullWidth
                required
              />
              <TextField
                size="small"
                label="Last name"
                value={form.lastName}
                onChange={(e) => setForm((f) => ({ ...f, lastName: e.target.value }))}
                fullWidth
                required
              />
            </Stack>

            <Typography variant="subtitle2" color="text.secondary">
              Profile & HR
            </Typography>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              {meta &&
                renderLookupSelect("Qualification", form.qualificationId, meta.qualifications, (id) =>
                  setForm((f) => ({ ...f, qualificationId: id })),
                )}
              <FormControl fullWidth size="small">
                <InputLabel>Gender</InputLabel>
                <Select
                  label="Gender"
                  value={form.genderId === "" ? "" : form.genderId}
                  onChange={(e) => {
                    const v = e.target.value as string | number;
                    const s = String(v);
                    setForm((f) => ({ ...f, genderId: s === "" ? "" : Number(s) }));
                  }}
                >
                  <MenuItem value="">—</MenuItem>
                  {genders.map((g) => (
                    <MenuItem key={g.id} value={g.id}>
                      {g.name}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
              {meta &&
                renderLookupSelect("Employment status", form.employmentStatusId, meta.employmentStatuses, (id) =>
                  setForm((f) => ({ ...f, employmentStatusId: id })),
                )}
            </Stack>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <TextField
                size="small"
                label="Phone"
                value={form.phone}
                onChange={(e) => setForm((f) => ({ ...f, phone: e.target.value }))}
                fullWidth
              />
              <TextField
                size="small"
                label="Alt phone"
                value={form.altPhone}
                onChange={(e) => setForm((f) => ({ ...f, altPhone: e.target.value }))}
                fullWidth
              />
            </Stack>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <TextField
                size="small"
                label="Email"
                type="email"
                value={form.email}
                onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))}
                fullWidth
              />
              <TextField
                size="small"
                label="Website"
                value={form.website}
                onChange={(e) => setForm((f) => ({ ...f, website: e.target.value }))}
                fullWidth
              />
            </Stack>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <TextField
                size="small"
                label="Date of joining"
                type="date"
                value={form.dateOfJoining}
                onChange={(e) => setForm((f) => ({ ...f, dateOfJoining: e.target.value }))}
                fullWidth
                slotProps={{ inputLabel: { shrink: true } }}
              />
              <TextField
                size="small"
                label="Contract end"
                type="date"
                value={form.contractEndDate}
                onChange={(e) => setForm((f) => ({ ...f, contractEndDate: e.target.value }))}
                fullWidth
                slotProps={{ inputLabel: { shrink: true } }}
              />
              <TextField
                size="small"
                label="Date of birth"
                type="date"
                value={form.dateOfBirth}
                onChange={(e) => setForm((f) => ({ ...f, dateOfBirth: e.target.value }))}
                fullWidth
                slotProps={{ inputLabel: { shrink: true } }}
              />
            </Stack>

            {teaching && meta && (
              <>
                <Divider sx={{ my: 1 }} />
                <Typography variant="subtitle2" color="text.secondary">
                  Teaching assignments
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Department roles (one row per department). College roles and subjects apply to teaching staff only.
                </Typography>

                {form.deptAssignments.map((row, idx) => (
                  <Stack
                    key={idx}
                    direction={{ xs: "column", md: "row" }}
                    spacing={2}
                    sx={{ alignItems: { md: "flex-start" } }}
                  >
                    <FormControl size="small" sx={{ minWidth: 220 }}>
                      <InputLabel>Department</InputLabel>
                      <Select
                        label="Department"
                        value={row.departmentId || ""}
                        onChange={(e) => updateDeptRow(idx, { departmentId: Number(e.target.value) })}
                      >
                        <MenuItem value={0}>—</MenuItem>
                        {deptOptions.map((d) => (
                          <MenuItem key={d.id} value={d.id}>
                            {d.name}
                            {d.code ? ` (${d.code})` : ""}
                          </MenuItem>
                        ))}
                      </Select>
                    </FormControl>
                    <FormControl size="small" sx={{ flex: 1, minWidth: 240 }}>
                      <InputLabel>Department roles</InputLabel>
                      <Select
                        label="Department roles"
                        multiple
                        value={row.departmentRoleLookupIds}
                        onChange={(e) =>
                          updateDeptRow(idx, {
                            departmentRoleLookupIds:
                              typeof e.target.value === "string"
                                ? []
                                : (e.target.value as number[]).map(Number),
                          })
                        }
                        renderValue={(selected) => (
                          <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.5 }}>
                            {(selected as number[]).map((rid) => (
                              <Chip
                                key={rid}
                                size="small"
                                label={meta.departmentRoles.find((x) => x.id === rid)?.name ?? rid}
                              />
                            ))}
                          </Box>
                        )}
                      >
                        {meta.departmentRoles.map((r) => (
                          <MenuItem key={r.id} value={r.id}>
                            <Checkbox checked={row.departmentRoleLookupIds.includes(r.id)} />
                            <ListItemText primary={r.name} />
                          </MenuItem>
                        ))}
                      </Select>
                    </FormControl>
                    <Button
                      color="error"
                      size="small"
                      onClick={() =>
                        setForm((f) => ({
                          ...f,
                          deptAssignments: f.deptAssignments.filter((_, i) => i !== idx),
                        }))
                      }
                    >
                      Remove
                    </Button>
                  </Stack>
                ))}
                <Button
                  variant="outlined"
                  size="small"
                  startIcon={<AddIcon />}
                  onClick={() =>
                    setForm((f) => ({
                      ...f,
                      deptAssignments: [...f.deptAssignments, { departmentId: 0, departmentRoleLookupIds: [] }],
                    }))
                  }
                  disabled={!form.collegeId}
                >
                  Add department row
                </Button>

                <FormControl fullWidth size="small">
                  <InputLabel>College-wide roles</InputLabel>
                  <Select
                    label="College-wide roles"
                    multiple
                    value={form.collegeRoleLookupIds}
                    onChange={(e) =>
                      setForm((f) => ({
                        ...f,
                        collegeRoleLookupIds:
                          typeof e.target.value === "string" ? [] : (e.target.value as number[]).map(Number),
                      }))
                    }
                    renderValue={(selected) => (
                      <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.5 }}>
                        {(selected as number[]).map((rid) => (
                          <Chip
                            key={rid}
                            size="small"
                            label={meta.collegeRoles.find((x) => x.id === rid)?.name ?? rid}
                          />
                        ))}
                      </Box>
                    )}
                  >
                    {meta.collegeRoles.map((r) => (
                      <MenuItem key={r.id} value={r.id}>
                        <Checkbox checked={form.collegeRoleLookupIds.includes(r.id)} />
                        <ListItemText primary={r.name} />
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>

                <Autocomplete
                  multiple
                  options={subjectOptions}
                  value={selectedSubjects}
                  onChange={(_, v) =>
                    setForm((f) => ({ ...f, subjectIds: v.map((x) => x.id) }))
                  }
                  getOptionLabel={(o) => o.label}
                  renderInput={(params) => (
                    <TextField {...params} label="Subject assignments" placeholder="Search subjects" size="small" />
                  )}
                />
              </>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)} disabled={saving}>
            Cancel
          </Button>
          <Button variant="contained" onClick={() => void save()} disabled={saving}>
            {saving ? "Saving…" : "Save"}
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default StaffPage;
