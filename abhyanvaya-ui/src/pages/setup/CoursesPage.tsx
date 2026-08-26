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
  createCourse,
  listCourses,
  listDepartments,
  updateCourse,
  type CourseRow,
  type DepartmentRow,
} from "../../services/setupService";
import {
  getAcademicConfiguration,
  listPrograms,
  type ProgramDto,
} from "../../services/programService";
import AcademicConfirmDialog from "../../components/academic/AcademicConfirmDialog";
import { getApiErrorMessage } from "../../utils/apiErrorMessage";
import { programsForCourseAssignmentSelector } from "../../utils/courseProgramAssignment";
import { buildCourseMasterSavePlan } from "../../utils/courseMasterPersistence";
import {
  buildProgramReassignmentCopy,
  shouldConfirmProgramReassignment,
} from "../../utils/programReassignmentConfirmation";

/** AI29.1A policy: Course.ProgramId may be null — UI label "No Program". */
const UNASSIGNED_PROGRAM = 0;

const CoursesPage = () => {
  const [rows, setRows] = useState<CourseRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [editingId, setEditingId] = useState(0);
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [departmentId, setDepartmentId] = useState(0);
  const [departments, setDepartments] = useState<DepartmentRow[]>([]);
  const [programId, setProgramId] = useState(UNASSIGNED_PROGRAM);
  /** Program at dialog open — used for Prompt 15 change confirmation. */
  const [initialProgramId, setInitialProgramId] = useState(UNASSIGNED_PROGRAM);
  const [confirmProgramChangeOpen, setConfirmProgramChangeOpen] = useState(false);

  const [enablePrograms, setEnablePrograms] = useState(false);
  const [programs, setPrograms] = useState<ProgramDto[]>([]);
  const [programsLoading, setProgramsLoading] = useState(false);
  const [programsError, setProgramsError] = useState<string | null>(null);
  const [configRetryTick, setConfigRetryTick] = useState(0);

  const programNameById = useMemo(() => {
    const map = new Map<number, string>();
    for (const p of programs) map.set(p.id, p.programName);
    return map;
  }, [programs]);

  const departmentNameById = useMemo(() => {
    const map = new Map<number, string>();
    for (const d of departments) map.set(d.id, d.name);
    return map;
  }, [departments]);

  const selectorPrograms = useMemo(() => {
    const base = programsForCourseAssignmentSelector(programs, programId > 0 ? programId : null);
    if (departmentId <= 0) return base;
    return base.filter((p) => p.departmentId === departmentId || p.id === programId);
  }, [programs, programId, departmentId]);

  const loadCourses = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await listCourses();
      setRows(res.data ?? []);
    } catch (e) {
      setError(getApiErrorMessage(e, "Failed to load courses."));
    } finally {
      setLoading(false);
    }
  }, []);

  const loadDepartments = useCallback(async () => {
    try {
      const res = await listDepartments(undefined, true);
      setDepartments(res.data ?? []);
    } catch (e) {
      setError(getApiErrorMessage(e, "Failed to load departments."));
      setDepartments([]);
    }
  }, []);

  const loadProgramContext = useCallback(async () => {
    setProgramsLoading(true);
    setProgramsError(null);
    try {
      const cfg = await getAcademicConfiguration();
      const enabled = Boolean(cfg.data?.enablePrograms);
      setEnablePrograms(enabled);
      if (!enabled) {
        setPrograms([]);
        return;
      }
      const res = await listPrograms(true);
      setPrograms(res.data ?? []);
    } catch (e) {
      setEnablePrograms(false);
      setPrograms([]);
      setProgramsError(
        getApiErrorMessage(e, "Could not load Programs configuration.", {
          forbiddenFallback:
            "Programs could not be loaded. Requires Program.View (or Program.Manage) to assign Courses to Programs.",
        }),
      );
    } finally {
      setProgramsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadCourses();
    void loadDepartments();
  }, [loadCourses, loadDepartments]);

  useEffect(() => {
    void loadProgramContext();
  }, [loadProgramContext, configRetryTick]);

  const openAdd = () => {
    setEditingId(0);
    setCode("");
    setName("");
    setDepartmentId(departments.find((d) => d.isActive !== false)?.id ?? departments[0]?.id ?? 0);
    setProgramId(UNASSIGNED_PROGRAM);
    setInitialProgramId(UNASSIGNED_PROGRAM);
    setDialogOpen(true);
  };

  const openEdit = (r: CourseRow) => {
    setEditingId(r.id);
    setCode(r.code);
    setName(r.name);
    setDepartmentId(r.departmentId);
    const pid = r.programId && r.programId > 0 ? r.programId : UNASSIGNED_PROGRAM;
    setProgramId(pid);
    setInitialProgramId(pid);
    setDialogOpen(true);
  };

  const onProgramChange = (nextProgramId: number) => {
    setProgramId(nextProgramId);
    if (nextProgramId > 0) {
      const prog = programs.find((p) => p.id === nextProgramId);
      if (prog?.departmentId) setDepartmentId(prog.departmentId);
    }
  };

  const persistCourse = async () => {
    const c = code.trim().toUpperCase();
    const n = name.trim();
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      if (departmentId <= 0) {
        setError("Department is required.");
        return;
      }

      const plan = buildCourseMasterSavePlan({
        editingId,
        code: c,
        name: n,
        departmentId,
        programId,
        enablePrograms,
      });
      if (plan.callAssignCourseSeparately) {
        throw new Error("Course Master must not call assign-course separately.");
      }

      if (plan.mode === "update") {
        await updateCourse({
          id: plan.coursePayload.id!,
          code: plan.coursePayload.code,
          name: plan.coursePayload.name,
          departmentId: plan.coursePayload.departmentId,
          ...(enablePrograms ? { programId: plan.coursePayload.programId ?? null } : {}),
        });
      } else {
        const created = await createCourse({
          code: plan.coursePayload.code,
          name: plan.coursePayload.name,
          departmentId: plan.coursePayload.departmentId,
          ...(enablePrograms ? { programId: plan.coursePayload.programId ?? null } : {}),
        });
        if (!Number(created.data?.id ?? 0)) {
          throw new Error("Course was created but no id was returned.");
        }
      }

      setMessage(editingId ? "Course updated." : "Course created.");
      setConfirmProgramChangeOpen(false);
      setDialogOpen(false);
      await loadCourses();
      if (enablePrograms) await loadProgramContext();
    } catch (e) {
      setError(
        getApiErrorMessage(e, "Could not save course / Program assignment.", {
          forbiddenFallback:
            "Not authorized to assign this Course to a Program. Requires Program.Manage or Setup.Courses.Manage.",
        }),
      );
    } finally {
      setSaving(false);
    }
  };

  const save = async () => {
    const c = code.trim().toUpperCase();
    const n = name.trim();
    if (!c || !n) {
      setError("Code and name are required.");
      return;
    }
    if (departmentId <= 0) {
      setError("Department is required.");
      return;
    }
    if (saving || confirmProgramChangeOpen) return;

    if (
      shouldConfirmProgramReassignment({
        currentProgramId: initialProgramId,
        requestedProgramId: programId,
        isExistingCourse: editingId > 0,
        programsEnabled: enablePrograms,
      })
    ) {
      setConfirmProgramChangeOpen(true);
      return;
    }
    await persistCourse();
  };

  const cancelProgramChange = () => {
    if (saving) return;
    setProgramId(initialProgramId);
    setConfirmProgramChangeOpen(false);
  };

  const programLabel = (id: number) =>
    id > 0 ? (programNameById.get(id) ?? `Program #${id}`) : "No Program";

  const reassignmentCopy = buildProgramReassignmentCopy({
    courseLabel: name.trim() || "This course",
    currentProgramName: programLabel(initialProgramId),
    requestedProgramName: programLabel(programId),
  });

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup" startIcon={<ArrowBackIcon />} variant="text">
          Catalog
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Courses
        </Typography>
        <Button variant="contained" onClick={openAdd}>
          Add course
        </Button>
      </Box>
      {message && <Alert severity="success">{message}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}
      {programsError && (
        <Alert
          severity="warning"
          action={
            <Button color="inherit" size="small" onClick={() => setConfigRetryTick((n) => n + 1)}>
              Retry
            </Button>
          }
        >
          {programsError}
        </Alert>
      )}
      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", p: 3 }}>
          <CircularProgress />
        </Box>
      ) : (
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Code</TableCell>
              <TableCell>Name</TableCell>
              <TableCell>Department</TableCell>
              {enablePrograms ? <TableCell>Program</TableCell> : null}
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((r) => (
              <TableRow key={r.id} hover>
                <TableCell>{r.code}</TableCell>
                <TableCell>{r.name}</TableCell>
                <TableCell>{departmentNameById.get(r.departmentId) ?? r.departmentId}</TableCell>
                {enablePrograms ? (
                  <TableCell>
                    {r.programId && r.programId > 0
                      ? (programNameById.get(r.programId) ?? `Program #${r.programId}`)
                      : "No Program"}
                  </TableCell>
                ) : null}
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
        <DialogTitle>{editingId ? "Edit course" : "Add course"}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              label="Code"
              value={code}
              onChange={(e) => setCode(e.target.value.toUpperCase())}
              fullWidth
              required
            />
            <TextField
              label="Name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              fullWidth
              required
            />
            <TextField
              select
              label="Department"
              value={departmentId > 0 ? departmentId : ""}
              onChange={(e) => {
                const next = Number(e.target.value);
                setDepartmentId(next);
                if (programId > 0) {
                  const prog = programs.find((p) => p.id === programId);
                  if (prog && prog.departmentId !== next) setProgramId(UNASSIGNED_PROGRAM);
                }
              }}
              fullWidth
              required
              helperText="Course belongs to one Department (catalog ownership)."
            >
              {departments.map((d) => (
                <MenuItem key={d.id} value={d.id}>
                  {d.code ? `${d.code} — ${d.name}` : d.name}
                </MenuItem>
              ))}
            </TextField>
            {enablePrograms ? (
              <TextField
                select
                label="Program"
                value={programId}
                onChange={(e) => onProgramChange(Number(e.target.value))}
                fullWidth
                disabled={programsLoading || saving}
                helperText={
                  programsLoading
                    ? "Loading programs…"
                    : selectorPrograms.length === 0
                      ? "No compatible Programs for this Department (or create an Active Program)."
                      : "Optional. When selected, Department must match the Program Department (server-enforced)."
                }
              >
                <MenuItem value={UNASSIGNED_PROGRAM}>No Program</MenuItem>
                {selectorPrograms.map((p) => (
                  <MenuItem key={p.id} value={p.id}>
                    {p.programName}
                    {String(p.status).toLowerCase() === "inactive" || !p.isActive
                      ? " (Inactive)"
                      : String(p.status).toLowerCase() === "archived"
                        ? " (Archived)"
                        : ""}
                  </MenuItem>
                ))}
              </TextField>
            ) : null}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)} disabled={saving}>
            Cancel
          </Button>
          <Button variant="contained" onClick={() => void save()} disabled={saving || programsLoading}>
            {saving ? "Saving…" : "Save"}
          </Button>
        </DialogActions>
      </Dialog>

      <AcademicConfirmDialog
        open={confirmProgramChangeOpen}
        title={reassignmentCopy.title}
        description={reassignmentCopy.description}
        confirmLabel="Confirm"
        cancelLabel="Cancel"
        confirmColor="primary"
        confirming={saving}
        onCancel={cancelProgramChange}
        onConfirm={() => {
          if (saving) return;
          void persistCourse();
        }}
      />
    </Stack>
  );
};

export default CoursesPage;
