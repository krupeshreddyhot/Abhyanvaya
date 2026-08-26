import { useCallback, useEffect, useMemo, useState } from "react";
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
  MenuItem,
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
import AcademicConfirmDialog from "../../components/academic/AcademicConfirmDialog";
import { PermissionKeys } from "../../auth/permissionKeys";
import { useAuth } from "../../context/AuthContext";
import { useAcademicUi } from "../../context/AcademicUiContext";
import { listCourses, type CourseRow } from "../../services/setupService";
import {
  archiveProgram,
  assignCourseToProgram,
  createProgram,
  deleteProgram,
  getAcademicConfiguration,
  getProgramCourses,
  listProgramDepartmentOptions,
  listPrograms,
  updateAcademicConfiguration,
  updateProgram,
  type ProgramCourseRow,
  type ProgramDepartmentOptionDto,
  type ProgramDto,
} from "../../services/programService";
import { getApiErrorMessage } from "../../utils/apiErrorMessage";
import { coursesAvailableForProgramAssignment } from "../../utils/programCourseAssignment";
import {
  buildProgramReassignmentCopy,
  shouldConfirmProgramReassignment,
} from "../../utils/programReassignmentConfirmation";

const errMsg = (e: unknown): string => getApiErrorMessage(e, "Request failed.");

