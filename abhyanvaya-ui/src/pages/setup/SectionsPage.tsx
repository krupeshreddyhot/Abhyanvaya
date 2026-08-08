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
import {
  filterSemestersForScope,
  listGroups,
  listMasterCourses,
  listSemesters,
  type CourseRow,
  type GroupRow,
  type SemesterRow,
} from "../../services/setupService";
import {
  assignFacultySection,
  assignStudentSection,
  autoAllocateSections,
  commitMerge,
  commitSplit,
  createSection,
  deleteSection,
  exportSectionReport,
  getCapacitySummary,
  getLifecycleHistory,
  getMergeHistory,
  getSectionHealth,
  getSectionOccupancy,
  getSplitHistory,
  listFacultySections,
  listLifecycleStates,
  listSections,
  listStudentSections,
  previewMerge,
  previewSplit,
  transferStudentSection,
  transitionSectionLifecycle,
  updateSection,
  type FacultySectionDto,
  type SectionCapacitySnapshotDto,
  type SectionDto,
  type SectionMergePreviewDto,
  type SectionReadinessDto,
  type SectionSplitPreviewDto,
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
  const canLifecycleEdit = hasPermission(PermissionKeys.SectionLifecycleEdit);
  const canCapacity = hasPermission(PermissionKeys.SectionCapacity);
  const canMerge = hasPermission(PermissionKeys.SectionMerge);
  const canSplit = hasPermission(PermissionKeys.SectionSplit);
  const canReadiness = hasPermission(PermissionKeys.SectionReadiness);

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

  // AI29.1B
  const [lifecycleStates, setLifecycleStates] = useState<string[]>([]);
  const [lifecycleSectionId, setLifecycleSectionId] = useState(0);
  const [lifecycleTarget, setLifecycleTarget] = useState("Active");
  const [lifecycleReason, setLifecycleReason] = useState("");
  const [lifecycleHistory, setLifecycleHistory] = useState<{ fromStatus: string; toStatus: string; reason?: string; transitionedUtc: string }[]>([]);
  const [occupancy, setOccupancy] = useState<SectionCapacitySnapshotDto[]>([]);
  const [capacitySummary, setCapacitySummary] = useState<string>("");
  const [health, setHealth] = useState<SectionReadinessDto[]>([]);
  const [mergeSources, setMergeSources] = useState("");
  const [mergeTarget, setMergeTarget] = useState(0);
  const [mergePreview, setMergePreview] = useState<SectionMergePreviewDto | null>(null);
  const [splitSource, setSplitSource] = useState(0);
  const [splitPreview, setSplitPreview] = useState<SectionSplitPreviewDto | null>(null);
  const [mergeHistory, setMergeHistory] = useState<string>("");
  const [splitHistory, setSplitHistory] = useState<string>("");

  const filteredGroups = useMemo(() => groups.filter((g) => !courseId || g.courseId === courseId), [groups, courseId]);
  const filteredSemesters = useMemo(
    () => filterSemestersForScope(semesters, courseId, groupId),
    [semesters, courseId, groupId],
  );

  useEffect(() => {
    if (semesterId > 0 && !filteredSemesters.some((s) => s.id === semesterId)) {
      setSemesterId(0);
    }
  }, [filteredSemesters, semesterId]);

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

  const loadOps = async () => {
    try {
      if (canCapacity) {
        const [occ, sum] = await Promise.all([
          getSectionOccupancy({ academicYearId: yearId || undefined, semesterId: semesterId || undefined }),
          getCapacitySummary({ academicYearId: yearId || undefined, semesterId: semesterId || undefined }),
        ]);
        setOccupancy(occ.data);
        setCapacitySummary(
          `${sum.data.sectionCount} sections · avg occupancy ${sum.data.averageOccupancyPercent}% · over ${sum.data.overCapacityCount} · under ${sum.data.underCapacityCount}`,
        );
      }
      if (canReadiness) {
        const h = await getSectionHealth();
        setHealth(h.data);
      }
      const states = await listLifecycleStates();
      setLifecycleStates(states.data);
    } catch (e) {
      setError(errMsg(e));
    }
  };

  useEffect(() => {
    if (tab >= 4) void loadOps();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tab, yearId, semesterId]);

  const doTransition = async () => {
    try {
      await transitionSectionLifecycle(lifecycleSectionId, { targetStatus: lifecycleTarget, reason: lifecycleReason || undefined });
      setMessage("Lifecycle transition applied.");
      const hist = await getLifecycleHistory(lifecycleSectionId);
      setLifecycleHistory(hist.data);
      await loadSections();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const doMergePreview = async () => {
    try {
      const ids = mergeSources
        .split(/[,\s]+/)
        .map((x) => Number(x))
        .filter((n) => n > 0);
      const res = await previewMerge({ sourceSectionIds: ids, targetSectionId: mergeTarget });
      setMergePreview(res.data);
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const doMergeCommit = async () => {
    try {
      const ids = mergeSources
        .split(/[,\s]+/)
        .map((x) => Number(x))
        .filter((n) => n > 0);
      await commitMerge({
        sourceSectionIds: ids,
        targetSectionId: mergeTarget,
        effectiveDate: new Date().toISOString().slice(0, 10),
      });
      setMessage("Merge committed (sources preserved as Merged).");
      await loadSections();
      const hist = await getMergeHistory();
      setMergeHistory(hist.data.map((h) => `${h.transactionId}: ${h.sourceSectionIds.join("+")}→${h.targetSectionId} (${h.status})`).join("\n"));
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const doSplitPreview = async () => {
    try {
      const res = await previewSplit({ sourceSectionId: splitSource, childCount: 2, strategyCode: "Manual" });
      setSplitPreview(res.data);
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const doSplitCommit = async () => {
    try {
      await commitSplit({
        sourceSectionId: splitSource,
        strategyCode: "Manual",
        effectiveDate: new Date().toISOString().slice(0, 10),
        children: splitPreview?.proposedChildren,
      });
      setMessage("Split committed (source preserved as Split; children created).");
      await loadSections();
      const hist = await getSplitHistory();
      setSplitHistory(hist.data.map((h) => `${h.transactionId}: ${h.sourceSectionId}→[${h.childSectionIds.join(",")}] (${h.status})`).join("\n"));
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const doExport = async (kind: string, format: string) => {
    try {
      const res = await exportSectionReport(kind, format);
      const url = URL.createObjectURL(res.data);
      const a = document.createElement("a");
      a.href = url;
      a.download = `${kind}.${format === "excel" ? "xlsx" : format}`;
      a.click();
      URL.revokeObjectURL(url);
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
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
        Operational sections under Course → Group → Semester. Subject curriculum is unchanged. Manual attendance remains
        fully compatible when no section filter is applied.
      </Typography>
      <Button component={RouterLink} to="/setup/academic/allocation-context" size="small" sx={{ mb: 2 }}>
        Allocation Context Explorer (read-only)
      </Button>

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
        <TextField
          select
          size="small"
          label="Course"
          value={courseId || ""}
          onChange={(e) => {
            setCourseId(Number(e.target.value));
            setGroupId(0);
            setSemesterId(0);
          }}
          sx={{ minWidth: 160 }}
        >
          {courses.map((c) => (
            <MenuItem key={c.id} value={c.id}>
              {c.name}
            </MenuItem>
          ))}
        </TextField>
        <TextField
          select
          size="small"
          label="Group"
          value={groupId || ""}
          onChange={(e) => {
            setGroupId(Number(e.target.value));
            setSemesterId(0);
          }}
          sx={{ minWidth: 140 }}
        >
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

      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2 }} variant="scrollable" scrollButtons="auto">
        <Tab label="Section List" />
        <Tab label="Student Allocation" />
        <Tab label="Faculty Allocation" />
        <Tab label="Transfer / Auto-Allocate" />
        <Tab label="Lifecycle" />
        <Tab label="Capacity" />
        <Tab label="Merge" />
        <Tab label="Split" />
        <Tab label="Readiness" />
        <Tab label="History" />
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
                      {r.occupancyPercent != null ? ` (${r.occupancyPercent}%)` : ""}
                    </TableCell>
                    <TableCell>
                      {r.remainingCapacity}
                      {r.capacityStatus ? ` · ${r.capacityStatus}` : ""}
                    </TableCell>
                    <TableCell>
                      {r.status}
                      {r.sectionTypeCode ? ` · ${r.sectionTypeCode}` : ""}
                    </TableCell>
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
            Combined classes use Section Groups + Timetable → Sections mapping (one entry, many sections). Attendance history
            is never rewritten on transfer. Readiness never auto-fixes operations.
          </Alert>
        </Stack>
      )}

      {tab === 4 && (
        <Stack spacing={1.5}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Section Lifecycle (state machine)
          </Typography>
          <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
            <TextField select size="small" label="Section" value={lifecycleSectionId || ""} onChange={(e) => setLifecycleSectionId(Number(e.target.value))} sx={{ minWidth: 160 }}>
              {rows.map((r) => (
                <MenuItem key={r.id} value={r.id}>
                  {r.sectionCode} ({r.status})
                </MenuItem>
              ))}
            </TextField>
            <TextField select size="small" label="Target Status" value={lifecycleTarget} onChange={(e) => setLifecycleTarget(e.target.value)} sx={{ minWidth: 140 }}>
              {(lifecycleStates.length ? lifecycleStates : ["Draft", "Planning", "Open", "Active", "Locked", "Closed", "Archived"]).map((s) => (
                <MenuItem key={s} value={s}>
                  {s}
                </MenuItem>
              ))}
            </TextField>
            <TextField size="small" label="Reason" value={lifecycleReason} onChange={(e) => setLifecycleReason(e.target.value)} sx={{ minWidth: 200 }} />
            {canLifecycleEdit && (
              <Button variant="contained" onClick={() => void doTransition()} disabled={!lifecycleSectionId}>
                Transition
              </Button>
            )}
            <Button
              variant="outlined"
              onClick={async () => {
                if (!lifecycleSectionId) return;
                const hist = await getLifecycleHistory(lifecycleSectionId);
                setLifecycleHistory(hist.data);
              }}
            >
              Load History
            </Button>
          </Stack>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>From</TableCell>
                <TableCell>To</TableCell>
                <TableCell>When</TableCell>
                <TableCell>Reason</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {lifecycleHistory.map((h, i) => (
                <TableRow key={i}>
                  <TableCell>{h.fromStatus}</TableCell>
                  <TableCell>{h.toStatus}</TableCell>
                  <TableCell>{h.transitionedUtc}</TableCell>
                  <TableCell>{h.reason}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Stack>
      )}

      {tab === 5 && (
        <Stack spacing={1.5}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Capacity Engine
          </Typography>
          {capacitySummary && <Alert severity="info">{capacitySummary}</Alert>}
          <Stack direction="row" spacing={1}>
            <Button variant="outlined" onClick={() => void loadOps()}>
              Refresh Capacity
            </Button>
            <Button variant="outlined" onClick={() => void doExport("section-capacity", "csv")}>
              Export CSV
            </Button>
            <Button variant="outlined" onClick={() => void doExport("section-occupancy", "excel")}>
              Export Excel
            </Button>
          </Stack>
          {canCapacity ? (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Section</TableCell>
                  <TableCell>Occupancy</TableCell>
                  <TableCell>Available</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell>Warnings</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {occupancy.map((r) => (
                  <TableRow key={r.sectionId}>
                    <TableCell>
                      {r.sectionCode} — {r.sectionName}
                    </TableCell>
                    <TableCell>
                      {r.currentStrength}/{r.maximumCapacity} ({r.occupancyPercent}%)
                    </TableCell>
                    <TableCell>{r.availableSeats}</TableCell>
                    <TableCell>{r.capacityStatus}</TableCell>
                    <TableCell>{r.warnings?.join(" ")}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          ) : (
            <Alert severity="warning">Section.Capacity permission required.</Alert>
          )}
        </Stack>
      )}

      {tab === 6 && (
        <Stack spacing={1.5}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Merge Wizard
          </Typography>
          {canMerge ? (
            <>
              <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
                <TextField size="small" label="Source Section Ids (comma)" value={mergeSources} onChange={(e) => setMergeSources(e.target.value)} sx={{ minWidth: 220 }} />
                <TextField select size="small" label="Target Section" value={mergeTarget || ""} onChange={(e) => setMergeTarget(Number(e.target.value))} sx={{ minWidth: 160 }}>
                  {rows.map((r) => (
                    <MenuItem key={r.id} value={r.id}>
                      {r.sectionCode}
                    </MenuItem>
                  ))}
                </TextField>
                <Button variant="outlined" onClick={() => void doMergePreview()}>
                  Validate / Preview
                </Button>
                <Button variant="contained" onClick={() => void doMergeCommit()} disabled={!mergePreview?.isValid}>
                  Commit Merge
                </Button>
              </Stack>
              {mergePreview && (
                <Alert severity={mergePreview.isValid ? "success" : "error"}>
                  Valid={String(mergePreview.isValid)} · students={mergePreview.combinedStudentCount} · faculty={mergePreview.combinedFacultyCount}
                  {mergePreview.errors?.length ? ` · ${mergePreview.errors.join(" ")}` : ""}
                  {mergePreview.warnings?.length ? ` · ${mergePreview.warnings.join(" ")}` : ""}
                </Alert>
              )}
            </>
          ) : (
            <Alert severity="warning">Section.Merge permission required.</Alert>
          )}
        </Stack>
      )}

      {tab === 7 && (
        <Stack spacing={1.5}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Split Wizard
          </Typography>
          {canSplit ? (
            <>
              <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
                <TextField select size="small" label="Source Section" value={splitSource || ""} onChange={(e) => setSplitSource(Number(e.target.value))} sx={{ minWidth: 160 }}>
                  {rows.map((r) => (
                    <MenuItem key={r.id} value={r.id}>
                      {r.sectionCode}
                    </MenuItem>
                  ))}
                </TextField>
                <Button variant="outlined" onClick={() => void doSplitPreview()}>
                  Preview
                </Button>
                <Button variant="contained" onClick={() => void doSplitCommit()} disabled={!splitPreview?.isValid}>
                  Commit Split
                </Button>
              </Stack>
              {splitPreview && (
                <Alert severity={splitPreview.isValid ? "info" : "error"}>
                  Students={splitPreview.sourceStudentCount} · strategy={splitPreview.strategyCode}
                  {splitPreview.proposedChildren?.map((c) => ` · ${c.proposedCode}(${c.plannedStudentCount})`).join("")}
                  {splitPreview.warnings?.length ? ` · ${splitPreview.warnings.join(" ")}` : ""}
                </Alert>
              )}
            </>
          ) : (
            <Alert severity="warning">Section.Split permission required.</Alert>
          )}
        </Stack>
      )}

      {tab === 8 && (
        <Stack spacing={1.5}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Operational Readiness (advisory only)
          </Typography>
          {canReadiness ? (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Section</TableCell>
                  <TableCell>Overall</TableCell>
                  <TableCell>Checks</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {health.map((h) => (
                  <TableRow key={h.sectionId}>
                    <TableCell>
                      {h.sectionCode} — {h.sectionName}
                    </TableCell>
                    <TableCell>{h.overallStatus}</TableCell>
                    <TableCell>{h.checks.map((c) => `${c.area}:${c.status}`).join(" · ")}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          ) : (
            <Alert severity="warning">Section.Readiness permission required.</Alert>
          )}
        </Stack>
      )}

      {tab === 9 && (
        <Stack spacing={1.5}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Merge / Split History
          </Typography>
          <Stack direction="row" spacing={1}>
            <Button
              variant="outlined"
              onClick={async () => {
                const hist = await getMergeHistory();
                setMergeHistory(hist.data.map((h) => `${h.transactionId}: ${h.sourceSectionIds.join("+")}→${h.targetSectionId} (${h.status})`).join("\n"));
              }}
            >
              Load Merge History
            </Button>
            <Button
              variant="outlined"
              onClick={async () => {
                const hist = await getSplitHistory();
                setSplitHistory(hist.data.map((h) => `${h.transactionId}: ${h.sourceSectionId}→[${h.childSectionIds.join(",")}] (${h.status})`).join("\n"));
              }}
            >
              Load Split History
            </Button>
            <Button variant="outlined" onClick={() => void doExport("merge-history", "csv")}>
              Export Merge CSV
            </Button>
            <Button variant="outlined" onClick={() => void doExport("readiness", "csv")}>
              Export Readiness CSV
            </Button>
          </Stack>
          <Typography variant="body2" component="pre" sx={{ whiteSpace: "pre-wrap" }}>
            {mergeHistory || "No merge history loaded."}
          </Typography>
          <Typography variant="body2" component="pre" sx={{ whiteSpace: "pre-wrap" }}>
            {splitHistory || "No split history loaded."}
          </Typography>
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
