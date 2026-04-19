import { useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
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
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import useMediaQuery from "@mui/material/useMediaQuery";
import { useTheme } from "@mui/material/styles";
import {
  createStudent,
  exportStudentsCsv,
  getCourses,
  getGenders,
  getGroups,
  getLanguages,
  getMediums,
  getSemesters,
  getStudents,
  type GroupOptionDto,
  type MasterOptionDto,
  type StudentRecordDto,
  type StudentUpsertPayload,
  updateStudent,
} from "../services/studentsService";

type StudentForm = {
  id: number;
  appraId: string;
  studentNumber: string;
  name: string;
  courseId: number;
  groupId: number;
  semesterId: number;
  genderId: number;
  mediumId: number;
  firstLanguageId: number;
  languageId: number;
  batch: string;
  dateOfBirth: string;
  mobileNumber: string;
  alternateMobileNumber: string;
  email: string;
  parentMobileNumber: string;
  parentAlternateMobileNumber: string;
  fatherName: string;
  motherName: string;
};

const emptyForm: StudentForm = {
  id: 0,
  appraId: "",
  studentNumber: "",
  name: "",
  courseId: 0,
  groupId: 0,
  semesterId: 0,
  genderId: 0,
  mediumId: 0,
  firstLanguageId: 0,
  languageId: 0,
  batch: "",
  dateOfBirth: "",
  mobileNumber: "",
  alternateMobileNumber: "",
  email: "",
  parentMobileNumber: "",
  parentAlternateMobileNumber: "",
  fatherName: "",
  motherName: "",
};

const StudentsPage = () => {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));

  const [students, setStudents] = useState<StudentRecordDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(30);
  const [courses, setCourses] = useState<MasterOptionDto[]>([]);
  const [groups, setGroups] = useState<GroupOptionDto[]>([]);
  const [semesters, setSemesters] = useState<MasterOptionDto[]>([]);
  const [genders, setGenders] = useState<MasterOptionDto[]>([]);
  const [mediums, setMediums] = useState<MasterOptionDto[]>([]);
  const [languages, setLanguages] = useState<MasterOptionDto[]>([]);

  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [batchFilter, setBatchFilter] = useState("");
  const [courseFilter, setCourseFilter] = useState(0);
  const [groupFilter, setGroupFilter] = useState(0);
  const [semesterFilter, setSemesterFilter] = useState(0);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [isEdit, setIsEdit] = useState(false);
  const [form, setForm] = useState<StudentForm>(emptyForm);

  useEffect(() => {
    const t = setTimeout(() => setSearch(searchInput.trim()), 300);
    return () => clearTimeout(t);
  }, [searchInput]);

  const loadStudents = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await getStudents({
        search: search || undefined,
        batch: batchFilter.trim() ? Number.parseInt(batchFilter, 10) || undefined : undefined,
        courseId: courseFilter > 0 ? courseFilter : undefined,
        groupId: groupFilter > 0 ? groupFilter : undefined,
        semesterId: semesterFilter > 0 ? semesterFilter : undefined,
        pageNumber,
        pageSize,
      });
      setStudents(res.data.data);
      setTotalCount(res.data.totalCount);
    } catch {
      setError("Failed to load students.");
    } finally {
      setLoading(false);
    }
  };

  const loadMasters = async () => {
    try {
      const [cRes, grpRes, sRes, gRes, mRes, lRes] = await Promise.all([
        getCourses(),
        getGroups(),
        getSemesters(),
        getGenders(),
        getMediums(),
        getLanguages(),
      ]);
      setCourses(cRes.data);
      setGroups(grpRes.data);
      setSemesters(sRes.data);
      setGenders(gRes.data);
      setMediums(mRes.data);
      setLanguages(lRes.data);
    } catch {
      setError("Failed to load master data.");
    }
  };

  useEffect(() => {
    void loadMasters();
  }, []);

  useEffect(() => {
    setPageNumber(1);
  }, [search, batchFilter, courseFilter, groupFilter, semesterFilter]);

  useEffect(() => {
    void loadStudents();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search, batchFilter, courseFilter, groupFilter, semesterFilter, pageNumber, pageSize]);

  const groupOptions = useMemo(() => groups.filter((g) => g.courseId === form.courseId), [groups, form.courseId]);

  const openAdd = () => {
    setIsEdit(false);
    setForm(emptyForm);
    setDialogOpen(true);
  };

  const openEdit = (s: StudentRecordDto) => {
    setIsEdit(true);
    setForm({
      id: s.id,
      appraId: s.appraId ?? "",
      studentNumber: s.studentNumber,
      name: s.name,
      courseId: s.courseId,
      groupId: s.groupId,
      semesterId: s.semesterId,
      genderId: s.genderId,
      mediumId: s.mediumId,
      firstLanguageId: s.firstLanguageId,
      languageId: s.languageId,
      batch: s.batch?.toString() ?? "",
      dateOfBirth: s.dateOfBirth ? s.dateOfBirth.slice(0, 10) : "",
      mobileNumber: s.mobileNumber ?? "",
      alternateMobileNumber: s.alternateMobileNumber ?? "",
      email: s.email ?? "",
      parentMobileNumber: s.parentMobileNumber ?? "",
      parentAlternateMobileNumber: s.parentAlternateMobileNumber ?? "",
      fatherName: s.fatherName ?? "",
      motherName: s.motherName ?? "",
    });
    setDialogOpen(true);
  };

  const validate = () => {
    if (!form.studentNumber.trim()) return "Student Number is required.";
    if (!form.name.trim()) return "Student name is required.";
    if (!form.courseId || !form.groupId || !form.semesterId) return "Course, group and semester are required.";
    if (!form.genderId || !form.mediumId || !form.languageId)
      return "Gender, medium and second language are required.";
    if (isEdit && !form.firstLanguageId) return "First language is required.";
    return null;
  };

  const toPayload = (): StudentUpsertPayload => ({
    appraId: form.appraId.trim() || undefined,
    studentNumber: form.studentNumber.trim(),
    name: form.name.trim(),
    courseId: form.courseId,
    groupId: form.groupId,
    semesterId: form.semesterId,
    genderId: form.genderId,
    mediumId: form.mediumId,
    ...(form.firstLanguageId > 0 ? { firstLanguageId: form.firstLanguageId } : {}),
    languageId: form.languageId,
    batch: form.batch.trim() ? Number.parseInt(form.batch, 10) || null : null,
    dateOfBirth: form.dateOfBirth || null,
    mobileNumber: form.mobileNumber.trim() || undefined,
    alternateMobileNumber: form.alternateMobileNumber.trim() || undefined,
    email: form.email.trim() || undefined,
    parentMobileNumber: form.parentMobileNumber.trim() || undefined,
    parentAlternateMobileNumber: form.parentAlternateMobileNumber.trim() || undefined,
    fatherName: form.fatherName.trim() || undefined,
    motherName: form.motherName.trim() || undefined,
  });

  const saveStudent = async () => {
    const v = validate();
    if (v) {
      setError(v);
      return;
    }

    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      const payload = toPayload();
      if (isEdit) {
        await updateStudent({
          ...payload,
          id: form.id,
          firstLanguageId: form.firstLanguageId,
        });
        setMessage("Student updated successfully.");
      } else {
        await createStudent(payload);
        setMessage("Student created successfully.");
      }
      setDialogOpen(false);
      await loadStudents();
    } catch {
      setError(isEdit ? "Failed to update student." : "Failed to create student.");
    } finally {
      setSaving(false);
    }
  };

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  const activeFilterChips = [
    batchFilter.trim() ? `Batch: ${batchFilter.trim()}` : null,
    courseFilter > 0 ? `Course: ${courses.find((c) => c.id === courseFilter)?.name ?? courseFilter}` : null,
    groupFilter > 0 ? `Group: ${groups.find((g) => g.id === groupFilter)?.name ?? groupFilter}` : null,
    semesterFilter > 0
      ? `Semester: ${semesters.find((s) => s.id === semesterFilter)?.name ?? semesterFilter}`
      : null,
  ].filter((x): x is string => !!x);

  const onExport = async () => {
    try {
      const res = await exportStudentsCsv({
        search: search || undefined,
        batch: batchFilter.trim() ? Number.parseInt(batchFilter, 10) || undefined : undefined,
        courseId: courseFilter > 0 ? courseFilter : undefined,
        groupId: groupFilter > 0 ? groupFilter : undefined,
        semesterId: semesterFilter > 0 ? semesterFilter : undefined,
      });
      const blob = new Blob([res.data], { type: "text/csv;charset=utf-8;" });
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `students-${new Date().toISOString().slice(0, 10)}.csv`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
    } catch {
      setError("Failed to export students.");
    }
  };

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 1 }}>
        <Typography variant={isMobile ? "h5" : "h4"}>Students</Typography>
        <Box sx={{ display: "flex", gap: 1 }}>
          <Button variant="outlined" onClick={onExport}>
            Export CSV
          </Button>
          <Button variant="contained" onClick={openAdd}>
            Add Student
          </Button>
        </Box>
      </Box>

      {message && <Alert severity="success">{message}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}

      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", md: "1fr 1fr 1fr 1fr auto" },
          gap: 1,
          alignItems: "center",
        }}
      >
        <TextField
          label="Batch"
          type="number"
          value={batchFilter}
          onChange={(e) => setBatchFilter(e.target.value)}
          size="small"
          fullWidth
        />
        <TextField
          select
          label="Course"
          value={courseFilter}
          onChange={(e) => {
            const nextCourse = Number(e.target.value);
            setCourseFilter(nextCourse);
            setGroupFilter(0);
          }}
          size="small"
          fullWidth
        >
          <MenuItem value={0}>All courses</MenuItem>
          {courses.map((x) => (
            <MenuItem key={x.id} value={x.id}>
              {x.name}
            </MenuItem>
          ))}
        </TextField>
        <TextField
          select
          label="Group"
          value={groupFilter}
          onChange={(e) => setGroupFilter(Number(e.target.value))}
          size="small"
          fullWidth
        >
          <MenuItem value={0}>All groups</MenuItem>
          {groups
            .filter((x) => (courseFilter > 0 ? x.courseId === courseFilter : true))
            .map((x) => (
            <MenuItem key={x.id} value={x.id}>
              {x.name}
            </MenuItem>
            ))}
        </TextField>
        <TextField
          select
          label="Semester"
          value={semesterFilter}
          onChange={(e) => setSemesterFilter(Number(e.target.value))}
          size="small"
          fullWidth
        >
          <MenuItem value={0}>All semesters</MenuItem>
          {semesters.map((x) => (
            <MenuItem key={x.id} value={x.id}>
              {x.name}
            </MenuItem>
          ))}
        </TextField>
        <Button
          variant="text"
          onClick={() => {
            setBatchFilter("");
            setCourseFilter(0);
            setGroupFilter(0);
            setSemesterFilter(0);
          }}
        >
          Clear
        </Button>
      </Box>

      {activeFilterChips.length > 0 && (
        <Box sx={{ display: "flex", flexWrap: "wrap", gap: 1 }}>
          {activeFilterChips.map((label) => (
            <Chip key={label} label={label} size="small" variant="outlined" />
          ))}
        </Box>
      )}

      <TextField
        label="Search by student no / name / mobile"
        value={searchInput}
        onChange={(e) => setSearchInput(e.target.value)}
        fullWidth
      />

      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 1, flexWrap: "wrap" }}>
        <Typography variant="body2" color="text.secondary">
          Total: {totalCount}
        </Typography>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <TextField
            select
            size="small"
            label="Rows"
            value={pageSize}
            onChange={(e) => {
              setPageSize(Number(e.target.value));
              setPageNumber(1);
            }}
            sx={{ minWidth: 100 }}
          >
            {[10, 20, 30, 50, 100].map((x) => (
              <MenuItem key={x} value={x}>
                {x}
              </MenuItem>
            ))}
          </TextField>
          <Button size="small" onClick={() => setPageNumber((p) => Math.max(1, p - 1))} disabled={pageNumber <= 1 || loading}>
            Prev
          </Button>
          <Typography variant="body2">
            {pageNumber} / {totalPages}
          </Typography>
          <Button
            size="small"
            onClick={() => setPageNumber((p) => Math.min(totalPages, p + 1))}
            disabled={pageNumber >= totalPages || loading}
          >
            Next
          </Button>
        </Box>
      </Box>

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", p: 3 }}>
          <CircularProgress />
        </Box>
      ) : isMobile ? (
        <Stack spacing={1.5}>
          {students.map((s) => (
            <Card key={s.id} variant="outlined">
              <CardContent sx={{ pb: "16px !important" }}>
                <Stack spacing={0.5}>
                  <Typography variant="subtitle2">{s.name}</Typography>
                  <Typography variant="body2">No: {s.studentNumber}</Typography>
                  <Typography variant="body2">
                    {s.courseName} / {s.groupName} / {s.semesterName}
                  </Typography>
                  <Typography variant="body2">Mobile: {s.mobileNumber || "-"}</Typography>
                  <Button size="small" sx={{ alignSelf: "flex-start", mt: 0.5 }} onClick={() => openEdit(s)}>
                    Edit
                  </Button>
                </Stack>
              </CardContent>
            </Card>
          ))}
        </Stack>
      ) : (
        <TableContainer component={Card} variant="outlined">
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Student No</TableCell>
                <TableCell>Name</TableCell>
                <TableCell>Course</TableCell>
                <TableCell>Group</TableCell>
                <TableCell>Semester</TableCell>
                <TableCell>Batch</TableCell>
                <TableCell>Mobile</TableCell>
                <TableCell>Email</TableCell>
                <TableCell align="right">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {students.map((s) => (
                <TableRow key={s.id} hover>
                  <TableCell>{s.studentNumber}</TableCell>
                  <TableCell>{s.name}</TableCell>
                  <TableCell>{s.courseName}</TableCell>
                  <TableCell>{s.groupName}</TableCell>
                  <TableCell>{s.semesterName}</TableCell>
                  <TableCell>{s.batch ?? "-"}</TableCell>
                  <TableCell>{s.mobileNumber || "-"}</TableCell>
                  <TableCell>{s.email || "-"}</TableCell>
                  <TableCell align="right">
                    <Button size="small" onClick={() => openEdit(s)}>
                      Edit
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} fullScreen={isMobile} maxWidth="md" fullWidth>
        <DialogTitle>{isEdit ? "Edit Student" : "Add Student"}</DialogTitle>
        <DialogContent dividers>
          <Stack spacing={2} sx={{ mt: 0.5 }}>
            <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "1fr 1fr" }, gap: 2 }}>
              <TextField
                label="Student Number"
                value={form.studentNumber}
                onChange={(e) => setForm((f) => ({ ...f, studentNumber: e.target.value }))}
                fullWidth
                disabled={isEdit}
              />
              <TextField
                label="Appra Id"
                value={form.appraId}
                onChange={(e) => setForm((f) => ({ ...f, appraId: e.target.value }))}
                fullWidth
              />
              <TextField
                label="Student Name"
                value={form.name}
                onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
                fullWidth
              />
              <TextField
                label="Batch"
                type="number"
                value={form.batch}
                onChange={(e) => setForm((f) => ({ ...f, batch: e.target.value }))}
                fullWidth
              />
              <TextField
                select
                label="Course"
                value={form.courseId}
                onChange={(e) =>
                  setForm((f) => ({ ...f, courseId: Number(e.target.value), groupId: 0 }))
                }
                fullWidth
              >
                <MenuItem value={0}>Select course</MenuItem>
                {courses.map((x) => (
                  <MenuItem key={x.id} value={x.id}>
                    {x.name}
                  </MenuItem>
                ))}
              </TextField>
              <TextField
                select
                label="Group"
                value={form.groupId}
                onChange={(e) => setForm((f) => ({ ...f, groupId: Number(e.target.value) }))}
                fullWidth
              >
                <MenuItem value={0}>Select group</MenuItem>
                {groupOptions.map((x) => (
                  <MenuItem key={x.id} value={x.id}>
                    {x.name}
                  </MenuItem>
                ))}
              </TextField>
              <TextField
                select
                label="Semester"
                value={form.semesterId}
                onChange={(e) => setForm((f) => ({ ...f, semesterId: Number(e.target.value) }))}
                fullWidth
              >
                <MenuItem value={0}>Select semester</MenuItem>
                {semesters.map((x) => (
                  <MenuItem key={x.id} value={x.id}>
                    {x.name}
                  </MenuItem>
                ))}
              </TextField>
              <TextField
                select
                label="Gender"
                value={form.genderId}
                onChange={(e) => setForm((f) => ({ ...f, genderId: Number(e.target.value) }))}
                fullWidth
              >
                <MenuItem value={0}>Select gender</MenuItem>
                {genders.map((x) => (
                  <MenuItem key={x.id} value={x.id}>
                    {x.name}
                  </MenuItem>
                ))}
              </TextField>
              <TextField
                select
                label="Medium"
                value={form.mediumId}
                onChange={(e) => setForm((f) => ({ ...f, mediumId: Number(e.target.value) }))}
                fullWidth
              >
                <MenuItem value={0}>Select medium</MenuItem>
                {mediums.map((x) => (
                  <MenuItem key={x.id} value={x.id}>
                    {x.name}
                  </MenuItem>
                ))}
              </TextField>
              <TextField
                select
                label={isEdit ? "First language" : "First language (optional)"}
                value={form.firstLanguageId}
                onChange={(e) => setForm((f) => ({ ...f, firstLanguageId: Number(e.target.value) }))}
                fullWidth
                helperText={
                  isEdit ? undefined : "Leave unselected to default to English for your institution."
                }
              >
                <MenuItem value={0}>{isEdit ? "Select first language" : "Default (English)"}</MenuItem>
                {languages.map((x) => (
                  <MenuItem key={x.id} value={x.id}>
                    {x.name}
                  </MenuItem>
                ))}
              </TextField>
              <TextField
                select
                label="Second language"
                value={form.languageId}
                onChange={(e) => setForm((f) => ({ ...f, languageId: Number(e.target.value) }))}
                fullWidth
              >
                <MenuItem value={0}>Select second language</MenuItem>
                {languages.map((x) => (
                  <MenuItem key={x.id} value={x.id}>
                    {x.name}
                  </MenuItem>
                ))}
              </TextField>
              <TextField
                label="Date of Birth"
                type="date"
                value={form.dateOfBirth}
                onChange={(e) => setForm((f) => ({ ...f, dateOfBirth: e.target.value }))}
                slotProps={{ inputLabel: { shrink: true } }}
                fullWidth
              />
              <TextField
                label="Mobile Number"
                value={form.mobileNumber}
                onChange={(e) => setForm((f) => ({ ...f, mobileNumber: e.target.value }))}
                fullWidth
              />
              <TextField
                label="Alternate Mobile Number"
                value={form.alternateMobileNumber}
                onChange={(e) => setForm((f) => ({ ...f, alternateMobileNumber: e.target.value }))}
                fullWidth
              />
              <TextField
                label="Email"
                value={form.email}
                onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))}
                fullWidth
              />
              <TextField
                label="Parent Mobile Number"
                value={form.parentMobileNumber}
                onChange={(e) => setForm((f) => ({ ...f, parentMobileNumber: e.target.value }))}
                fullWidth
              />
              <TextField
                label="Parent Alternate Mobile Number"
                value={form.parentAlternateMobileNumber}
                onChange={(e) => setForm((f) => ({ ...f, parentAlternateMobileNumber: e.target.value }))}
                fullWidth
              />
              <TextField
                label="Father Name"
                value={form.fatherName}
                onChange={(e) => setForm((f) => ({ ...f, fatherName: e.target.value }))}
                fullWidth
              />
              <TextField
                label="Mother Name"
                value={form.motherName}
                onChange={(e) => setForm((f) => ({ ...f, motherName: e.target.value }))}
                fullWidth
              />
            </Box>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)} disabled={saving}>
            Cancel
          </Button>
          <Button variant="contained" onClick={saveStudent} disabled={saving}>
            {saving ? "Saving..." : isEdit ? "Update Student" : "Create Student"}
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default StudentsPage;

