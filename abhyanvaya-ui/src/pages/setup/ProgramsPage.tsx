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
  FormControlLabel,
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
import { PermissionKeys } from "../../auth/permissionKeys";
import { useAuth } from "../../context/AuthContext";
import {
  archiveProgram,
  createProgram,
  deleteProgram,
  getAcademicConfiguration,
  listPrograms,
  updateAcademicConfiguration,
  updateProgram,
  type ProgramDto,
} from "../../services/programService";

const errMsg = (e: unknown): string => {
  const d = (e as { response?: { data?: unknown } }).response?.data;
  if (typeof d === "string") return d;
  return "Request failed.";
};

const ProgramsPage = () => {
  const { hasPermission } = useAuth();
  const canCreate = hasPermission(PermissionKeys.ProgramCreate);
  const canEdit = hasPermission(PermissionKeys.ProgramEdit);
  const canDelete = hasPermission(PermissionKeys.ProgramDelete);
  const canManage = hasPermission(PermissionKeys.ProgramManage);

  const [rows, setRows] = useState<ProgramDto[]>([]);
  const [enablePrograms, setEnablePrograms] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [viewOpen, setViewOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [editingId, setEditingId] = useState(0);
  const [viewRow, setViewRow] = useState<ProgramDto | null>(null);
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [displayOrder, setDisplayOrder] = useState(0);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const [cfg, list] = await Promise.all([getAcademicConfiguration(), listPrograms(true)]);
      setEnablePrograms(cfg.data.enablePrograms);
      setRows(list.data);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const togglePrograms = async (next: boolean) => {
    if (!canManage) return;
    try {
      const res = await updateAcademicConfiguration(next);
      setEnablePrograms(res.data.enablePrograms);
      setMessage(next ? "Programs enabled — hierarchy is College → Program → Course." : "Programs disabled — hierarchy is College → Course.");
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const openAdd = () => {
    setEditingId(0);
    setCode("");
    setName("");
    setDescription("");
    setDisplayOrder(rows.length);
    setDialogOpen(true);
  };

  const openEdit = (r: ProgramDto) => {
    setEditingId(r.id);
    setCode(r.programCode);
    setName(r.programName);
    setDescription(r.description ?? "");
    setDisplayOrder(r.displayOrder);
    setDialogOpen(true);
  };

  const save = async () => {
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      if (editingId) {
        await updateProgram(editingId, {
          programCode: code,
          programName: name,
          description,
          displayOrder,
          isActive: true,
          status: "Active",
        });
        setMessage("Program updated.");
      } else {
        await createProgram({
          programCode: code,
          programName: name,
          description,
          displayOrder,
          isActive: true,
        });
        setMessage("Program created.");
      }
      setDialogOpen(false);
      await load();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  };

  const doArchive = async (id: number) => {
    if (!window.confirm("Archive this program? It cannot receive new courses.")) return;
    try {
      await archiveProgram(id);
      setMessage("Program archived.");
      await load();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const doDelete = async (id: number) => {
    if (!window.confirm("Soft-delete this program? Courses must be unlinked first.")) return;
    try {
      await deleteProgram(id);
      setMessage("Program deleted.");
      await load();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  return (
    <Box sx={{ p: 2, maxWidth: 1100, mx: "auto" }}>
      <Button component={RouterLink} to="/setup" startIcon={<ArrowBackIcon />} sx={{ mb: 1 }}>
        Catalog
      </Button>
      <Typography variant="h5" sx={{ fontWeight: 800, mb: 0.5 }}>
        Programs
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Optional academic grouping (Commerce, Arts, Science, …). When disabled, Course remains the top catalog level.
        Attendance and timetable workflows are unchanged.
      </Typography>

      {error && (
        <Alert severity="error" sx={{ mb: 1.5 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}
      {message && (
        <Alert severity="success" sx={{ mb: 1.5 }} onClose={() => setMessage(null)}>
          {message}
        </Alert>
      )}

      <Stack direction="row" spacing={2} sx={{ mb: 2, alignItems: "center", flexWrap: "wrap" }}>
        <FormControlLabel
          control={
            <Switch
              checked={enablePrograms}
              onChange={(_, v) => void togglePrograms(v)}
              disabled={!canManage}
            />
          }
          label={enablePrograms ? "Programs enabled" : "Programs disabled"}
        />
        {canCreate && (
          <Button variant="contained" onClick={openAdd} disabled={!enablePrograms}>
            Create Program
          </Button>
        )}
        <Button variant="outlined" onClick={() => void load()}>
          Refresh
        </Button>
      </Stack>

      {!enablePrograms && (
        <Alert severity="info" sx={{ mb: 2 }}>
          Enable Programs to manage Program → Course hierarchy. Existing Course → Group → Semester → Subject structure
          continues to work either way.
        </Alert>
      )}

      {loading ? (
        <CircularProgress />
      ) : (
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Code</TableCell>
              <TableCell>Name</TableCell>
              <TableCell>Courses</TableCell>
              <TableCell>Students</TableCell>
              <TableCell>Faculty</TableCell>
              <TableCell>Status</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((r) => (
              <TableRow key={r.id}>
                <TableCell>{r.programCode}</TableCell>
                <TableCell>{r.programName}</TableCell>
                <TableCell>{r.courseCount}</TableCell>
                <TableCell>{r.studentCount}</TableCell>
                <TableCell>{r.facultyCount}</TableCell>
                <TableCell>{r.status}</TableCell>
                <TableCell align="right">
                  <Button
                    size="small"
                    onClick={() => {
                      setViewRow(r);
                      setViewOpen(true);
                    }}
                  >
                    View
                  </Button>
                  {canEdit && r.status !== "Archived" && (
                    <Button size="small" onClick={() => openEdit(r)}>
                      Edit
                    </Button>
                  )}
                  {canEdit && r.status !== "Archived" && (
                    <Button size="small" onClick={() => void doArchive(r.id)}>
                      Archive
                    </Button>
                  )}
                  {canDelete && (
                    <Button size="small" color="error" onClick={() => void doDelete(r.id)}>
                      Delete
                    </Button>
                  )}
                </TableCell>
              </TableRow>
            ))}
            {rows.length === 0 && (
              <TableRow>
                <TableCell colSpan={7}>
                  <Typography variant="body2" color="text.secondary">
                    No programs yet.
                  </Typography>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      )}

      <Dialog open={dialogOpen} onClose={() => setDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>{editingId ? "Edit Program" : "Create Program"}</DialogTitle>
        <DialogContent>
          <Stack spacing={1.5} sx={{ mt: 1 }}>
            <TextField label="Program Code" value={code} onChange={(e) => setCode(e.target.value)} helperText="e.g. COM, SCI, ENG" />
            <TextField label="Program Name" value={name} onChange={(e) => setName(e.target.value)} helperText="e.g. Commerce, Science" />
            <TextField label="Description" value={description} onChange={(e) => setDescription(e.target.value)} multiline minRows={2} />
            <TextField type="number" label="Display Order" value={displayOrder} onChange={(e) => setDisplayOrder(Number(e.target.value))} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)}>Cancel</Button>
          <Button variant="contained" disabled={saving} onClick={() => void save()}>
            Save
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={viewOpen} onClose={() => setViewOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>View Program</DialogTitle>
        <DialogContent>
          {viewRow && (
            <Stack spacing={1} sx={{ mt: 1 }}>
              <Typography>
                <strong>{viewRow.programCode}</strong> — {viewRow.programName}
              </Typography>
              <Typography variant="body2">{viewRow.description || "No description."}</Typography>
              <Typography variant="body2">Courses: {viewRow.courseCount}</Typography>
              <Typography variant="body2">Students: {viewRow.studentCount}</Typography>
              <Typography variant="body2">Faculty: {viewRow.facultyCount}</Typography>
              <Typography variant="body2">Status: {viewRow.status}</Typography>
            </Stack>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setViewOpen(false)}>Close</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};

export default ProgramsPage;
