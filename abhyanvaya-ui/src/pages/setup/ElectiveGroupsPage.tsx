import { useEffect, useMemo, useState } from "react";
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
import {
  createElectiveGroup,
  listCourses,
  listElectiveGroups,
  listGroups,
  listSemesters,
  updateElectiveGroup,
  type CourseRow,
  type ElectiveGroupRow,
  type GroupRow,
  type SemesterRow,
} from "../../services/setupService";

const errMsg = (e: unknown): string => {
  const d = (e as { response?: { data?: unknown } }).response?.data;
  if (typeof d === "string") return d;
  return "Request failed.";
};

const ElectiveGroupsPage = () => {
  const [courses, setCourses] = useState<CourseRow[]>([]);
  const [groups, setGroups] = useState<GroupRow[]>([]);
  const [semesters, setSemesters] = useState<SemesterRow[]>([]);
  const [rows, setRows] = useState<ElectiveGroupRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [editingId, setEditingId] = useState(0);
  const [name, setName] = useState("");
  const [courseId, setCourseId] = useState(0);
  const [groupId, setGroupId] = useState(0);
  const [semesterId, setSemesterId] = useState(0);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const [cRes, gRes, sRes, eRes] = await Promise.all([
        listCourses(),
        listGroups(),
        listSemesters(),
        listElectiveGroups(),
      ]);
      setCourses(cRes.data);
      setGroups(gRes.data);
      setSemesters(sRes.data);
      setRows(eRes.data);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const groupsForCourse = useMemo(
    () => groups.filter((g) => g.courseId === courseId),
    [groups, courseId],
  );

  const semestersForSelection = useMemo(() => {
    return semesters.filter((s) => {
      if (s.courseId !== courseId) return false;
      if (s.groupId == null) return true;
      return s.groupId === groupId;
    });
  }, [semesters, courseId, groupId]);

  const openAdd = () => {
    setEditingId(0);
    setName("");
    const c0 = courses[0]?.id ?? 0;
    setCourseId(c0);
    const g0 = groups.find((x) => x.courseId === c0)?.id ?? 0;
    setGroupId(g0);
    const sem0 = semesters.find((s) => s.courseId === c0 && (s.groupId == null || s.groupId === g0))?.id ?? 0;
    setSemesterId(sem0);
    setDialogOpen(true);
  };

  const openEdit = (r: ElectiveGroupRow) => {
    setEditingId(r.id);
    setName(r.name);
    setCourseId(r.courseId);
    setGroupId(r.groupId);
    setSemesterId(r.semesterId);
    setDialogOpen(true);
  };

  const save = async () => {
    const n = name.trim();
    if (!n || !courseId || !groupId || !semesterId) {
      setError("All fields are required.");
      return;
    }
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      const payload = { name: n, courseId, groupId, semesterId };
      if (editingId) await updateElectiveGroup({ id: editingId, ...payload });
      else await createElectiveGroup(payload);
      setMessage(editingId ? "Elective group updated." : "Elective group created.");
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
          Elective groups
        </Typography>
        <Button variant="contained" onClick={openAdd} disabled={!courses.length}>
          Add elective group
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
              <TableCell>Group</TableCell>
              <TableCell>Semester</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((r) => (
              <TableRow key={r.id} hover>
                <TableCell>{r.name}</TableCell>
                <TableCell>{r.courseName}</TableCell>
                <TableCell>{r.groupName}</TableCell>
                <TableCell>{r.semesterName}</TableCell>
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
        <DialogTitle>{editingId ? "Edit elective group" : "Add elective group"}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              select
              label="Course"
              value={courseId || ""}
              onChange={(e) => {
                const cid = Number(e.target.value);
                setCourseId(cid);
                const g = groups.find((x) => x.courseId === cid);
                setGroupId(g?.id ?? 0);
                const sem = semesters.find((s) => s.courseId === cid && (s.groupId == null || s.groupId === g?.id));
                setSemesterId(sem?.id ?? 0);
              }}
              fullWidth
              required
            >
              {courses.map((c) => (
                <MenuItem key={c.id} value={c.id}>
                  {c.name}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              select
              label="Group"
              value={groupId || ""}
              onChange={(e) => {
                const gid = Number(e.target.value);
                setGroupId(gid);
                const sem = semesters.find((s) => s.courseId === courseId && (s.groupId == null || s.groupId === gid));
                setSemesterId(sem?.id ?? 0);
              }}
              fullWidth
              required
            >
              {groupsForCourse.map((g) => (
                <MenuItem key={g.id} value={g.id}>
                  {g.name}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              select
              label="Semester"
              value={semesterId || ""}
              onChange={(e) => setSemesterId(Number(e.target.value))}
              fullWidth
              required
            >
              {semestersForSelection.map((s) => (
                <MenuItem key={s.id} value={s.id}>
                  {s.name} (#{s.number})
                </MenuItem>
              ))}
            </TextField>
            <TextField label="Elective group name" value={name} onChange={(e) => setName(e.target.value)} fullWidth required />
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

export default ElectiveGroupsPage;
