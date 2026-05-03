import { useEffect, useState } from "react";
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
  Typography,
} from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import { PermissionKeys } from "../../auth/permissionKeys";
import { useAuth } from "../../context/AuthContext";
import {
  createDepartment,
  deleteDepartment,
  getStaffSetupMetadata,
  listDepartments,
  updateDepartment,
  type CollegeSummary,
  type DepartmentRow,
} from "../../services/setupService";

const errMsg = (e: unknown): string => {
  const d = (e as { response?: { data?: unknown } }).response?.data;
  if (typeof d === "string") return d;
  return "Request failed.";
};

const DepartmentsPage = () => {
  const { hasAnyPermission } = useAuth();
  const canManageLookups = hasAnyPermission([PermissionKeys.SetupLookupsManage]);

  const [colleges, setColleges] = useState<CollegeSummary[]>([]);
  const [rows, setRows] = useState<DepartmentRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [editingId, setEditingId] = useState(0);
  const [collegeId, setCollegeId] = useState(0);
  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [sortOrder, setSortOrder] = useState(0);

  const loadMeta = async () => {
    try {
      const res = await getStaffSetupMetadata();
      setColleges(res.data.colleges);
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await listDepartments(undefined);
      setRows(res.data);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadMeta();
  }, []);

  useEffect(() => {
    void load();
  }, []);

  const openAdd = () => {
    setEditingId(0);
    setCollegeId(colleges[0]?.id || 0);
    setName("");
    setCode("");
    setSortOrder(0);
    setDialogOpen(true);
  };

  const openEdit = (r: DepartmentRow) => {
    setEditingId(r.id);
    setCollegeId(r.collegeId);
    setName(r.name);
    setCode(r.code ?? "");
    setSortOrder(r.sortOrder);
    setDialogOpen(true);
  };

  const save = async () => {
    const n = name.trim();
    if (!n) {
      setError("Name is required.");
      return;
    }
    if (!collegeId) {
      setError("College is required.");
      return;
    }
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      const codeVal = code.trim() === "" ? null : code.trim();
      if (editingId) {
        await updateDepartment(editingId, { name: n, code: codeVal, sortOrder });
      } else {
        await createDepartment({ collegeId, name: n, code: codeVal, sortOrder });
      }
      setMessage(editingId ? "Department updated." : "Department created.");
      setDialogOpen(false);
      await load();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  };

  const remove = async (r: DepartmentRow) => {
    if (!window.confirm(`Delete department "${r.name}"?`)) return;
    setError(null);
    setMessage(null);
    try {
      await deleteDepartment(r.id);
      setMessage("Department deleted.");
      await load();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup" startIcon={<ArrowBackIcon />} variant="text">
          Catalog
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Departments
        </Typography>
        <Button variant="contained" onClick={openAdd} disabled={!colleges.length}>
          Add department
        </Button>
      </Box>
      {canManageLookups && (
        <Typography variant="body2" color="text.secondary">
          Labels for roles when assigning staff to departments are edited under{" "}
          <RouterLink to="/setup/staff-lookups">Staff &amp; department lookups</RouterLink> (Department roles tab).
        </Typography>
      )}
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
              <TableCell>Code</TableCell>
              <TableCell>Name</TableCell>
              <TableCell>Sort</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((r) => (
              <TableRow key={r.id} hover>
                <TableCell>{r.code ?? "—"}</TableCell>
                <TableCell>{r.name}</TableCell>
                <TableCell>{r.sortOrder}</TableCell>
                <TableCell align="right">
                  <Button size="small" onClick={() => openEdit(r)}>
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
      )}
      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>{editingId ? "Edit department" : "Add department"}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <FormControl fullWidth required disabled={!!editingId}>
              <InputLabel id="dept-col">College</InputLabel>
              <Select
                labelId="dept-col"
                label="College"
                value={collegeId || ""}
                onChange={(e) => setCollegeId(Number(e.target.value))}
              >
                {colleges.map((c) => (
                  <MenuItem key={c.id} value={c.id}>
                    {c.name} ({c.code})
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <TextField label="Name" value={name} onChange={(e) => setName(e.target.value)} fullWidth required />
            <TextField
              label="Code (optional)"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              fullWidth
              helperText="Unique per college when set"
            />
            <TextField
              label="Sort order"
              type="number"
              value={sortOrder}
              onChange={(e) => setSortOrder(Number(e.target.value) || 0)}
              fullWidth
            />
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

export default DepartmentsPage;