const ProgramsPage = () => {
  const { hasPermission } = useAuth();
  const academicUi = useAcademicUi();
  const canCreate = hasPermission(PermissionKeys.ProgramCreate);
  const canEdit = hasPermission(PermissionKeys.ProgramEdit);
  const canDelete = hasPermission(PermissionKeys.ProgramDelete);
  const canManage = hasPermission(PermissionKeys.ProgramManage);
  /** Same server policy as CanAssignCourseToProgram — Program.Manage OR Setup.Courses.Manage. */
  const canAssignCourse =
    hasPermission(PermissionKeys.ProgramManage) || hasPermission(PermissionKeys.SetupCoursesManage);

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
  const [departmentId, setDepartmentId] = useState(0);
  const [departmentOptions, setDepartmentOptions] = useState<ProgramDepartmentOptionDto[]>([]);

  const [assignedCourses, setAssignedCourses] = useState<ProgramCourseRow[]>([]);
  const [allCourses, setAllCourses] = useState<CourseRow[]>([]);
  const [coursesLoading, setCoursesLoading] = useState(false);
  const [assignCourseId, setAssignCourseId] = useState(0);
  const [assigning, setAssigning] = useState(false);
  const [unassignTarget, setUnassignTarget] = useState<ProgramCourseRow | null>(null);
  const [reassignConfirmOpen, setReassignConfirmOpen] = useState(false);

  const availableCourses = useMemo(
    () =>
      viewRow
        ? coursesAvailableForProgramAssignment(allCourses, viewRow.id)
        : [],
    [allCourses, viewRow],
  );

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const [cfg, list, depts] = await Promise.all([
        getAcademicConfiguration(),
        listPrograms(true),
        listProgramDepartmentOptions(),
      ]);
      setEnablePrograms(cfg.data.enablePrograms);
      setRows(list.data);
      setDepartmentOptions(depts.data ?? []);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  };

  const refreshViewCourses = useCallback(async (program: ProgramDto) => {
    setCoursesLoading(true);
    try {
      const [progCourses, catalog] = await Promise.all([
        getProgramCourses(program.id),
        listCourses(),
      ]);
      setAssignedCourses(progCourses.data ?? []);
      setAllCourses(catalog.data ?? []);
      // Keep list count in sync with authoritative Course.ProgramId query.
      setViewRow((prev) =>
        prev && prev.id === program.id
          ? { ...prev, courseCount: (progCourses.data ?? []).length }
          : prev,
      );
      setRows((prev) =>
        prev.map((r) =>
          r.id === program.id ? { ...r, courseCount: (progCourses.data ?? []).length } : r,
        ),
      );
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setCoursesLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, []);

  const openView = async (r: ProgramDto) => {
    setViewRow(r);
    setViewOpen(true);
    setAssignCourseId(0);
    setAssignedCourses([]);
    await refreshViewCourses(r);
  };

  const togglePrograms = async (next: boolean) => {
    if (!canManage) return;
    try {
      const res = await updateAcademicConfiguration(next);
      setEnablePrograms(res.data.enablePrograms);
      setMessage(
        next
          ? "Programs enabled — hierarchy is Department → Program → Course."
          : "Programs disabled — hierarchy is Department → Course (Program not required).",
      );
      await academicUi.refreshCatalogs();
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
    setDepartmentId(departmentOptions.find((d) => d.isActive)?.id ?? departmentOptions[0]?.id ?? 0);
    setDialogOpen(true);
  };

  const openEdit = (r: ProgramDto) => {
    setEditingId(r.id);
    setCode(r.programCode);
    setName(r.programName);
    setDescription(r.description ?? "");
    setDisplayOrder(r.displayOrder);
    setDepartmentId(r.departmentId);
    setDialogOpen(true);
  };

  const save = async () => {
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      if (departmentId <= 0) {
        setError("Department is required.");
        return;
      }
      if (editingId) {
        await updateProgram(editingId, {
          departmentId,
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
          departmentId,
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
      await academicUi.refreshCatalogs();
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
      await academicUi.refreshCatalogs();
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
      await academicUi.refreshCatalogs();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const selectedAssignCourse = useMemo(
    () => allCourses.find((c) => c.id === assignCourseId) ?? null,
    [allCourses, assignCourseId],
  );

  const reassignmentCopy = useMemo(() => {
    if (!viewRow || !selectedAssignCourse) {
      return buildProgramReassignmentCopy({
        courseLabel: "This course",
        currentProgramName: "its current Program",
        requestedProgramName: "the selected Program",
      });
    }
    const currentName =
      rows.find((p) => p.id === selectedAssignCourse.programId)?.programName ??
      (selectedAssignCourse.programId ? `Program #${selectedAssignCourse.programId}` : "No Program");
    return buildProgramReassignmentCopy({
      courseLabel: `${selectedAssignCourse.code} — ${selectedAssignCourse.name}`,
      currentProgramName: currentName,
      requestedProgramName: viewRow.programName,
    });
  }, [viewRow, selectedAssignCourse, rows]);

  const performAssign = async () => {
    if (!viewRow || assignCourseId <= 0 || !canAssignCourse) return;
    if (viewRow.status === "Archived" || !viewRow.isActive) {
      setError("Archived or inactive Programs cannot receive new Courses.");
      return;
    }
    setAssigning(true);
    setError(null);
    try {
      // Authoritative Course.ProgramId contract — existing API only (no new endpoint).
      await assignCourseToProgram(assignCourseId, viewRow.id);
      setMessage("Course assigned to Program.");
      setAssignCourseId(0);
      setReassignConfirmOpen(false);
      await refreshViewCourses(viewRow);
      await load();
      await academicUi.refreshCatalogs();
    } catch (e) {
      setError(
        getApiErrorMessage(e, "Could not assign Course to Program.", {
          forbiddenFallback:
            "Not authorized. Requires Program.Manage or Setup.Courses.Manage.",
        }),
      );
    } finally {
      setAssigning(false);
    }
  };

  const doAssign = async () => {
    if (!viewRow || assignCourseId <= 0 || !canAssignCourse || assigning || reassignConfirmOpen) return;
    if (viewRow.status === "Archived" || !viewRow.isActive) {
      setError("Archived or inactive Programs cannot receive new Courses.");
      return;
    }
    const course = allCourses.find((c) => c.id === assignCourseId);
    if (!course) return;

    // AI29.1D.24A — confirm only when moving an existing Course between Programs.
    if (
      shouldConfirmProgramReassignment({
        currentProgramId: course.programId ?? null,
        requestedProgramId: viewRow.id,
        isExistingCourse: true,
        programsEnabled: enablePrograms,
      })
    ) {
      setReassignConfirmOpen(true);
      return;
    }
    await performAssign();
  };

  const cancelReassign = () => {
    if (assigning) return;
    setReassignConfirmOpen(false);
    // No API, no refresh, no event — leave dropdown selection as-is.
  };

  const doUnassign = async () => {
    if (!unassignTarget || !viewRow || !canAssignCourse) return;
    setAssigning(true);
    setError(null);
    try {
      await assignCourseToProgram(unassignTarget.id, null);
      setMessage("Course unassigned (No Program).");
      setUnassignTarget(null);
      await refreshViewCourses(viewRow);
      await load();
      await academicUi.refreshCatalogs();
    } catch (e) {
      setError(
        getApiErrorMessage(e, "Could not unassign Course.", {
          forbiddenFallback:
            "Not authorized. Requires Program.Manage or Setup.Courses.Manage.",
        }),
      );
    } finally {
      setAssigning(false);
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
        Optional academic grouping under a Department (Commerce, Arts, Science, …). When Programs are disabled,
        Department → Course remains the catalog path. Attendance and timetable workflows are unchanged.
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
          Enable Programs to manage Department → Program → Course. Existing Department → Course → Group → Semester
          structure continues to work without Program selection.
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
              <TableCell>Department</TableCell>
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
                <TableCell>{r.departmentName ?? r.departmentId}</TableCell>
                <TableCell>{r.courseCount}</TableCell>
                <TableCell>{r.studentCount}</TableCell>
                <TableCell>{r.facultyCount}</TableCell>
                <TableCell>{r.status}</TableCell>
                <TableCell align="right">
                  <Button size="small" onClick={() => void openView(r)}>
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
                <TableCell colSpan={8}>
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
            <TextField
              select
              label="Department"
              value={departmentId > 0 ? departmentId : ""}
              onChange={(e) => setDepartmentId(Number(e.target.value))}
              required
              helperText="Program belongs to one Department in this College."
            >
              {departmentOptions.map((d) => (
                <MenuItem key={d.id} value={d.id} disabled={!d.isActive && d.id !== departmentId}>
                  {d.code ? `${d.code} — ${d.name}` : d.name}
                </MenuItem>
              ))}
            </TextField>
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

      <Dialog open={viewOpen} onClose={() => setViewOpen(false)} fullWidth maxWidth="md">
        <DialogTitle>{viewRow ? viewRow.programName : "View Program"}</DialogTitle>
        <DialogContent>
          {viewRow && (
            <Stack spacing={2} sx={{ mt: 1 }}>
              <Typography>
                <strong>{viewRow.programCode}</strong> — {viewRow.programName}
              </Typography>
              <Typography variant="body2">
                Department: {viewRow.departmentName ?? viewRow.departmentId}
                {viewRow.departmentCode ? ` (${viewRow.departmentCode})` : ""}
              </Typography>
              <Typography variant="body2">{viewRow.description || "No description."}</Typography>
              <Typography variant="body2">
                Courses: <strong>{viewRow.courseCount}</strong> (from Course.ProgramId)
              </Typography>
              <Typography variant="body2">Students: {viewRow.studentCount}</Typography>
              <Typography variant="body2">Faculty: {viewRow.facultyCount}</Typography>
              <Typography variant="body2">Status: {viewRow.status}</Typography>

              <Typography variant="subtitle1" sx={{ fontWeight: 700, pt: 1 }}>
                Assigned Courses
              </Typography>
              {coursesLoading ? (
                <CircularProgress size={28} />
              ) : (
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Code</TableCell>
                      <TableCell>Course</TableCell>
                      {canAssignCourse ? <TableCell align="right">Actions</TableCell> : null}
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {assignedCourses.map((c) => (
                      <TableRow key={c.id}>
                        <TableCell>{c.code}</TableCell>
                        <TableCell>{c.name}</TableCell>
                        {canAssignCourse ? (
                          <TableCell align="right">
                            <Button
                              size="small"
                              disabled={assigning}
                              onClick={() => setUnassignTarget(c)}
                            >
                              Unassign
                            </Button>
                          </TableCell>
                        ) : null}
                      </TableRow>
                    ))}
                    {assignedCourses.length === 0 && (
                      <TableRow>
                        <TableCell colSpan={canAssignCourse ? 3 : 2}>
                          <Typography variant="body2" color="text.secondary">
                            No courses assigned to this Program.
                          </Typography>
                        </TableCell>
                      </TableRow>
                    )}
                  </TableBody>
                </Table>
              )}

              {canAssignCourse && viewRow.status !== "Archived" && viewRow.isActive && (
                <Stack spacing={1.5} sx={{ pt: 1 }}>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                    Assign Course
                  </Typography>
                  <TextField
                    select
                    label="Select Course"
                    value={assignCourseId}
                    onChange={(e) => setAssignCourseId(Number(e.target.value))}
                    fullWidth
                    disabled={assigning || coursesLoading}
                    helperText={
                      availableCourses.length === 0
                        ? "No courses available for assignment (all tenant courses may already be on this Program)."
                        : "Uses POST /api/programs/assign-course → Course.ProgramId."
                    }
                  >
                    <MenuItem value={0}>Select Course</MenuItem>
                    {availableCourses.map((c) => (
                      <MenuItem key={c.id} value={c.id}>
                        {c.code} — {c.name}
                        {c.programId ? " (reassign)" : ""}
                      </MenuItem>
                    ))}
                  </TextField>
                  <Button
                    variant="contained"
                    disabled={assigning || assignCourseId <= 0}
                    onClick={() => void doAssign()}
                  >
                    {assigning ? "Assigning…" : "Assign"}
                  </Button>
                </Stack>
              )}
            </Stack>
          )}
        </DialogContent>
        <DialogActions>
          <Button
            onClick={() => {
              if (viewRow) void refreshViewCourses(viewRow);
            }}
            disabled={coursesLoading}
          >
            Refresh courses
          </Button>
          <Button onClick={() => setViewOpen(false)}>Close</Button>
        </DialogActions>
      </Dialog>

      <AcademicConfirmDialog
        open={!!unassignTarget}
        title="Unassign Course?"
        description={
          unassignTarget
            ? `${unassignTarget.code} — ${unassignTarget.name} will be set to No Program (Course.ProgramId = null).`
            : ""
        }
        confirmLabel="Unassign"
        cancelLabel="Cancel"
        confirmColor="warning"
        confirming={assigning}
        onCancel={() => {
          if (!assigning) setUnassignTarget(null);
        }}
        onConfirm={() => {
          if (assigning) return;
          void doUnassign();
        }}
      />

      <AcademicConfirmDialog
        open={reassignConfirmOpen}
        title={reassignmentCopy.title}
        description={reassignmentCopy.description}
        confirmLabel="Confirm"
        cancelLabel="Cancel"
        confirmColor="primary"
        confirming={assigning}
        onCancel={cancelReassign}
        onConfirm={() => {
          if (assigning) return;
          void performAssign();
        }}
      />
    </Box>
  );
};

export default ProgramsPage;
