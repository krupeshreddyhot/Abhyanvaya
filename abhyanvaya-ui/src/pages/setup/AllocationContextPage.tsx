import { useState } from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import { AcademicPermissionAccess } from "../../auth/academicPermissionAccess";
import { PermissionKeys } from "../../auth/permissionKeys";
import { useAuth } from "../../context/AuthContext";
import { useAcademicUi } from "../../context/AcademicUiContext";
import {
  AcademicContextBreadcrumb,
  AcademicDataPanel,
  AcademicOperationalPageShell,
  AcademicScopeSelector,
  AcademicScopeToolbar,
  AcademicStatusChip,
  academicTouchButtonSx,
} from "../../components/academic";
import PermissionAwareButton from "../../components/common/PermissionAwareButton";
import PermissionDeniedAlert from "../../components/common/PermissionDeniedAlert";
import { isAcademicScopeReady } from "../../utils/academicSelectorFieldState";
import { getApiErrorMessage } from "../../utils/apiErrorMessage";
import {
  approveAllocation,
  compareAllocation,
  createAllocationSnapshot,
  getAllocationArchitectureReport,
  getAllocationContext,
  getAllocationDashboard,
  getAllocationHealth,
  getAllocationReadiness,
  getAllocationValidation,
  listAllocationSnapshots,
  runAllocation,
  type AllocationComparisonReport,
  type AllocationDashboardDto,
  type AllocationDraft,
  type AllocationExecutionResult,
  type AllocationHealthReport,
  type AllocationReadinessReport,
  type AllocationSnapshotDto,
  type AllocationValidationReport,
  type SectionAllocationContext,
} from "../../services/allocationPlatformService";

const errMsg = (e: unknown): string => getApiErrorMessage(e, "Request failed.");

