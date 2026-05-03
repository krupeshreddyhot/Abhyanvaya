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
  createSemester,
  listMasterCourses,
  listMasterGroups,
  listSemesters,
  updateSemester,
  type CourseRow,
  type GroupRow,
  type SemesterRow,
} from "../../services/setupService";

const errMsg = (e: unknown): string => {
  const d = (e as { response?: { data?: unknown } }).response?.data;
  if (typeof d === "string") return d;
  return "Request failed.";
};

const NONE_GROUP = 0;

const SemestersPage = () => {
  const [courses, setCourses] = useState<CourseRow[]>([]);
  const [groups, setGroups] = useState<GroupRow[]>([]);
  const [rows, setRows] = useState<SemesterRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [editingId, setEditingId] = useState(0);
  const [numberStr, setNumberStr] = useState("1");
  const [name, setName] = useState("");
  const [courseId, setCourseId] = useState(0);
  const [groupId, setGroupId] = useState(NONE_GROUP);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const [cRes, gRes, sRes] = await Promise.all([
        listMasterCourses(),
        listMasterGroups(),
        listSemesters(),
      ]);
      setCourses(cRes.data);
      setGroups(gRes.data);
      setRows(sRes.data);
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

  const openAdd = () => {
    setEditingId(0);
    setNumberStr("1");
    setName("");
    const c0 = courses[0]?.id ?? 0;
    setCourseId(c0);
    setGroupId(groups.find((g) => g.courseId === c0)?.id ?? NONE_GROUP);
    setDialogOpen(true);
  };

  const openEdit = (r: SemesterRow) => {
    setEditingId(r.id);
    setNumberStr(String(r.number));
    setName(r.name);
    setCourseId(r.courseId);
    setGroupId(r.groupId ?? NONE_GROUP);
    setDialogOpen(true);
  };

  const save = async () => {
    const num = Number.parseInt(numberStr, 10);
    const n = name.trim();
    if (!n || !courseId || Number.isNaN(num) || num < 1) {
      setError("Valid number, name and course are required.");
      return;
    }
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      const payloadBase = {
        number: num,
        name: n,
        courseId,
        groupId: groupId === NONE_GROUP ? null : groupId,
      };
      if (editingId) await updateSemester({ id: editingId, ...payloadBase });
      else await createSemester(payloadBase);
      setMessage(editingId ? "Semester updated." : "Semester created.");
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
          Semesters
        </Typography>
        <Button variant="contained" onClick={openAdd} disabled={!courses.length}>
          Add semester
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
              <TableCell>#</TableCell>
              <TableCell>Name</TableCell>
              <TableCell>Course</TableCell>
              <TableCell>Group</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((r) => (
              <TableRow key={r.id} hover>
                <TableCell>{r.number}</TableCell>
                <TableCell>{r.name}</TableCell>
                <TableCell>{r.courseName}</TableCell>
                <TableCell>{r.groupName ?? "—"}</TableCell>
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
        <DialogTitle>{editingId ? "Edit semester" : "Add semester"}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              select
              label="Course"
              value={courseId || ""}
              onChange={(e) => {
                const cid = Number(e.target.value);
                setCourseId(cid);
                const firstG = groups.find((g) => g.courseId === cid);
                setGroupId(firstG?.id ?? NONE_GROUP);
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
              label="Group (optional)"
              value={groupId === NONE_GROUP ? "" : groupId}
              onChange={(e) => setGroupId(e.target.value ? Number(e.target.value) : NONE_GROUP)}
              fullWidth
              helperText="Leave empty when the semester applies to the whole course."
            >
              <MenuItem value="">— None —</MenuItem>
              {groupsForCourse.map((g) => (
                <MenuItem key={g.id} value={g.id}>
                  {g.name}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              label="Semester number"
              type="number"
              value={numberStr}
              onChange={(e) => setNumberStr(e.target.value)}
              fullWidth
              required
              slotProps={{ htmlInput: { min: 1 } }}
            />
            <TextField label="Display name" value={name} onChange={(e) => setName(e.target.value)} fullWidth required />
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

export default SemestersPage;
