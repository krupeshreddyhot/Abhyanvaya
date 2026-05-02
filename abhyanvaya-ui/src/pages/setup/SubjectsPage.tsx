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
  listTenantSubjects,
  createTenantSubject,
  updateSubject,
  type CourseRow,
  type ElectiveGroupRow,
  type GroupRow,
  type IdName,
  type SemesterRow,
  type SubjectCatalogRow,
  type TenantSubjectRow,
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

/** True if trimmed query is a substring of the subject's name or code (case-insensitive). */
const queryMatchesSelectedSubject = (query: string, subject: TenantSubjectRow | null) => {
  if (!subject) return false;
  const q = query.trim().toLowerCase();
  if (!q) return false;
  if (subject.name.toLowerCase().includes(q)) return true;
  if (subject.code && subject.code.toLowerCase().includes(q)) return true;
  return false;
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

  const [tenantSubjects, setTenantSubjects] = useState<TenantSubjectRow[]>([]);
  const [selectedTenantSubject, setSelectedTenantSubject] = useState<TenantSubjectRow | null>(null);
  const [tenantSubjectId, setTenantSubjectId] = useState(0);
  const [subjectLookupQuery, setSubjectLookupQuery] = useState("");
  const [lookupDialogOpen, setLookupDialogOpen] = useState(false);
  const [newTenantSubjectCode, setNewTenantSubjectCode] = useState("");
  const [newTenantSubjectName, setNewTenantSubjectName] = useState("");
  const [creatingTenantSubject, setCreatingTenantSubject] = useState(false);
  const [courseId, setCourseId] = useState(0);
  const [groupId, setGroupId] = useState(0);
  const [semesterId, setSemesterId] = useState(0);
  const [isElective, setIsElective] = useState(false);
  const [electiveGroupId, setElectiveGroupId] = useState(0);
  const [languageSubjectSlot, setLanguageSubjectSlot] = useState(0);
  const [teachingLanguageId, setTeachingLanguageId] = useState(0);
  const [hpw, setHpw] = useState<string>("");
  const [credits, setCredits] = useState<string>("");
  const [examHours, setExamHours] = useState<string>("");
  const [marks, setMarks] = useState<string>("");

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const [cRes, gRes, sRes, lRes, eRes, subRes, tsRes] = await Promise.all([
        listCourses(),
        listGroups(),
        listSemesters(),
        listLanguages(),
        listElectiveGroups(),
        listSubjectCatalog(),
        listTenantSubjects(),
      ]);
      setCourses(cRes.data);
      setGroups(gRes.data);
      setSemesters(sRes.data);
      setLanguages(lRes.data);
      setElectiveGroups(eRes.data);
      setRows(subRes.data);
      setTenantSubjects(tsRes.data);
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
    const filtered = semesters.filter((s) => {
      if (s.courseId !== courseId) return false;
      if (s.groupId == null) return true;
      return s.groupId === groupId;
    });
    // If the subject's semester is tied to a different group in master data, the filter
    // can drop it and MUI Select shows blank. Always include the currently selected semester.
    if (semesterId > 0) {
      const current = semesters.find((s) => s.id === semesterId);
      if (current && !filtered.some((s) => s.id === current.id)) {
        return [...filtered, current].sort(
          (a, b) => a.number - b.number || a.id - b.id,
        );
      }
    }
    return filtered;
  }, [semesters, courseId, groupId, semesterId]);

  const electivesForContext = useMemo(() => {
    return electiveGroups.filter(
      (e) => e.courseId === courseId && e.semesterId === semesterId && e.groupId === groupId,
    );
  }, [electiveGroups, courseId, semesterId, groupId]);

  const openAdd = () => {
    setEditingId(0);
    setTenantSubjectId(0);
    setSelectedTenantSubject(null);
    setSubjectLookupQuery("");
    setNewTenantSubjectCode("");
    setNewTenantSubjectName("");
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
    setHpw("");
    setCredits("");
    setExamHours("");
    setMarks("");
    setDialogOpen(true);
  };

  const openEdit = (r: SubjectCatalogRow) => {
    setEditingId(r.id);
    setTenantSubjectId(r.tenantSubjectId);
    setSelectedTenantSubject({ id: r.tenantSubjectId, name: r.name, code: r.code });
    setSubjectLookupQuery(r.name);
    setNewTenantSubjectCode("");
    setNewTenantSubjectName("");
    setCourseId(r.courseId);
    setGroupId(r.groupId);
    setSemesterId(r.semesterId);
    setIsElective(r.isElective);
    setElectiveGroupId(r.electiveGroupId ?? 0);
    setLanguageSubjectSlot(r.languageSubjectSlot);
    setTeachingLanguageId(r.teachingLanguageId ?? languages[0]?.id ?? 0);
    setHpw(r.hpw == null ? "" : String(r.hpw));
    setCredits(r.credits == null ? "" : String(r.credits));
    setExamHours(r.examHours == null ? "" : String(r.examHours));
    setMarks(r.marks == null ? "" : String(r.marks));
    setDialogOpen(true);
  };

  useEffect(() => {
    if (!dialogOpen) return;
    if (editingId) return;
    const timer = window.setTimeout(async () => {
      try {
        const res = await listTenantSubjects(subjectLookupQuery.trim() || undefined);
        setTenantSubjects(res.data);
        if (tenantSubjectId > 0 && !selectedTenantSubject) {
          const selected = res.data.find((x) => x.id === tenantSubjectId) ?? null;
          setSelectedTenantSubject(selected);
        }
      } catch {
        // no-op: keep previous list and show submit-time errors.
      }
    }, 250);
    return () => window.clearTimeout(timer);
  }, [subjectLookupQuery, dialogOpen, tenantSubjectId, selectedTenantSubject, editingId]);

  const selectTenantSubject = (subject: TenantSubjectRow) => {
    setTenantSubjectId(subject.id);
    setSelectedTenantSubject(subject);
    setSubjectLookupQuery(subject.name);
    setError(null);
  };

  const addTenantSubject = async () => {
    const name = newTenantSubjectName.trim();
    const code = newTenantSubjectCode.trim().toUpperCase();
    if (!name) {
      setError("Subject name is required to add a tenant subject.");
      return;
    }
    setCreatingTenantSubject(true);
    setError(null);
    try {
      const res = await createTenantSubject({ name, code: code || null });
      setTenantSubjects((prev) => [res.data, ...prev.filter((x) => x.id !== res.data.id)]);
      setTenantSubjectId(res.data.id);
      setSelectedTenantSubject(res.data);
      setSubjectLookupQuery(res.data.name);
      setNewTenantSubjectCode("");
      setNewTenantSubjectName("");
      setLookupDialogOpen(false);
      setMessage("Subject lookup created.");
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setCreatingTenantSubject(false);
    }
  };

  const save = async () => {
    if (!tenantSubjectId || !courseId || !groupId || !semesterId) {
      setError("Subject, course, group and semester are required.");
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
    const hpwValue = hpw.trim() === "" ? null : Number(hpw);
    const creditsValue = credits.trim() === "" ? null : Number(credits);
    const examHoursValue = examHours.trim() === "" ? null : Number(examHours);
    const marksValue = marks.trim() === "" ? null : Number(marks);

    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      const base = {
        tenantSubjectId,
        courseId,
        groupId,
        semesterId,
        isElective,
        electiveGroupId: isElective ? electiveGroupId : null,
        languageSubjectSlot,
        teachingLanguageId: teachingId,
        hpw: Number.isNaN(hpwValue) ? null : hpwValue,
        credits: Number.isNaN(creditsValue) ? null : creditsValue,
        examHours: Number.isNaN(examHoursValue) ? null : examHoursValue,
        marks: Number.isNaN(marksValue) ? null : marksValue,
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
                  <TableCell>{r.code ?? "—"}</TableCell>
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

      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} fullScreen>
        <DialogTitle>{editingId ? "Edit subject" : "Add subject"}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {!editingId && (
              <>
                <TextField
                  label="Search subject by name or code"
                  value={subjectLookupQuery}
                  onChange={(e) => {
                    const value = e.target.value;
                    setSubjectLookupQuery(value);
                    if (selectedTenantSubject && !queryMatchesSelectedSubject(value, selectedTenantSubject)) {
                      setSelectedTenantSubject(null);
                      setTenantSubjectId(0);
                    }
                  }}
                  fullWidth
                  helperText="Matches subject name or subject code (partial search)."
                />
                {subjectLookupQuery.trim() && tenantSubjects.length > 0 && (
                  <Stack spacing={1}>
                    {tenantSubjects.map((subject) => (
                      <Button
                        key={subject.id}
                        variant={subject.id === tenantSubjectId ? "contained" : "outlined"}
                        onClick={() => selectTenantSubject(subject)}
                        sx={{
                          justifyContent: "flex-start",
                          alignItems: "flex-start",
                          flexDirection: "column",
                          textAlign: "left",
                          py: 1.25,
                        }}
                      >
                        <Typography variant="body1" component="span" sx={{ fontWeight: 600 }}>
                          {subject.name}
                        </Typography>
                        <Typography variant="body2" color="text.secondary" component="span">
                          {subject.code ? `Code: ${subject.code}` : "No code"}
                        </Typography>
                      </Button>
                    ))}
                  </Stack>
                )}
                {subjectLookupQuery.trim() && tenantSubjects.length === 0 && (
                  <Button
                    variant="outlined"
                    onClick={() => {
                      setNewTenantSubjectName(subjectLookupQuery.trim());
                      setLookupDialogOpen(true);
                    }}
                  >
                    Add subject
                  </Button>
                )}
              </>
            )}
            <TextField label="Subject name" value={selectedTenantSubject?.name ?? ""} fullWidth disabled required />
            <TextField label="Subject code" value={selectedTenantSubject?.code ?? ""} fullWidth disabled />
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
            <TextField label="HPW" type="number" value={hpw} onChange={(e) => setHpw(e.target.value)} fullWidth />
            <TextField
              label="Credits"
              type="number"
              value={credits}
              onChange={(e) => setCredits(e.target.value)}
              fullWidth
            />
            <TextField
              label="Exam Hours"
              type="number"
              value={examHours}
              onChange={(e) => setExamHours(e.target.value)}
              fullWidth
            />
            <TextField
              label="Marks"
              type="number"
              value={marks}
              onChange={(e) => setMarks(e.target.value)}
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

      <Dialog open={lookupDialogOpen} onClose={() => !creatingTenantSubject && setLookupDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>Add subject lookup</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              label="Subject name"
              value={newTenantSubjectName}
              onChange={(e) => setNewTenantSubjectName(e.target.value)}
              fullWidth
              required
            />
            <TextField
              label="Subject code (optional)"
              value={newTenantSubjectCode}
              onChange={(e) => setNewTenantSubjectCode(e.target.value.toUpperCase())}
              fullWidth
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setLookupDialogOpen(false)} disabled={creatingTenantSubject}>
            Cancel
          </Button>
          <Button variant="contained" onClick={() => void addTenantSubject()} disabled={creatingTenantSubject}>
            {creatingTenantSubject ? "Saving..." : "Save"}
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default SubjectsPage;
