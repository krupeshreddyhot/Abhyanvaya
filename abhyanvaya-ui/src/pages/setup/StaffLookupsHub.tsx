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
  FormControlLabel,
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
import {
  createStaffHubLookup,
  deleteStaffHubLookup,
  listStaffHubLookups,
  updateStaffHubLookup,
  type StaffHubLookupKind,
  type StaffLookupAdminRow,
  type StaffLookupWritePayload,
} from "../../services/setupService";

const errMsg = (e: unknown): string => {
  const d = (e as { response?: { data?: unknown } }).response?.data;
  if (typeof d === "string") return d;
  return "Request failed.";
};

type TabDef = { kind: StaffHubLookupKind; label: string };

const tabs: TabDef[] = [
  { kind: "staff-types", label: "Staff types" },
  { kind: "person-titles", label: "Person titles" },
  { kind: "designations", label: "Designations" },
  { kind: "qualifications", label: "Qualifications" },
  { kind: "employment-statuses", label: "Employment statuses" },
  { kind: "department-roles", label: "Department roles" },
  { kind: "college-roles", label: "College roles" },
];

const emptyForm = (): StaffLookupWritePayload => ({
  name: "",
  code: "",
  sortOrder: 0,
  isActive: true,
  isExclusivePerDepartment: false,
  isExclusivePerCollege: false,
});

const StaffLookupsHub = () => {
  const [tabIndex, setTabIndex] = useState(0);
  const kind = tabs[tabIndex]!.kind;

  const [rows, setRows] = useState<StaffLookupAdminRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [editingId, setEditingId] = useState(0);
  const [form, setForm] = useState<StaffLookupWritePayload>(emptyForm);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await listStaffHubLookups(kind);
      setRows(res.data);
    } catch (e) {
      setError(errMsg(e));
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, [kind]);

  useEffect(() => {
    void load();
  }, [load]);

  const openAdd = () => {
    setEditingId(0);
    setForm(emptyForm());
    setDialogOpen(true);
  };

  const openEdit = (r: StaffLookupAdminRow) => {
    setEditingId(r.id);
    setForm({
      name: r.name,
      code: r.code ?? "",
      sortOrder: r.sortOrder,
      isActive: r.isActive,
      isExclusivePerDepartment: r.isExclusivePerDepartment,
      isExclusivePerCollege: r.isExclusivePerCollege,
    });
    setDialogOpen(true);
  };

  const save = async () => {
    const n = form.name.trim();
    if (!n) {
      setError("Name is required.");
      return;
    }
    setSaving(true);
    setError(null);
    setMessage(null);
    const payload: StaffLookupWritePayload = {
      name: n,
      code: form.code?.trim() || null,
      sortOrder: Number(form.sortOrder) || 0,
      isActive: form.isActive,
      isExclusivePerDepartment: kind === "department-roles" ? form.isExclusivePerDepartment : false,
      isExclusivePerCollege: kind === "college-roles" ? form.isExclusivePerCollege : false,
    };
    try {
      if (editingId) {
        await updateStaffHubLookup(kind, editingId, payload);
        setMessage("Updated.");
      } else {
        await createStaffHubLookup(kind, payload);
        setMessage("Created.");
      }
      setDialogOpen(false);
      await load();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  };

  const remove = async (r: StaffLookupAdminRow) => {
    if (!window.confirm(`Remove "${r.name}"? It will be hidden from pickers if unused.`)) return;
    setError(null);
    setMessage(null);
    try {
      await deleteStaffHubLookup(kind, r.id);
      setMessage("Removed.");
      await load();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const showDeptExclusive = kind === "department-roles";
  const showCollegeExclusive = kind === "college-roles";

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup" startIcon={<ArrowBackIcon />} variant="text">
          Catalog
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Staff &amp; department lookups
        </Typography>
        <Button variant="contained" onClick={openAdd}>
          Add row
        </Button>
      </Box>

      <Typography variant="body2" color="text.secondary">
        Values used on the Staff page (types, titles, designations, qualifications, employment, department roles, college
        roles). Department roles apply when assigning staff to departments; college roles are institution-wide.
      </Typography>

      <Tabs value={tabIndex} onChange={(_, v) => setTabIndex(v)} variant="scrollable" scrollButtons="auto">
        {tabs.map((t, i) => (
          <Tab key={t.kind} label={t.label} value={i} />
        ))}
      </Tabs>

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
              <TableCell>Name</TableCell>
              <TableCell>Code</TableCell>
              <TableCell align="right">Sort</TableCell>
              <TableCell>Active</TableCell>
              {showDeptExclusive && <TableCell>Exclusive / dept</TableCell>}
              {showCollegeExclusive && <TableCell>Exclusive / college</TableCell>}
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((r) => (
              <TableRow key={r.id} hover>
                <TableCell>{r.name}</TableCell>
                <TableCell>{r.code ?? "—"}</TableCell>
                <TableCell align="right">{r.sortOrder}</TableCell>
                <TableCell>{r.isActive ? "Yes" : "No"}</TableCell>
                {showDeptExclusive && <TableCell>{r.isExclusivePerDepartment ? "Yes" : "No"}</TableCell>}
                {showCollegeExclusive && <TableCell>{r.isExclusivePerCollege ? "Yes" : "No"}</TableCell>}
                <TableCell align="right">
                  <Button size="small" onClick={() => openEdit(r)}>
                    Edit
                  </Button>{" "}
                  <Button size="small" color="error" onClick={() => void remove(r)}>
                    Remove
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>{editingId ? "Edit" : "Add"} — {tabs[tabIndex]?.label}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              label="Name"
              value={form.name}
              onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
              fullWidth
              required
            />
            <TextField
              label="Code (optional)"
              value={form.code ?? ""}
              onChange={(e) => setForm((f) => ({ ...f, code: e.target.value }))}
              fullWidth
              helperText="Stable code (e.g. TEACHING for staff type). Used by integrations."
            />
            <TextField
              label="Sort order"
              type="number"
              value={form.sortOrder}
              onChange={(e) => setForm((f) => ({ ...f, sortOrder: Number(e.target.value) || 0 }))}
              fullWidth
            />
            <FormControlLabel
              control={
                <Checkbox
                  checked={form.isActive}
                  onChange={(e) => setForm((f) => ({ ...f, isActive: e.target.checked }))}
                />
              }
              label="Active (shown in Staff forms)"
            />
            {showDeptExclusive && (
              <FormControlLabel
                control={
                  <Checkbox
                    checked={form.isExclusivePerDepartment}
                    onChange={(e) => setForm((f) => ({ ...f, isExclusivePerDepartment: e.target.checked }))}
                  />
                }
                label="At most one staff member per department can hold this role"
              />
            )}
            {showCollegeExclusive && (
              <FormControlLabel
                control={
                  <Checkbox
                    checked={form.isExclusivePerCollege}
                    onChange={(e) => setForm((f) => ({ ...f, isExclusivePerCollege: e.target.checked }))}
                  />
                }
                label="At most one staff member per college can hold this role"
              />
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

export default StaffLookupsHub;
