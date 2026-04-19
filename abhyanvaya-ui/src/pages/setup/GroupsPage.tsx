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
  MenuItem,
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
import { createGroup, listCourses, listGroups, updateGroup, type CourseRow, type GroupRow } from "../../services/setupService";

const errMsg = (e: unknown): string => {
  const d = (e as { response?: { data?: unknown } }).response?.data;
  if (typeof d === "string") return d;
  return "Request failed.";
};

const GroupsPage = () => {
  const [courses, setCourses] = useState<CourseRow[]>([]);
  const [rows, setRows] = useState<GroupRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [editingId, setEditingId] = useState(0);
  const [name, setName] = useState("");
  const [courseId, setCourseId] = useState(0);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const [cRes, gRes] = await Promise.all([listCourses(), listGroups()]);
      setCourses(cRes.data);
      setRows(gRes.data);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const openAdd = () => {
    setEditingId(0);
    setName("");
    setCourseId(courses[0]?.id ?? 0);
    setDialogOpen(true);
  };

  const openEdit = (r: GroupRow) => {
    setEditingId(r.id);
    setName(r.name);
    setCourseId(r.courseId);
    setDialogOpen(true);
  };

  const save = async () => {
    const n = name.trim();
    if (!n || !courseId) {
      setError("Name and course are required.");
      return;
    }
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      if (editingId) await updateGroup({ id: editingId, name: n, courseId });
      else await createGroup({ name: n, courseId });
      setMessage(editingId ? "Group updated." : "Group created.");
      setDialogOpen(false);
      await load();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup" startIcon={<ArrowBackIcon />} variant="text">
          Catalog
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Groups
        </Typography>
        <Button variant="contained" onClick={openAdd} disabled={!courses.length}>
          Add group
        </Button>
      </Box>
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
              <TableCell>Course</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((r) => (
              <TableRow key={r.id} hover>
                <TableCell>{r.name}</TableCell>
                <TableCell>{r.courseName}</TableCell>
                <TableCell align="right">
                  <Button size="small" onClick={() => openEdit(r)}>
                    Edit
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>{editingId ? "Edit group" : "Add group"}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              select
              label="Course"
              value={courseId || ""}
              onChange={(e) => setCourseId(Number(e.target.value))}
              fullWidth
              required
            >
              {courses.map((c) => (
                <MenuItem key={c.id} value={c.id}>
                  {c.name}
                </MenuItem>
              ))}
            </TextField>
            <TextField label="Group name" value={name} onChange={(e) => setName(e.target.value)} fullWidth required />
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

export default GroupsPage;
