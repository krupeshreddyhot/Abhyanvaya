import { useEffect, useMemo, useState } from "react";
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
  MenuItem,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import {
  createSubject,
  listCourses,
  listElectiveGroups,
  listGroups,
  listLanguages,
  listSemesters,
  listSubjectCatalog,
  updateSubject,
  type CourseRow,
  type ElectiveGroupRow,
  type GroupRow,
  type IdName,
  type SemesterRow,
  type SubjectCatalogRow,
} from "../../services/setupService";

const errMsg = (e: unknown): string => {
  const d = (e as { response?: { data?: unknown } }).response?.data;
  if (typeof d === "string") return d;
  return "Request failed.";
};

const SLOT_LABELS: Record<number, string> = {
  0: "Not a language period",
  1: "First language",
  2: "Second language",
};

const SubjectsPage = () => {
  const [courses, setCourses] = useState<CourseRow[]>([]);
  const [groups, setGroups] = useState<GroupRow[]>([]);
  const [semesters, setSemesters] = useState<SemesterRow[]>([]);
  const [languages, setLanguages] = useState<IdName[]>([]);
  const [electiveGroups, setElectiveGroups] = useState<ElectiveGroupRow[]>([]);
  const [rows, setRows] = useState<SubjectCatalogRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [editingId, setEditingId] = useState(0);

  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [courseId, setCourseId] = useState(0);
  const [groupId, setGroupId] = useState(0);
  const [semesterId, setSemesterId] = useState(0);
  const [isElective, setIsElective] = useState(false);
  const [electiveGroupId, setElectiveGroupId] = useState(0);
  const [languageSubjectSlot, setLanguageSubjectSlot] = useState(0);
  const [teachingLanguageId, setTeachingLanguageId] = useState(0);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const [cRes, gRes, sRes, lRes, eRes, subRes] = await Promise.all([
        listCourses(),
        listGroups(),
        listSemesters(),
        listLanguages(),
        listElectiveGroups(),
        listSubjectCatalog(),
      ]);
      setCourses(cRes.data);
      setGroups(gRes.data);
      setSemesters(sRes.data);
      setLanguages(lRes.data);
      setElectiveGroups(eRes.data);
      setRows(subRes.data);
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

  const electivesForContext = useMemo(() => {
    return electiveGroups.filter(
      (e) => e.courseId === courseId && e.semesterId === semesterId && e.groupId === groupId,
    );
  }, [electiveGroups, courseId, semesterId, groupId]);

  const openAdd = () => {
    setEditingId(0);
    setCode("");
    setName("");
    const c0 = courses[0]?.id ?? 0;
    setCourseId(c0);
    const g0 = groups.find((x) => x.courseId === c0)?.id ?? 0;
    setGroupId(g0);
    const s0 =
      semesters.find((x) => x.courseId === c0 && (x.groupId == null || x.groupId === g0))?.id ?? 0;
    setSemesterId(s0);
    setIsElective(false);
    setElectiveGroupId(0);
    setLanguageSubjectSlot(0);
    setTeachingLanguageId(languages[0]?.id ?? 0);
    setDialogOpen(true);
  };

  const openEdit = (r: SubjectCatalogRow) => {
    setEditingId(r.id);
    setCode(r.code);
    setName(r.name);
    setCourseId(r.courseId);
    setGroupId(r.groupId);
    setSemesterId(r.semesterId);
    setIsElective(r.isElective);
    setElectiveGroupId(r.electiveGroupId ?? 0);
    setLanguageSubjectSlot(r.languageSubjectSlot);
    setTeachingLanguageId(r.teachingLanguageId ?? languages[0]?.id ?? 0);
    setDialogOpen(true);
  };

  const save = async () => {
    const c = code.trim().toUpperCase();
    const n = name.trim();
    if (!c || !n || !courseId || !groupId || !semesterId) {
      setError("Code, name, course, group and semester are required.");
      return;
    }
    if (isElective && !electiveGroupId) {
      setError("Elective group is required for elective subjects.");
      return;
    }
    if ((languageSubjectSlot === 1 || languageSubjectSlot === 2) && !teachingLanguageId) {
      setError("Teaching language is required when a language slot is selected.");
      return;
    }

    const teachingId =
      languageSubjectSlot === 1 || languageSubjectSlot === 2 ? teachingLanguageId : null;

    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      const base = {
        code: c,
        name: n,
        courseId,
        groupId,
        semesterId,
        isElective,
        electiveGroupId: isElective ? electiveGroupId : null,
        languageSubjectSlot,
        teachingLanguageId: teachingId,
      };
      if (editingId) await updateSubject({ id: editingId, ...base });
      else await createSubject(base);
      setMessage(editingId ? "Subject updated." : "Subject created.");
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
          Subjects
        </Typography>
        <Button variant="contained" onClick={openAdd} disabled={!courses.length}>
          Add subject
        </Button>
      </Box>
      {message && <Alert severity="success">{message}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}
      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", p: 3 }}>
          <CircularProgress />
        </Box>
      ) : (
        <TableContainer sx={{ maxWidth: "100%", overflowX: "auto" }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Code</TableCell>
                <TableCell>Name</TableCell>
                <TableCell>Course</TableCell>
                <TableCell>Group</TableCell>
                <TableCell>Semester</TableCell>
                <TableCell>Elective</TableCell>
                <TableCell>Lang. slot</TableCell>
                <TableCell>Teaching lang.</TableCell>
                <TableCell align="right">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {rows.map((r) => (
                <TableRow key={r.id} hover>
                  <TableCell>{r.code}</TableCell>
                  <TableCell>{r.name}</TableCell>
                  <TableCell>{r.courseName}</TableCell>
                  <TableCell>{r.groupName}</TableCell>
                  <TableCell>{r.semesterName}</TableCell>
                  <TableCell>{r.isElective ? "Yes" : "No"}</TableCell>
                  <TableCell>{SLOT_LABELS[r.languageSubjectSlot] ?? r.languageSubjectSlot}</TableCell>
                  <TableCell>{r.teachingLanguageName ?? "—"}</TableCell>
                  <TableCell align="right">
                    <Button size="small" onClick={() => openEdit(r)}>
                      Edit
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} fullWidth maxWidth="md">
        <DialogTitle>{editingId ? "Edit subject" : "Add subject"}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              label="Subject code"
              value={code}
              onChange={(e) => setCode(e.target.value.toUpperCase())}
              fullWidth
              required
            />
            <TextField label="Subject name" value={name} onChange={(e) => setName(e.target.value)} fullWidth required />
            <TextField
              select
              label="Course"
              value={courseId || ""}
              onChange={(e) => {
                const cid = Number(e.target.value);
                setCourseId(cid);
                const g = groups.find((x) => x.courseId === cid);
                setGroupId(g?.id ?? 0);
                const sem = semesters.find(
                  (s) => s.courseId === cid && (s.groupId == null || s.groupId === g?.id),
                );
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
                const sem = semesters.find(
                  (s) => s.courseId === courseId && (s.groupId == null || s.groupId === gid),
                );
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
            <FormControlLabel
              control={<Checkbox checked={isElective} onChange={(e) => setIsElective(e.target.checked)} />}
              label="Elective subject"
            />
            {isElective && (
              <TextField
                select
                label="Elective group"
                value={electiveGroupId || ""}
                onChange={(e) => setElectiveGroupId(Number(e.target.value))}
                fullWidth
                required
                helperText="Define elective groups under Catalog → Elective groups."
              >
                {electivesForContext.map((e) => (
                  <MenuItem key={e.id} value={e.id}>
                    {e.name}
                  </MenuItem>
                ))}
              </TextField>
            )}
            <TextField
              select
              label="Language period"
              value={languageSubjectSlot}
              onChange={(e) => setLanguageSubjectSlot(Number(e.target.value))}
              fullWidth
            >
              {Object.entries(SLOT_LABELS).map(([k, label]) => (
                <MenuItem key={k} value={Number(k)}>
                  {label}
                </MenuItem>
              ))}
            </TextField>
            {(languageSubjectSlot === 1 || languageSubjectSlot === 2) && (
              <TextField
                select
                label="Teaching language"
                value={teachingLanguageId || ""}
                onChange={(e) => setTeachingLanguageId(Number(e.target.value))}
                fullWidth
                required
              >
                {languages.map((l) => (
                  <MenuItem key={l.id} value={l.id}>
                    {l.name}
                  </MenuItem>
                ))}
              </TextField>
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

export default SubjectsPage;