const AllocationContextPage = () => {
  const { hasPermission, hasAnyPermission } = useAuth();
  const canView = hasAnyPermission([...AcademicPermissionAccess.allocation.contextAny]);
  const canRun = hasPermission(PermissionKeys.AllocationRun);
  const canApprove = hasPermission(PermissionKeys.AllocationApprove);
  const canCompare = hasPermission(PermissionKeys.AllocationScenarioCompare);
  const { selection } = useAcademicUi();

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [context, setContext] = useState<SectionAllocationContext | null>(null);
  const [readiness, setReadiness] = useState<AllocationReadinessReport | null>(null);
  const [health, setHealth] = useState<AllocationHealthReport | null>(null);
  const [validation, setValidation] = useState<AllocationValidationReport | null>(null);
  const [snapshots, setSnapshots] = useState<AllocationSnapshotDto[]>([]);
  const [arch, setArch] = useState<string>("");
  const [runResult, setRunResult] = useState<AllocationExecutionResult | null>(null);
  const [comparison, setComparison] = useState<AllocationComparisonReport | null>(null);
  const [draft, setDraft] = useState<AllocationDraft | null>(null);
  const [dashboard, setDashboard] = useState<AllocationDashboardDto | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const scopeReady = isAcademicScopeReady(selection);
  const scope = {
    academicYearId: selection.academicYearId!,
    courseId: selection.courseId!,
    groupId: selection.groupId!,
    semesterId: selection.semesterId!,
  };

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

  const runEngine = async () => {
    if (!scopeReady) return;
    setLoading(true);
    setError(null);
    setMessage(null);
    try {
      const res = await runAllocation({ ...scope, groupingMode: "Alphabetical" });
      setRunResult(res.data);
      setComparison(null);
      setDraft(null);
      const dash = await getAllocationDashboard();
      setDashboard(dash.data);
      setMessage(`Scenario ${res.data.scenarioId} generated (score ${res.data.score?.totalScore ?? 0}). No live student writes.`);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  };

  const compare = async () => {
    if (!runResult?.scenarioId) return;
    try {
      const res = await compareAllocation(runResult.scenarioId);
      setComparison(res.data);
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const approve = async () => {
    if (!runResult?.scenarioId) return;
    try {
      const res = await approveAllocation(runResult.scenarioId);
      setDraft(res.data);
      setMessage(res.data.note || "Draft created.");
    } catch (e) {
      setError(errMsg(e));
    }
  };

  if (!canView) {
    return (
      <Box sx={{ p: 2 }}>
        <PermissionDeniedAlert permissionKey={AcademicPermissionAccess.allocation.operationsView} />
      </Box>
    );
  }

  return (
    <AcademicOperationalPageShell
      title="Allocation Context Explorer"
      ariaLabel="Allocation context explorer"
      breadcrumb={<AcademicContextBreadcrumb />}
      subtitle="Read-only AI29.1B.7 platform. No student allocation is performed here."
      headerActions={
        <Button component={RouterLink} to="/setup/sections" startIcon={<ArrowBackIcon />} size="small" sx={academicTouchButtonSx}>
          Sections
        </Button>
      }
      error={error}
      onClearError={() => setError(null)}
      message={message}
      onClearMessage={() => setMessage(null)}
      toolbar={
        <AcademicScopeToolbar
          helpTitle="Allocation scope"
          helpBody="Load context for the selected Academic Year → Course → Group → Semester. Engine actions use existing allocation platform APIs."
          actions={
            <>
              <Button variant="contained" size="small" disabled={!scopeReady || loading} onClick={() => void load(false)} sx={academicTouchButtonSx}>
                Load Context
              </Button>
              <Button variant="outlined" size="small" disabled={!scopeReady || loading} onClick={() => void load(true)} sx={academicTouchButtonSx}>
                Refresh
              </Button>
              <Button variant="outlined" size="small" disabled={!scopeReady || loading} onClick={() => void snapshot()} sx={academicTouchButtonSx}>
                Snapshot
              </Button>
              <PermissionAwareButton
                allowed={canRun}
                permissionKey={AcademicPermissionAccess.allocation.run}
                variant="contained"
                color="secondary"
                size="small"
                disabled={!scopeReady || loading}
                disabledTooltip="Select a complete academic scope first."
                onClick={() => void runEngine()}
                sx={academicTouchButtonSx}
              >
                Run Engine
              </PermissionAwareButton>
              <PermissionAwareButton
                allowed={canCompare}
                permissionKey={AcademicPermissionAccess.allocationScenario.compare}
                variant="outlined"
                size="small"
                disabled={!runResult || loading}
                disabledTooltip="Run allocation first."
                onClick={() => void compare()}
                sx={academicTouchButtonSx}
              >
                Compare
              </PermissionAwareButton>
              <PermissionAwareButton
                allowed={canApprove}
                permissionKey={AcademicPermissionAccess.allocation.approve}
                variant="outlined"
                size="small"
                disabled={!runResult || loading}
                disabledTooltip="Run allocation first."
                onClick={() => void approve()}
                sx={academicTouchButtonSx}
              >
                Approve → Draft
              </PermissionAwareButton>
            </>
          }
        >
          <AcademicScopeSelector fields={["academicYear", "program", "course", "group", "semester"]} showCascadeHint />
        </AcademicScopeToolbar>
      }
    >
      {loading && !context ? (
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", py: 3, justifyContent: "center" }}>
          <CircularProgress size={28} aria-label="Loading allocation context" />
          <Typography variant="body2" color="text.secondary">
            Loading allocation context…
          </Typography>
        </Stack>
      ) : null}

      {context && (
        <Stack spacing={1.5}>
          <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap", alignItems: "center" }}>
            <AcademicStatusChip label={`Health: ${context.overallHealth}`} status={context.overallHealth} />
            <AcademicStatusChip label={`Readiness: ${context.overallReadiness}`} status={context.overallReadiness} />
            <AcademicStatusChip label={`Timetable: ${context.timetableStatus}`} status={context.timetableStatus} variant="outlined" />
            {arch ? <AcademicStatusChip label={`Architecture: ${arch}`} status={arch} /> : null}
          </Stack>
          <Alert severity="info" variant="outlined" sx={{ py: 0.5 }}>
            Context {context.contextId} · schema {context.schemaVersion} · checksum {context.checksum?.slice(0, 12)}…
          </Alert>

          <Typography variant="subtitle2" sx={{ fontWeight: 800 }}>
            Hierarchy
          </Typography>
          <Typography variant="body2">
            {context.hierarchy.academicYearName} / {context.hierarchy.programName || "-"} / {context.hierarchy.courseName} /{" "}
            {context.hierarchy.groupName} / {context.hierarchy.semesterName}
          </Typography>

          <AcademicDataPanel
            title="Sections & capacity"
            accent="academic"
            empty={context.sections.length === 0}
            emptyTitle="No sections in context"
            emptyDescription="Create sections for this scope, then reload context."
            helpTitle="Capacity"
            helpBody="Capacity and lifecycle come from the allocation platform context contract."
          >
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
                    <TableRow key={s.sectionId} hover>
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
          </AcademicDataPanel>

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

          {dashboard && (
            <Alert severity="info">
              Dashboard — runs {dashboard.totalRuns} · best score {dashboard.bestScore} · utilization{" "}
              {dashboard.averageCapacityUtilization}% · compliance {dashboard.averageConstraintCompliance}%
            </Alert>
          )}

          {runResult && (
            <>
              <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                Scenario Viewer — {runResult.scenarioId}
              </Typography>
              <Typography variant="body2">
                Score {runResult.score?.totalScore} · status {runResult.status} · recommendations{" "}
                {runResult.scenario?.recommendations?.length ?? 0}
              </Typography>
              <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                Strategy Trace
              </Typography>
              <Typography variant="body2" component="pre" sx={{ whiteSpace: "pre-wrap" }}>
                {(runResult.trace?.steps || [])
                  .map(
                    (s) =>
                      `${s.order}. ${s.strategyCode} ${s.executed ? "executed" : "skipped"} (${s.durationMs?.toFixed?.(1) ?? s.durationMs}ms) score=${s.scoreAfter}`,
                  )
                  .join("\n")}
              </Typography>
              <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                Constraint Trace
              </Typography>
              <Typography variant="body2">
                {(runResult.scenario?.constraints || [])
                  .map((c) => `${c.constraintCode}:${c.priority}:${c.satisfied ? "ok" : "fail"}`)
                  .join(" · ")}
              </Typography>
              <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                Score Breakdown
              </Typography>
              <Typography variant="body2">
                total={runResult.score?.totalScore} capacity={runResult.score?.capacityUtilization} policy=
                {runResult.score?.policyCompliance} gender={runResult.score?.genderBalance}
              </Typography>
              <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                Sample Explanations
              </Typography>
              <Typography variant="body2" component="pre" sx={{ whiteSpace: "pre-wrap" }}>
                {(runResult.scenario?.recommendations || [])
                  .slice(0, 5)
                  .map(
                    (r) =>
                      `${r.studentNumber || r.studentId} → ${r.toSectionCode}: ${(r.explanations || []).join("; ")}`,
                  )
                  .join("\n")}
              </Typography>
            </>
          )}
          {comparison && (
            <Alert severity="info">
              Compare — capacity Δ {comparison.capacityImprovement}pp · gender {comparison.genderBalanceScore} · policy{" "}
              {comparison.policyComplianceScore} · {comparison.summary}
            </Alert>
          )}
          {draft && (
            <Alert severity="warning">
              Draft {draft.draftId} ({draft.status}) — {draft.note}
            </Alert>
          )}
        </Stack>
      )}
    </AcademicOperationalPageShell>
  );
};

export default AllocationContextPage;
