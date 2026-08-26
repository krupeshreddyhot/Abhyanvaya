import { useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
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
  const [groupId, setGroupId] = useState(0);
  const [editingLegacy, setEditingLegacy] = useState(false);

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
    setEditingLegacy(false);
    setNumberStr("1");
    setName("");
    const c0 = courses[0]?.id ?? 0;
    setCourseId(c0);
    const g0 = groups.find((g) => g.courseId === c0)?.id ?? 0;
    setGroupId(g0);
    setDialogOpen(true);
  };

  const openEdit = (r: SemesterRow) => {
    setEditingId(r.id);
    setEditingLegacy(r.groupId == null);
    setNumberStr(String(r.number));
    setName(r.name);
    setCourseId(r.courseId);
    setGroupId(r.groupId ?? 0);
    setDialogOpen(true);
  };

  const save = async () => {
    const num = Number.parseInt(numberStr, 10);
    const n = name.trim();
    if (!n || !courseId || Number.isNaN(num) || num < 1) {
      setError("Valid number, name and course are required.");
      return;
    }
    if (!groupId) {
      setError("Group is required for a Semester.");
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
        groupId,
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
                <TableCell>
                  {r.groupId == null ? (
                    <Chip size="small" label="Legacy / Historical" color="warning" variant="outlined" />
                  ) : (
                    r.groupName ?? r.groupId
                  )}
                </TableCell>
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
            {editingLegacy && (
              <Alert severity="warning">
                This is a legacy historical Semester (no Group). It is retained for audit only and is excluded
                from operational academic-tree / scheduling selectors. Select a Group only to convert it
                explicitly — it will not be auto-assigned as a course-wide wildcard.
              </Alert>
            )}
            <TextField
              select
              label="Course"
              value={courseId || ""}
              onChange={(e) => {
                const next = Number(e.target.value);
                setCourseId(next);
                const g0 = groups.find((g) => g.courseId === next)?.id ?? 0;
                setGroupId(g0);
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
              onChange={(e) => setGroupId(Number(e.target.value))}
              fullWidth
              required
              helperText="Group is required. New Semesters cannot be course-wide."
              disabled={!courseId || groupsForCourse.length === 0}
            >
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
          <Button variant="contained" onClick={() => void save()} disabled={saving || !groupId}>
            {saving ? "Saving…" : "Save"}
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default SemestersPage;
