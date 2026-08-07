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
  Tab,
  Tabs,
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
import { listAcademicYears, type AcademicYearDto } from "../../services/schedulingService";
import { listGroups, listMasterCourses, listSemesters, type CourseRow, type GroupRow, type SemesterRow } from "../../services/setupService";
import {
  assignFacultySection,
  assignStudentSection,
  autoAllocateSections,
  createSection,
  deleteSection,
  listFacultySections,
  listSections,
  listStudentSections,
  transferStudentSection,
  updateSection,
  type FacultySectionDto,
  type SectionDto,
  type StudentSectionDto,
} from "../../services/sectionService";

const errMsg = (e: unknown): string => {
  const d = (e as { response?: { data?: unknown } }).response?.data;
  if (typeof d === "string") return d;
  return "Request failed.";
};

const SectionsPage = () => {
  const { hasPermission } = useAuth();
  const canCreate = hasPermission(PermissionKeys.SectionCreate);
  const canEdit = hasPermission(PermissionKeys.SectionEdit);
  const canDelete = hasPermission(PermissionKeys.SectionDelete);
  const canAssignStudents = hasPermission(PermissionKeys.SectionAssignStudents);
  const canAssignFaculty = hasPermission(PermissionKeys.SectionAssignFaculty);

  const [tab, setTab] = useState(0);
  const [years, setYears] = useState<AcademicYearDto[]>([]);
  const [courses, setCourses] = useState<CourseRow[]>([]);
  const [groups, setGroups] = useState<GroupRow[]>([]);
  const [semesters, setSemesters] = useState<SemesterRow[]>([]);
  const [rows, setRows] = useState<SectionDto[]>([]);
  const [studentRows, setStudentRows] = useState<StudentSectionDto[]>([]);
  const [facultyRows, setFacultyRows] = useState<FacultySectionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const [yearId, setYearId] = useState(0);
  const [courseId, setCourseId] = useState(0);
  const [groupId, setGroupId] = useState(0);
  const [semesterId, setSemesterId] = useState(0);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [editingId, setEditingId] = useState(0);
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [maxStrength, setMaxStrength] = useState(60);
  const [displayOrder, setDisplayOrder] = useState(0);

  const [assignStudentId, setAssignStudentId] = useState("");
  const [assignSectionId, setAssignSectionId] = useState(0);
  const [transferStudentId, setTransferStudentId] = useState("");
  const [transferSectionId, setTransferSectionId] = useState(0);
  const [transferReason, setTransferReason] = useState("");
  const [facultyId, setFacultyId] = useState("");
  const [facultySectionId, setFacultySectionId] = useState(0);
  const [facultyRole, setFacultyRole] = useState("Primary");
  const [strategy, setStrategy] = useState("Alphabetical");

  const filteredGroups = useMemo(() => groups.filter((g) => !courseId || g.courseId === courseId), [groups, courseId]);
  const filteredSemesters = useMemo(
    () => semesters.filter((s) => (!courseId || s.courseId === courseId) && (!groupId || !s.groupId || s.groupId === groupId)),
    [semesters, courseId, groupId],
  );

  const loadMasters = async () => {
    const [y, c, g, s] = await Promise.all([listAcademicYears(), listMasterCourses(), listGroups(), listSemesters()]);
    setYears(y.data);
    setCourses(c.data);
    setGroups(g.data);
    setSemesters(s.data);
    const current = y.data.find((x) => x.isCurrent) ?? y.data[0];
    if (current) setYearId(current.id);
    if (c.data[0]) setCourseId(c.data[0].id);
  };

  const loadSections = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await listSections({
        academicYearId: yearId || undefined,
        courseId: courseId || undefined,
        groupId: groupId || undefined,
        semesterId: semesterId || undefined,
      });
      setRows(res.data);
      if (res.data[0] && !assignSectionId) setAssignSectionId(res.data[0].id);
      if (res.data[0] && !facultySectionId) setFacultySectionId(res.data[0].id);
      if (res.data[0] && !transferSectionId) setTransferSectionId(res.data[0].id);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  };

  const loadAllocations = async () => {
    try {
      const [st, fa] = await Promise.all([
        listStudentSections({ sectionId: assignSectionId || undefined, currentOnly: true }),
        listFacultySections({ sectionId: facultySectionId || undefined, currentOnly: true }),
      ]);
      setStudentRows(st.data);
      setFacultyRows(fa.data);
    } catch (e) {
      setError(errMsg(e));
    }
  };

  useEffect(() => {
    void (async () => {
      try {
        await loadMasters();
      } catch (e) {
        setError(errMsg(e));
      }
    })();
  }, []);

  useEffect(() => {
    void loadSections();
  }, [yearId, courseId, groupId, semesterId]);

  useEffect(() => {
    if (tab === 1 || tab === 2 || tab === 3) void loadAllocations();
  }, [tab, assignSectionId, facultySectionId]);

  const openAdd = () => {
    setEditingId(0);
    setCode("");
    setName("");
    setMaxStrength(60);
    setDisplayOrder(rows.length);
    setDialogOpen(true);
  };

  const openEdit = (r: SectionDto) => {
    setEditingId(r.id);
    setCode(r.sectionCode);
    setName(r.sectionName);
    setMaxStrength(r.maximumStrength);
    setDisplayOrder(r.displayOrder);
    setDialogOpen(true);
  };

  const save = async () => {
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      if (editingId) {
        await updateSection(editingId, {
          sectionCode: code,
          sectionName: name,
          displayOrder,
          maximumStrength: maxStrength,
          status: "Active",
        });
        setMessage("Section updated.");
      } else {
        await createSection({
          academicYearId: yearId,
          courseId,
          groupId,
          semesterId,
          sectionCode: code,
          sectionName: name,
          displayOrder,
          maximumStrength: maxStrength,
          status: "Active",
        });
        setMessage("Section created.");
      }
      setDialogOpen(false);
      await loadSections();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  };

  const remove = async (id: number) => {
    if (!window.confirm("Soft-delete this section?")) return;
    try {
      await deleteSection(id);
      setMessage("Section deleted.");
      await loadSections();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const doAssignStudent = async () => {
    try {
      await assignStudentSection({ studentId: Number(assignStudentId), sectionId: assignSectionId });
      setMessage("Student assigned.");
      await loadAllocations();
      await loadSections();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const doTransfer = async () => {
    try {
      await transferStudentSection({
        studentId: Number(transferStudentId),
        targetSectionId: transferSectionId,
        reason: transferReason || undefined,
      });
      setMessage("Student transferred (history preserved).");
      await loadAllocations();
      await loadSections();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const doAssignFaculty = async () => {
    try {
      await assignFacultySection({
        facultyId: Number(facultyId),
        sectionId: facultySectionId,
        academicYearId: yearId,
        role: facultyRole,
      });
      setMessage("Faculty assigned.");
      await loadAllocations();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const doAutoAllocate = async () => {
    try {
      const res = await autoAllocateSections({
        academicYearId: yearId,
        courseId,
        groupId,
        semesterId,
        strategy,
      });
      setMessage(res.data.messages.join(" "));
      await loadSections();
      await loadAllocations();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  return (
    <Box sx={{ p: 2, maxWidth: 1200, mx: "auto" }}>
      <Button component={RouterLink} to="/setup" startIcon={<ArrowBackIcon />} sx={{ mb: 1 }}>
        Academic Setup
      </Button>
      <Typography variant="h5" sx={{ fontWeight: 800, mb: 0.5 }}>
        Sections
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Operational sections under Course → Group → Semester. Subject curriculum is unchanged. Manual attendance remains
        fully compatible when no section filter is applied.
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

      <Stack direction="row" spacing={1.5} useFlexGap sx={{ flexWrap: "wrap", mb: 2 }}>
        <TextField select size="small" label="Academic Year" value={yearId || ""} onChange={(e) => setYearId(Number(e.target.value))} sx={{ minWidth: 180 }}>
          {years.map((y) => (
            <MenuItem key={y.id} value={y.id}>
              {y.name}
            </MenuItem>
          ))}
        </TextField>
        <TextField select size="small" label="Course" value={courseId || ""} onChange={(e) => setCourseId(Number(e.target.value))} sx={{ minWidth: 160 }}>
          {courses.map((c) => (
            <MenuItem key={c.id} value={c.id}>
              {c.name}
            </MenuItem>
          ))}
        </TextField>
        <TextField select size="small" label="Group" value={groupId || ""} onChange={(e) => setGroupId(Number(e.target.value))} sx={{ minWidth: 140 }}>
          <MenuItem value={0}>All</MenuItem>
          {filteredGroups.map((g) => (
            <MenuItem key={g.id} value={g.id}>
              {g.name}
            </MenuItem>
          ))}
        </TextField>
        <TextField select size="small" label="Semester" value={semesterId || ""} onChange={(e) => setSemesterId(Number(e.target.value))} sx={{ minWidth: 140 }}>
          <MenuItem value={0}>All</MenuItem>
          {filteredSemesters.map((s) => (
            <MenuItem key={s.id} value={s.id}>
              {s.name}
            </MenuItem>
          ))}
        </TextField>
      </Stack>

      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2 }}>
        <Tab label="Section List" />
        <Tab label="Student Allocation" />
        <Tab label="Faculty Allocation" />
        <Tab label="Transfer / Auto-Allocate" />
      </Tabs>

      {tab === 0 && (
        <>
          <Stack direction="row" spacing={1} sx={{ mb: 1 }}>
            {canCreate && (
              <Button variant="contained" onClick={openAdd} disabled={!yearId || !courseId || !groupId || !semesterId}>
                Create Section
              </Button>
            )}
            <Button variant="outlined" onClick={() => void loadSections()}>
              Refresh
            </Button>
          </Stack>
          {loading ? (
            <CircularProgress />
          ) : (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Code</TableCell>
                  <TableCell>Name</TableCell>
                  <TableCell>Strength</TableCell>
                  <TableCell>Capacity</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell align="right">Actions</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {rows.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell>{r.sectionCode}</TableCell>
                    <TableCell>{r.sectionName}</TableCell>
                    <TableCell>
                      {r.currentStrength}/{r.maximumStrength}
                    </TableCell>
                    <TableCell>{r.remainingCapacity}</TableCell>
                    <TableCell>{r.status}</TableCell>
                    <TableCell align="right">
                      {canEdit && (
                        <Button size="small" onClick={() => openEdit(r)}>
                          Edit
                        </Button>
                      )}
                      {canDelete && (
                        <Button size="small" color="error" onClick={() => void remove(r.id)}>
                          Delete
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
                {rows.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={6}>
                      <Typography variant="body2" color="text.secondary">
                        No sections for this scope. Create Section A/B/C or ensure General.
                      </Typography>
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          )}
        </>
      )}

      {tab === 1 && (
        <Stack spacing={1.5}>
          {canAssignStudents && (
            <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
              <TextField size="small" label="Student Id" value={assignStudentId} onChange={(e) => setAssignStudentId(e.target.value)} />
              <TextField select size="small" label="Section" value={assignSectionId || ""} onChange={(e) => setAssignSectionId(Number(e.target.value))} sx={{ minWidth: 140 }}>
                {rows.map((r) => (
                  <MenuItem key={r.id} value={r.id}>
                    {r.sectionCode} — {r.sectionName}
                  </MenuItem>
                ))}
              </TextField>
              <Button variant="contained" onClick={() => void doAssignStudent()}>
                Assign
              </Button>
            </Stack>
          )}
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Student</TableCell>
                <TableCell>Section</TableCell>
                <TableCell>From</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {studentRows.map((r) => (
                <TableRow key={r.id}>
                  <TableCell>
                    {r.studentNumber} — {r.studentName}
                  </TableCell>
                  <TableCell>
                    {r.sectionCode} {r.sectionName}
                  </TableCell>
                  <TableCell>{r.effectiveFrom}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Stack>
      )}

      {tab === 2 && (
        <Stack spacing={1.5}>
          {canAssignFaculty && (
            <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
              <TextField size="small" label="Faculty (Staff) Id" value={facultyId} onChange={(e) => setFacultyId(e.target.value)} />
              <TextField select size="small" label="Section" value={facultySectionId || ""} onChange={(e) => setFacultySectionId(Number(e.target.value))} sx={{ minWidth: 140 }}>
                {rows.map((r) => (
                  <MenuItem key={r.id} value={r.id}>
                    {r.sectionCode}
                  </MenuItem>
                ))}
              </TextField>
              <TextField select size="small" label="Role" value={facultyRole} onChange={(e) => setFacultyRole(e.target.value)} sx={{ minWidth: 120 }}>
                <MenuItem value="Primary">Primary</MenuItem>
                <MenuItem value="Secondary">Secondary</MenuItem>
              </TextField>
              <Button variant="contained" onClick={() => void doAssignFaculty()}>
                Assign
              </Button>
            </Stack>
          )}
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Faculty</TableCell>
                <TableCell>Section</TableCell>
                <TableCell>Role</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {facultyRows.map((r) => (
                <TableRow key={r.id}>
                  <TableCell>
                    {r.facultyName} (#{r.facultyId})
                  </TableCell>
                  <TableCell>{r.sectionCode}</TableCell>
                  <TableCell>{r.role}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Stack>
      )}

      {tab === 3 && (
        <Stack spacing={2}>
          {canAssignStudents && (
            <>
              <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                Transfer Student
              </Typography>
              <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
                <TextField size="small" label="Student Id" value={transferStudentId} onChange={(e) => setTransferStudentId(e.target.value)} />
                <TextField select size="small" label="Target Section" value={transferSectionId || ""} onChange={(e) => setTransferSectionId(Number(e.target.value))} sx={{ minWidth: 140 }}>
                  {rows.map((r) => (
                    <MenuItem key={r.id} value={r.id}>
                      {r.sectionCode}
                    </MenuItem>
                  ))}
                </TextField>
                <TextField size="small" label="Reason" value={transferReason} onChange={(e) => setTransferReason(e.target.value)} sx={{ minWidth: 200 }} />
                <Button variant="contained" onClick={() => void doTransfer()}>
                  Transfer
                </Button>
              </Stack>
              <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                Auto Allocation
              </Typography>
              <Stack direction="row" spacing={1}>
                <TextField select size="small" label="Strategy" value={strategy} onChange={(e) => setStrategy(e.target.value)} sx={{ minWidth: 180 }}>
                  <MenuItem value="Alphabetical">Alphabetical</MenuItem>
                  <MenuItem value="GenderBalance">Gender Balance</MenuItem>
                  <MenuItem value="Merit">Merit</MenuItem>
                  <MenuItem value="Random">Random</MenuItem>
                  <MenuItem value="CapacityBased">Capacity Based</MenuItem>
                </TextField>
                <Button variant="outlined" onClick={() => void doAutoAllocate()} disabled={!yearId || !courseId || !groupId || !semesterId}>
                  Run Auto-Allocate
                </Button>
              </Stack>
            </>
          )}
          <Alert severity="info">
            Combined classes are configured via Timetable → Sections mapping (one entry, many sections). Attendance history
            is never rewritten on transfer.
          </Alert>
        </Stack>
      )}

      <Dialog open={dialogOpen} onClose={() => setDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>{editingId ? "Edit Section" : "Create Section"}</DialogTitle>
        <DialogContent>
          <Stack spacing={1.5} sx={{ mt: 1 }}>
            <TextField label="Section Code" value={code} onChange={(e) => setCode(e.target.value)} helperText="e.g. A, B, C" />
            <TextField label="Section Name" value={name} onChange={(e) => setName(e.target.value)} helperText="e.g. Section A" />
            <TextField
              type="number"
              label="Maximum Strength"
              value={maxStrength}
              onChange={(e) => setMaxStrength(Number(e.target.value))}
              helperText="College-configurable capacity (no hardcoding)"
            />
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
    </Box>
  );
};

export default SectionsPage;
