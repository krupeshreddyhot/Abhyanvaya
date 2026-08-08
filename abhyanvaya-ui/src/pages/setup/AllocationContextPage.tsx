import { useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
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
  createAllocationSnapshot,
  getAllocationArchitectureReport,
  getAllocationContext,
  getAllocationHealth,
  getAllocationReadiness,
  getAllocationValidation,
  listAllocationSnapshots,
  type AllocationHealthReport,
  type AllocationReadinessReport,
  type AllocationSnapshotDto,
  type AllocationValidationReport,
  type SectionAllocationContext,
} from "../../services/allocationPlatformService";

const errMsg = (e: unknown): string => {
  const d = (e as { response?: { data?: unknown } }).response?.data;
  if (typeof d === "string") return d;
  return "Request failed.";
};

const AllocationContextPage = () => {
  const { hasPermission } = useAuth();
  const canView = hasPermission(PermissionKeys.SectionView);

  const [years, setYears] = useState<AcademicYearDto[]>([]);
  const [courses, setCourses] = useState<CourseRow[]>([]);
  const [groups, setGroups] = useState<GroupRow[]>([]);
  const [semesters, setSemesters] = useState<SemesterRow[]>([]);
  const [yearId, setYearId] = useState(0);
  const [courseId, setCourseId] = useState(0);
  const [groupId, setGroupId] = useState(0);
  const [semesterId, setSemesterId] = useState(0);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [context, setContext] = useState<SectionAllocationContext | null>(null);
  const [readiness, setReadiness] = useState<AllocationReadinessReport | null>(null);
  const [health, setHealth] = useState<AllocationHealthReport | null>(null);
  const [validation, setValidation] = useState<AllocationValidationReport | null>(null);
  const [snapshots, setSnapshots] = useState<AllocationSnapshotDto[]>([]);
  const [arch, setArch] = useState<string>("");

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

  useEffect(() => {
    void (async () => {
      try {
        const [y, c, g, s] = await Promise.all([listAcademicYears(), listMasterCourses(), listGroups(), listSemesters()]);
        setYears(y.data);
        setCourses(c.data);
        setGroups(g.data);
        setSemesters(s.data);
        if (y.data[0]) setYearId(y.data[0].id);
      } catch (e) {
        setError(errMsg(e));
      }
    })();
  }, []);

  const scopeReady = yearId > 0 && courseId > 0 && groupId > 0 && semesterId > 0;
  const scope = { academicYearId: yearId, courseId, groupId, semesterId };

  const load = async (refresh = false) => {
    if (!scopeReady) return;
    setLoading(true);
    setError(null);
    try {
      const [ctx, ready, h, v, snaps, a] = await Promise.all([
        getAllocationContext(scope, refresh),
        getAllocationReadiness(scope),
        getAllocationHealth(scope),
        getAllocationValidation(scope),
        listAllocationSnapshots(scope),
        getAllocationArchitectureReport(),
      ]);
      setContext(ctx.data);
      setReadiness(ready.data);
      setHealth(h.data);
      setValidation(v.data);
      setSnapshots(snaps.data);
      setArch(a.data.passed ? "Passed" : `Failed: ${(a.data.violations || []).join("; ")}`);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  };

  const snapshot = async () => {
    try {
      await createAllocationSnapshot(scope);
      const snaps = await listAllocationSnapshots(scope);
      setSnapshots(snaps.data);
    } catch (e) {
      setError(errMsg(e));
    }
  };

  if (!canView) {
    return (
      <Box sx={{ p: 2 }}>
        <Alert severity="warning">Section.View permission required.</Alert>
      </Box>
    );
  }

  return (
    <Box sx={{ p: 2, maxWidth: 1200, mx: "auto" }}>
      <Button component={RouterLink} to="/setup/sections" startIcon={<ArrowBackIcon />} sx={{ mb: 1 }}>
        Sections
      </Button>
      <Typography variant="h5" sx={{ fontWeight: 800, mb: 0.5 }}>
        Allocation Context Explorer
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Read-only AI29.1B.7 platform. No student allocation is performed here.
      </Typography>

      {error && (
        <Alert severity="error" sx={{ mb: 1.5 }} onClose={() => setError(null)}>
          {error}
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
          {filteredGroups.map((g) => (
            <MenuItem key={g.id} value={g.id}>
              {g.name}
            </MenuItem>
          ))}
        </TextField>
        <TextField select size="small" label="Semester" value={semesterId || ""} onChange={(e) => setSemesterId(Number(e.target.value))} sx={{ minWidth: 140 }}>
          {filteredSemesters.map((s) => (
            <MenuItem key={s.id} value={s.id}>
              {s.name}
            </MenuItem>
          ))}
        </TextField>
        <Button variant="contained" disabled={!scopeReady || loading} onClick={() => void load(false)}>
          Load Context
        </Button>
        <Button variant="outlined" disabled={!scopeReady || loading} onClick={() => void load(true)}>
          Refresh
        </Button>
        <Button variant="outlined" disabled={!scopeReady || loading} onClick={() => void snapshot()}>
          Create Snapshot
        </Button>
      </Stack>

      {loading && <CircularProgress size={28} />}

      {context && (
        <Stack spacing={2}>
          <Alert severity="info">
            Context {context.contextId} · schema {context.schemaVersion} · checksum {context.checksum?.slice(0, 12)}… · health{" "}
            {context.overallHealth} · readiness {context.overallReadiness} · timetable {context.timetableStatus}
          </Alert>
          {arch && <Alert severity={arch === "Passed" ? "success" : "error"}>Architecture: {arch}</Alert>}

          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Hierarchy
          </Typography>
          <Typography variant="body2">
            {context.hierarchy.academicYearName} / {context.hierarchy.programName || "-"} / {context.hierarchy.courseName} /{" "}
            {context.hierarchy.groupName} / {context.hierarchy.semesterName}
          </Typography>

          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Sections & Capacity
          </Typography>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Section</TableCell>
                <TableCell>Type</TableCell>
                <TableCell>Lifecycle</TableCell>
                <TableCell>Health</TableCell>
                <TableCell>Readiness</TableCell>
                <TableCell>Capacity</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {context.sections.map((s) => {
                const cap = context.capacities.find((c) => c.sectionId === s.sectionId);
                return (
                  <TableRow key={s.sectionId}>
                    <TableCell>
                      {s.sectionCode} — {s.sectionName}
                    </TableCell>
                    <TableCell>{s.sectionType}</TableCell>
                    <TableCell>{s.lifecycle}</TableCell>
                    <TableCell>{s.health}</TableCell>
                    <TableCell>{s.readiness}</TableCell>
                    <TableCell>
                      {cap ? `${cap.currentStrength}/${cap.maximumCapacity} (${cap.occupancyPercent}%)` : "-"}
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>

          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Students ({context.students.length}) · Faculty ({context.facultyAssignments.length}) · Subjects (
            {context.subjectAssignments.length})
          </Typography>
          <Typography variant="body2">Rooms: {context.roomAvailability.map((r) => `${r.status} (${r.timetableMappingCount})`).join(", ")}</Typography>

          {readiness && (
            <>
              <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                Readiness — {readiness.overallStatus}
              </Typography>
              <Typography variant="body2">{readiness.checks.map((c) => `${c.area}:${c.status}`).join(" · ")}</Typography>
            </>
          )}
          {health && (
            <>
              <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                Health — {health.overallStatus}
              </Typography>
              <Typography variant="body2">{health.dimensions.map((d) => `${d.area}:${d.status}`).join(" · ")}</Typography>
            </>
          )}
          {validation && (
            <Alert severity={validation.isValid ? "success" : "error"}>
              Validation {validation.isValid ? "passed" : "failed"}
              {validation.errors?.length ? ` · ${validation.errors.join(" ")}` : ""}
              {validation.warnings?.length ? ` · warnings: ${validation.warnings.join(" ")}` : ""}
            </Alert>
          )}

          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Policies
          </Typography>
          <Typography variant="body2" component="pre" sx={{ whiteSpace: "pre-wrap" }}>
            {(context.policies || []).join("\n") || "None"}
          </Typography>

          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Snapshots
          </Typography>
          <Typography variant="body2">
            {snapshots.length === 0
              ? "No snapshots yet."
              : snapshots.map((s) => `${s.snapshotId} @ ${s.generatedDate} (${s.checksum?.slice(0, 8)}…)`).join("\n")}
          </Typography>
        </Stack>
      )}
    </Box>
  );
};

export default AllocationContextPage;
