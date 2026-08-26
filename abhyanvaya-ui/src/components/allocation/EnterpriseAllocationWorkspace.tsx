import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Stack,
  Step,
  StepLabel,
  Stepper,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import { AcademicPermissionAccess } from "../../auth/academicPermissionAccess";
import { PermissionKeys } from "../../auth/permissionKeys";
import PermissionDeniedAlert from "../common/PermissionDeniedAlert";
import { getApiErrorMessage } from "../../utils/apiErrorMessage";
import { useAuth } from "../../context/AuthContext";
import { AcademicContextBreadcrumb } from "../academic";
import { useAcademicUi } from "../../context/AcademicUiContext";
import { AcademicScopeSelector } from "../academic";
import { isAcademicScopeReady } from "../../utils/academicSelectorFieldState";
import {
  compareAllocation,
  DEFAULT_ALLOCATION_STRATEGIES,
  DEFAULT_CONSTRAINT_PRIORITIES,
  getAllocationConstraintPriorityDefaults,
  getAllocationContext,
  getAllocationHealth,
  getAllocationReadiness,
  getAllocationValidation,
  listAllocationGroupingModes,
  runAllocation,
  saveAllocationSandboxDraft,
  simulateAllocation,
  type AllocationComparisonReport,
  type AllocationExecutionResult,
  type AllocationHealthReport,
  type AllocationReadinessReport,
  type AllocationSandboxItem,
  type AllocationScope,
  type AllocationValidationReport,
  type SectionAllocationContext,
} from "../../services/allocationPlatformService";
import {
  approveAllocationScenario,
  archiveAllocationScenario,
  compareAllocationScenarios,
  getAllocationScenarioDetail,
  rejectAllocationScenario,
  replayAllocationScenario,
  reviewAllocationScenario,
  type AllocationGovernanceResult,
  type AllocationMultiCompareReport,
  type AllocationScenarioDetailDto,
} from "../../services/allocationOperationsService";
import StudentPopulationFilterPanel from "./StudentPopulationFilterPanel";
import AllocationStrategyConfigPanel from "./AllocationStrategyConfigPanel";
import AllocationCapacityPanel from "./AllocationCapacityPanel";
import CapacityViolationBanner from "./CapacityViolationBanner";
import AllocationPreviewPanel from "./AllocationPreviewPanel";
import AllocationPreviewErrorBoundary from "./AllocationPreviewErrorBoundary";
import AllocationGovernancePanel from "./AllocationGovernancePanel";
import {
  countPopulationFilter,
  countUnassignedMatches,
  DEFAULT_POPULATION_FILTER,
  isPopulationModeEnabled,
  takePopulationFilter,
  toAllocationPopulationSelection,
  validateLastThreeDigitsRange,
  validateStudentNumberRange,
  type PopulationFilterState,
} from "../../utils/allocationPopulationFilter";
import { ACADEMIC_UI_PAGE_SIZES, isAbortError, replaceAbortController } from "../../utils/academicRequest";
import type { ConstraintPriority } from "../../utils/allocationStrategyCatalog";
import { toGovernanceLifecycleDisplay } from "../../utils/allocationGovernanceLifecycle";
import {
  ALLOCATION_WORKSPACE_BANNER,
  MSG_ALLOCATION_ARCHIVED,
  MSG_ALLOCATION_CREATED,
  MSG_ALLOCATION_REJECTED,
  MSG_ALLOCATION_REVIEWED,
  MSG_COMPARE_COMPLETED,
  MSG_DRAFT_SAVED,
  MSG_NEED_PERMISSION_DRAFT,
  MSG_NEED_PREVIEW_OR_TEST,
  MSG_NEED_TEST_BEFORE_ALLOCATION,
  MSG_REPLAY_COMPLETED,
  MSG_REVIEW_SCOPE_THEN_GENERATE,
  MSG_TEST_ALLOCATION_ERRORS,
  MSG_TEST_ALLOCATION_SUCCESS,
  sanitizeAdministratorMessage,
} from "../../utils/allocationAdministratorCopy";
import {
  MSG_UNABLE_TO_LOAD_ELIGIBLE_SECTIONS,
  allocationScopeKey,
  canContinueWithTargetSections,
} from "../../utils/allocationTargetSectionSelection";

const WORKFLOW_STEPS = [
  "Academic Scope",
  "Student Population",
  "Allocation Rules",
  "Section Capacity",
  "Preview",
  "Simulation",
  "Allocation",
  "Review Allocation",
  "Approve Allocation",
] as const;

const errMsg = (e: unknown): string => getApiErrorMessage(e, "Request failed.");

/**
 * AI29.1D — Enterprise Section Allocation Workspace.
 * Orchestrates existing AI29.1C engine + AI29.1C.5A governance APIs only.
 * Does not compute allocation decisions in the UI.
 */
const EnterpriseAllocationWorkspace = () => {
  const { hasPermission } = useAuth();
  const { selection } = useAcademicUi();

  const canRun = hasPermission(PermissionKeys.AllocationRun);
  const canReview = hasPermission(PermissionKeys.AllocationScenarioReview);
  const canApprove = hasPermission(PermissionKeys.AllocationApprove);
  const canReject = hasPermission(PermissionKeys.AllocationReject);
  const canArchive = hasPermission(PermissionKeys.AllocationScenarioArchive);
  const canReplay = hasPermission(PermissionKeys.AllocationScenarioReplay);
  const canCompare = hasPermission(PermissionKeys.AllocationScenarioCompare);
  const canViewOps = hasPermission(PermissionKeys.AllocationOperationsView) || hasPermission(PermissionKeys.SectionView);
  const showTechnicalDetails = hasPermission(PermissionKeys.AllocationOperationsView);

  const [activeStep, setActiveStep] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const [context, setContext] = useState<SectionAllocationContext | null>(null);
  const [readiness, setReadiness] = useState<AllocationReadinessReport | null>(null);
  const [health, setHealth] = useState<AllocationHealthReport | null>(null);
  const [validation, setValidation] = useState<AllocationValidationReport | null>(null);

  const [groupingModes, setGroupingModes] = useState<string[]>(["Alphabetical"]);
  const [groupingMode, setGroupingMode] = useState("Alphabetical");
  const [strategies, setStrategies] = useState<Record<string, boolean>>({ ...DEFAULT_ALLOCATION_STRATEGIES });
  const [rollNumberBandSize, setRollNumberBandSize] = useState<number | null>(null);
  const [existingAssignmentPolicy, setExistingAssignmentPolicy] = useState<"PreserveExisting" | "Reallocate">(
    "PreserveExisting",
  );
  const [constraintPriorities, setConstraintPriorities] = useState<Record<string, ConstraintPriority>>({
    ...DEFAULT_CONSTRAINT_PRIORITIES,
  });
  const [combinedPresetActive, setCombinedPresetActive] = useState(false);
  const [comparison, setComparison] = useState<AllocationComparisonReport | null>(null);
  const [multiCompare, setMultiCompare] = useState<AllocationMultiCompareReport | null>(null);
  const [sandboxDraft, setSandboxDraft] = useState<AllocationSandboxItem | null>(null);

  const [simulation, setSimulation] = useState<AllocationExecutionResult | null>(null);
  const [execution, setExecution] = useState<AllocationExecutionResult | null>(null);
  const [scenarioDetail, setScenarioDetail] = useState<AllocationScenarioDetailDto | null>(null);
  const [governance, setGovernance] = useState<AllocationGovernanceResult | null>(null);
  const [reviewNotes, setReviewNotes] = useState("");
  const [rejectReason, setRejectReason] = useState("");
  const [populationFilter, setPopulationFilter] = useState<PopulationFilterState>({ ...DEFAULT_POPULATION_FILTER });
  /** null = all eligible sections; non-null = explicit target section ids. */
  const [targetSectionIds, setTargetSectionIds] = useState<number[] | null>(null);
  /** Fail-closed: Allocation Context / eligible sections load failed for current scope. */
  const [eligibleSectionsError, setEligibleSectionsError] = useState(false);
  const lastScopeKeyRef = useRef<string>("");

  const scopeReady = isAcademicScopeReady(selection);
  const scope: AllocationScope | null = scopeReady
    ? {
        academicYearId: selection.academicYearId!,
        courseId: selection.courseId!,
        groupId: selection.groupId!,
        semesterId: selection.semesterId!,
      }
    : null;
  const scopeKey = allocationScopeKey({
    academicYearId: selection.academicYearId,
    programId: selection.programId,
    courseId: selection.courseId,
    groupId: selection.groupId,
    semesterId: selection.semesterId,
  });

  const runRequest = useMemo(() => {
    if (!scope) return null;
    if (!isPopulationModeEnabled(context?.students ?? [], populationFilter.mode)) return null;
    return {
      ...scope,
      groupingMode,
      enabledStrategies: strategies,
      constraintPriorities,
      populationSelection: toAllocationPopulationSelection(populationFilter),
      targetSectionIds: targetSectionIds && targetSectionIds.length > 0 ? [...targetSectionIds].sort((a, b) => a - b) : null,
      rollNumberBandSize:
        strategies.RollNumberBands && rollNumberBandSize && rollNumberBandSize > 0 ? rollNumberBandSize : null,
      existingAssignmentPolicy,
    };
  }, [
    scope,
    groupingMode,
    strategies,
    constraintPriorities,
    populationFilter,
    targetSectionIds,
    context?.students,
    rollNumberBandSize,
    existingAssignmentPolicy,
  ]);

  const activeScenarioId = execution?.scenarioId || simulation?.scenarioId || scenarioDetail?.scenarioId || null;

  const contextStudents = useMemo(() => context?.students ?? [], [context?.students]);
  /** Prompt 19 — count/window matches; do not materialize full filtered arrays for UI. */
  const matchedCount = useMemo(
    () => countPopulationFilter(contextStudents, populationFilter),
    [contextStudents, populationFilter],
  );
  const unassignedMatchCount = useMemo(
    () => countUnassignedMatches(contextStudents, populationFilter),
    [contextStudents, populationFilter],
  );
  const matchedPreview = useMemo(
    () => takePopulationFilter(contextStudents, populationFilter, ACADEMIC_UI_PAGE_SIZES.allocationStudentPreview),
    [contextStudents, populationFilter],
  );
  const eligiblePreviewWindow = useMemo(
    () => takePopulationFilter(contextStudents, populationFilter, ACADEMIC_UI_PAGE_SIZES.allocationPreviewRows),
    [contextStudents, populationFilter],
  );
  const contextLoadAbortRef = useRef<AbortController | null>(null);

  const populationFilterValid = useMemo(() => {
    if (populationFilter.mode === "All") return true;
    if (populationFilter.mode === "StudentNumberRange") {
      return validateStudentNumberRange(populationFilter.fromStudentNumber, populationFilter.toStudentNumber).ok;
    }
    if (populationFilter.mode === "LastThreeDigitsRange") {
      return validateLastThreeDigitsRange(populationFilter.fromStudentNumber, populationFilter.toStudentNumber).ok;
    }
    return Boolean(populationFilter.facetValue.trim());
  }, [populationFilter]);

  useEffect(() => {
    void listAllocationGroupingModes()
      .then((res) => {
        const modes = res.data?.length ? res.data : ["Alphabetical"];
        setGroupingModes(modes);
        if (!modes.includes(groupingMode)) setGroupingMode(modes[0] ?? "Alphabetical");
      })
      .catch(() =>
        setGroupingModes([
          "Alphabetical",
          "StudentNumber",
          "LastThreeDigits",
          "Merit",
          "Gender",
          "Language",
          "Scholarship",
          "MinorSubject",
          "Hostel",
          "Transport",
          "ElectiveCombination",
        ]),
      );

    void getAllocationConstraintPriorityDefaults()
      .then((res) => {
        const data = res.data ?? {};
        const next: Record<string, ConstraintPriority> = { ...DEFAULT_CONSTRAINT_PRIORITIES };
        for (const [code, raw] of Object.entries(data)) {
          if (raw === "Mandatory" || raw === "Preferred" || raw === "Informational") {
            next[code] = raw;
          }
        }
        setConstraintPriorities(next);
      })
      .catch(() => setConstraintPriorities({ ...DEFAULT_CONSTRAINT_PRIORITIES }));
    // eslint-disable-next-line react-hooks/exhaustive-deps -- load once
  }, []);

  const loadContextBundle = useCallback(
    async (refresh = false) => {
      if (!scope) {
        setError("Select Academic Year, Course, Group, and Semester first.");
        return false;
      }
      const controller = replaceAbortController(contextLoadAbortRef.current);
      contextLoadAbortRef.current = controller;
      setLoading(true);
      setError(null);
      setEligibleSectionsError(false);
      try {
        // Parallel bundle — one round-trip set per scope (avoid N+1 readiness/health/validation).
        const [ctx, ready, h, v] = await Promise.all([
          getAllocationContext(scope, refresh),
          getAllocationReadiness(scope),
          getAllocationHealth(scope),
          getAllocationValidation(scope),
        ]);
        if (controller.signal.aborted) return false;
        setContext(ctx.data);
        setReadiness(ready.data);
        setHealth(h.data);
        setValidation(v.data);
        setPopulationFilter({ ...DEFAULT_POPULATION_FILTER });
        setTargetSectionIds(null);
        setEligibleSectionsError(false);
        return true;
      } catch (e) {
        if (isAbortError(e)) return false;
        // Fail-closed: drop previous scope's sections; do not keep stale Target Section list.
        setContext(null);
        setReadiness(null);
        setHealth(null);
        setValidation(null);
        setTargetSectionIds(null);
        setEligibleSectionsError(true);
        setError(`${MSG_UNABLE_TO_LOAD_ELIGIBLE_SECTIONS} ${errMsg(e)}`);
        return false;
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    },
    [scope],
  );

  // Prompt 4 — clear stale targetSectionIds whenever academic parent scope changes.
  useEffect(() => {
    if (scopeKey === lastScopeKeyRef.current) return;
    const hadPrior = lastScopeKeyRef.current !== "";
    lastScopeKeyRef.current = scopeKey;
    if (!hadPrior) return;
    setTargetSectionIds(null);
    setContext(null);
    setReadiness(null);
    setHealth(null);
    setValidation(null);
    setEligibleSectionsError(false);
    setSimulation(null);
    setExecution(null);
    setComparison(null);
    if (scopeReady && activeStep > 0) {
      void loadContextBundle(false);
    }
  }, [scopeKey, scopeReady, activeStep, loadContextBundle]);

  const loadScenarioDetail = useCallback(async (scenarioId: string) => {
    setLoading(true);
    setError(null);
    try {
      const res = await getAllocationScenarioDetail(scenarioId);
      setScenarioDetail(res.data);
      setGovernance(res.data.governance);
      return res.data;
    } catch (e) {
      setError(errMsg(e));
      return null;
    } finally {
      setLoading(false);
    }
  }, []);

  const canProceedFromStep = (step: number): boolean => {
    switch (step) {
      case 0:
        return scopeReady;
      case 1:
        return (
          Boolean(context) &&
          populationFilterValid &&
          matchedCount > 0 &&
          isPopulationModeEnabled(contextStudents, populationFilter.mode)
        );
      case 2:
        return Boolean(context) && !eligibleSectionsError;
      case 3:
        return (
          Boolean(context) &&
          !eligibleSectionsError &&
          canContinueWithTargetSections(targetSectionIds, context?.sections?.length ?? 0)
        );
      case 4:
        return Boolean(context) && !eligibleSectionsError;
      case 5:
        return Boolean(simulation?.succeeded || simulation?.scenarioId);
      case 6:
        return Boolean(activeScenarioId && (execution || simulation));
      case 7:
        return Boolean(activeScenarioId);
      default:
        return true;
    }
  };

  const goNext = async () => {
    setMessage(null);
    setError(null);

    if (activeStep === 0) {
      const ok = await loadContextBundle(false);
      if (!ok) return;
    }
    if (activeStep === 4 && !context) {
      const ok = await loadContextBundle(true);
      if (!ok) return;
    }
    if (activeStep === 5) {
      // Entering Scenario from Simulation — ensure we have engine output (no UI calculation).
      if (!simulation?.scenarioId && !execution?.scenarioId) {
        setError(MSG_NEED_TEST_BEFORE_ALLOCATION);
        return;
      }
    }
    if ((activeStep === 6 || activeStep === 7) && activeScenarioId) {
      await loadScenarioDetail(activeScenarioId);
    }

    setActiveStep((s) => Math.min(s + 1, WORKFLOW_STEPS.length - 1));
  };

  const goBack = () => setActiveStep((s) => Math.max(s - 1, 0));

  /**
   * AI29.1D.24B.4A.2 — Shared path for Preview and Test Allocation.
   * Both call POST /allocation/simulate (simulateAllocation). Backend semantics unchanged.
   * Distinction is UX only: Preview stays on step 4; Test Allocation advances to step 5.
   * Both store the same AllocationExecutionResult in `simulation` and render via AllocationPreviewPanel.
   */
  const doSimulate = async (opts?: { advanceToSimulationStep?: boolean }) => {
    if (!runRequest || !canRun) return false;
    setLoading(true);
    setError(null);
    setMessage(null);
    try {
      const res = await simulateAllocation(runRequest);
      setSimulation(res.data);
      setComparison(null);
      setMessage(res.data.succeeded ? MSG_TEST_ALLOCATION_SUCCESS : MSG_TEST_ALLOCATION_ERRORS);
      if (opts?.advanceToSimulationStep) {
        setActiveStep(5);
      }
      return true;
    } catch (e) {
      setError(errMsg(e));
      return false;
    } finally {
      setLoading(false);
    }
  };

  const doCompare = async () => {
    const scenarioId = execution?.scenarioId || simulation?.scenarioId;
    if (!scenarioId) {
      setError(MSG_NEED_PREVIEW_OR_TEST);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const res = await compareAllocation(scenarioId);
      setComparison(res.data);
      setMessage(sanitizeAdministratorMessage(res.data.summary || MSG_COMPARE_COMPLETED));
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  };

  const doSaveDraft = async (name: string) => {
    const scenarioId = execution?.scenarioId || simulation?.scenarioId;
    if (!scenarioId) {
      setError(MSG_NEED_PREVIEW_OR_TEST);
      return;
    }
    if (!canRun) {
      setError(MSG_NEED_PERMISSION_DRAFT);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const res = await saveAllocationSandboxDraft(
        scenarioId,
        name || `Draft ${new Date().toLocaleDateString()}`,
        "allocation-preview",
      );
      setSandboxDraft(res.data);
      setMessage(MSG_DRAFT_SAVED);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  };

  const doGenerateScenario = async () => {
    if (!runRequest || !canRun) return;
    setLoading(true);
    setError(null);
    setMessage(null);
    try {
      const res = await runAllocation(runRequest);
      setExecution(res.data);
      if (res.data.scenarioId) {
        await loadScenarioDetail(res.data.scenarioId);
      }
      setMessage(
        res.data.score?.totalScore != null
          ? `${MSG_ALLOCATION_CREATED} Allocation score: ${res.data.score.totalScore}.`
          : MSG_ALLOCATION_CREATED,
      );
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  };

  const doReview = async () => {
    if (!activeScenarioId || !canReview) return;
    setLoading(true);
    setError(null);
    try {
      const res = await reviewAllocationScenario(activeScenarioId, reviewNotes || undefined);
      setGovernance(res.data);
      setMessage(sanitizeAdministratorMessage(res.data.message || MSG_ALLOCATION_REVIEWED));
      await loadScenarioDetail(activeScenarioId);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  };

  const doApprove = async () => {
    if (!activeScenarioId || !canApprove) return;
    // Approval eligibility is decided only by the governance service (canApprove / blockingReasons).
    const latest = await loadScenarioDetail(activeScenarioId);
    if (latest?.governance && latest.governance.canApprove === false) {
      setError(
        sanitizeAdministratorMessage(
          (latest.governance.blockingReasons?.length
            ? latest.governance.blockingReasons.join(" · ")
            : latest.governance.message) || "Approval is currently unavailable. Please refresh and try again.",
        ),
      );
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const res = await approveAllocationScenario(activeScenarioId);
      setGovernance(res.data);
      if (!res.data.success || res.data.canApprove === false) {
        setError(
          sanitizeAdministratorMessage(
            (res.data.blockingReasons?.length ? res.data.blockingReasons.join(" · ") : res.data.message) ||
              "Approval is currently unavailable. Please refresh and try again.",
          ),
        );
      } else {
        setMessage(
          sanitizeAdministratorMessage(
            `${res.data.message || "Allocation approved."} Approval advances the allocation workflow and does not by itself permanently move students.`,
          ),
        );
      }
      await loadScenarioDetail(activeScenarioId);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  };

  const doReject = async () => {
    if (!activeScenarioId || !canReject) return;
    setLoading(true);
    setError(null);
    try {
      const res = await rejectAllocationScenario(activeScenarioId, rejectReason || undefined);
      setGovernance(res.data);
      setMessage(sanitizeAdministratorMessage(res.data.message || MSG_ALLOCATION_REJECTED));
      await loadScenarioDetail(activeScenarioId);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  };

  const doArchive = async () => {
    if (!activeScenarioId || !canArchive) return;
    setLoading(true);
    setError(null);
    try {
      const res = await archiveAllocationScenario(activeScenarioId);
      setGovernance(res.data);
      setMessage(sanitizeAdministratorMessage(res.data.message || MSG_ALLOCATION_ARCHIVED));
      await loadScenarioDetail(activeScenarioId);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  };

  const doReplay = async () => {
    if (!activeScenarioId || !canReplay) return;
    setLoading(true);
    setError(null);
    try {
      const res = await replayAllocationScenario(activeScenarioId);
      setExecution(res.data);
      setSimulation(res.data);
      setMessage(MSG_REPLAY_COMPLETED);
      await loadScenarioDetail(res.data.scenarioId || activeScenarioId);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  };

  const doGovernanceCompare = async () => {
    if (!activeScenarioId || !canCompare) return;
    setLoading(true);
    setError(null);
    try {
      const [engineCmp, multi] = await Promise.all([
        compareAllocation(activeScenarioId),
        compareAllocationScenarios([activeScenarioId]),
      ]);
      setComparison(engineCmp.data);
      setMultiCompare(multi.data);
      setMessage(
        sanitizeAdministratorMessage(multi.data.summary || engineCmp.data.summary || MSG_COMPARE_COMPLETED),
      );
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  };

  /** Formal Generate Allocation result preferred for later governance steps. */
  const engineResult = execution ?? simulation;
  /**
   * Preview / Test Allocation must show the simulate response, not a stale Generate Allocation result.
   * Recommendations live at result.scenario.recommendations (not result.recommendations).
   */
  const previewSimulationResult = simulation ?? execution;
  const sectionSummaries = engineResult?.scenario?.sectionSummaries ?? [];
  const engineConstraints = engineResult?.scenario?.constraints ?? [];

  if (!canViewOps) {
    return <PermissionDeniedAlert permissionKey={AcademicPermissionAccess.allocation.operationsView} />;
  }

  return (
    <Box>
      <Typography variant="h6" sx={{ fontWeight: 800, mb: 0.5 }}>
        Section Allocation
      </Typography>
      <AcademicContextBreadcrumb />
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5, mt: 0.5 }}>
        {ALLOCATION_WORKSPACE_BANNER}
      </Typography>

      <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap", mb: 2 }}>
        {showTechnicalDetails && (
          <>
            <Button component={RouterLink} to="/setup/academic/allocation-context" size="small">
              Academic Context Explorer
            </Button>
            <Button component={RouterLink} to="/setup/academic/allocation/operations" size="small">
              Allocation Operations
            </Button>
          </>
        )}
      </Stack>

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

      <Stepper
        activeStep={activeStep}
        alternativeLabel
        sx={{
          mb: 3,
          overflowX: { xs: "auto", md: "visible" },
          pb: { xs: 1, md: 0 },
          "& .MuiStepLabel-label": { typography: { xs: "caption", sm: "body2" } },
        }}
      >
        {WORKFLOW_STEPS.map((label) => (
          <Step key={label}>
            <StepLabel>{label}</StepLabel>
          </Step>
        ))}
      </Stepper>

      {loading && (
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", mb: 2 }}>
          <CircularProgress size={18} />
          <Typography variant="body2" color="text.secondary">
            Working on your allocation…
          </Typography>
        </Stack>
      )}

      {/* Step 0 — Academic Scope */}
      {activeStep === 0 && (
        <Stack spacing={1.5}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Academic Scope
          </Typography>
          <AcademicScopeSelector
            fields={["academicYear", "program", "course", "group", "semester"]}
            showCascadeHint
            showError={false}
          />
          {!scopeReady && (
            <Alert severity="info">Complete Year → Course → Group → Semester (Program when enabled) to continue.</Alert>
          )}
        </Stack>
      )}

      {/* Step 1 — Student Population (from SectionAllocationContext) */}
      {activeStep === 1 && (
        <Stack spacing={1.5}>
          <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap", alignItems: "center" }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
              Student Population
            </Typography>
            <Button size="small" variant="outlined" onClick={() => void loadContextBundle(true)} disabled={!scope || loading}>
              Refresh Students
            </Button>
            <Chip size="small" label={`Unassigned (match): ${unassignedMatchCount}`} />
          </Stack>
          {!context ? (
            <Alert severity="warning">Load academic scope to view the student population.</Alert>
          ) : (
            <>
              <StudentPopulationFilterPanel
                students={contextStudents}
                filter={populationFilter}
                onChange={setPopulationFilter}
              />
              {matchedCount === 0 ? (
                <Alert severity="info">
                  {contextStudents.length === 0
                    ? "Allocation Context has no eligible students for this scope."
                    : "No students match. Reset filters or choose a different population filter."}
                </Alert>
              ) : (
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Student #</TableCell>
                      <TableCell>Name</TableCell>
                      <TableCell>Gender</TableCell>
                      <TableCell>Language</TableCell>
                      <TableCell>Current Section</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {matchedPreview.map((s) => (
                      <TableRow key={s.studentId}>
                        <TableCell>{s.studentNumber ?? s.studentId}</TableCell>
                        <TableCell>{s.studentName ?? "—"}</TableCell>
                        <TableCell>{s.gender ?? "—"}</TableCell>
                        <TableCell>{s.language ?? "—"}</TableCell>
                        <TableCell>{s.currentSectionCode ?? "—"}</TableCell>
                      </TableRow>
                    ))}
                    {matchedCount > matchedPreview.length && (
                      <TableRow>
                        <TableCell colSpan={5}>
                          <Typography variant="caption" color="text.secondary">
                            Showing first {matchedPreview.length} of {matchedCount} matching students.
                          </Typography>
                        </TableCell>
                      </TableRow>
                    )}
                  </TableBody>
                </Table>
              )}
            </>
          )}
        </Stack>
      )}

      {/* Step 2 — Allocation Strategy (config only — engine executes) */}
      {activeStep === 2 && (
        <Stack spacing={1.5}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Allocation Rules
          </Typography>
          <AllocationStrategyConfigPanel
            groupingModes={groupingModes}
            groupingMode={groupingMode}
            onGroupingModeChange={setGroupingMode}
            strategies={strategies}
            onStrategiesChange={setStrategies}
            rollNumberBandSize={rollNumberBandSize}
            onRollNumberBandSizeChange={setRollNumberBandSize}
            existingAssignmentPolicy={existingAssignmentPolicy}
            onExistingAssignmentPolicyChange={setExistingAssignmentPolicy}
            constraintPriorities={constraintPriorities}
            onConstraintPrioritiesChange={setConstraintPriorities}
            combinedPresetActive={combinedPresetActive}
            onCombinedPresetChange={setCombinedPresetActive}
            showTechnicalDetails={showTechnicalDetails}
            targetSectionCapacities={(context?.capacities ?? []).map((c) => ({
              sectionId: c.sectionId,
              maximumCapacity: c.maximumCapacity ?? 0,
            }))}
          />
        </Stack>
      )}

      {/* Step 3 — Section Capacity (Section Capacity Engine authoritative) */}
      {activeStep === 3 && scope && (
        <AllocationCapacityPanel
          academicYearId={scope.academicYearId}
          semesterId={scope.semesterId}
          context={context}
          constraints={engineConstraints}
          proposedSummaries={sectionSummaries}
          targetSectionIds={targetSectionIds}
          onTargetSectionIdsChange={setTargetSectionIds}
          eligibleSectionsError={eligibleSectionsError || !context}
          onRetryEligibleSections={() => void loadContextBundle(true)}
        />
      )}

      {/* Step 4 — Allocation Preview (shared simulate result via AllocationPreviewPanel) */}
      {activeStep === 4 && (
        <AllocationPreviewErrorBoundary
          onReset={() => setSimulation(null)}
          onPreview={() => {
            setSimulation(null);
            void doSimulate();
          }}
          onTestAllocation={() => {
            setSimulation(null);
            void doSimulate({ advanceToSimulationStep: true });
          }}
        >
          <AllocationPreviewPanel
            result={previewSimulationResult}
            groupingMode={groupingMode}
            eligibleStudents={eligiblePreviewWindow}
            eligibleStudentCount={matchedCount}
            loading={loading}
            canRun={canRun && Boolean(runRequest)}
            comparison={comparison}
            draft={sandboxDraft}
            readinessStrip={
              <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
                <Chip size="small" label={`Scope health: ${context?.overallHealth ?? health?.overallStatus ?? "—"}`} />
                <Chip size="small" label={`Readiness: ${readiness?.overallStatus ?? context?.overallReadiness ?? "—"}`} />
                <Chip
                  size="small"
                  color={validation?.isValid === false ? "error" : "default"}
                  label={`Checks: ${validation?.isValid == null ? "—" : validation.isValid ? "Ready" : "Issues found"}`}
                />
                <Button size="small" variant="text" onClick={() => void loadContextBundle(true)} disabled={loading || !scope}>
                  Refresh Scope
                </Button>
              </Stack>
            }
            showTechnicalDetails={showTechnicalDetails}
            onPreview={() => void doSimulate()}
            onSimulation={() => void doSimulate({ advanceToSimulationStep: true })}
            onCompare={() => void doCompare()}
            onBack={goBack}
            onSaveDraft={(name) => void doSaveDraft(name)}
          />
        </AllocationPreviewErrorBoundary>
      )}

      {/* Step 5 — Simulation / Test Allocation (same /allocation/simulate + same panel) */}
      {activeStep === 5 && (
        <Stack spacing={1.5}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Simulation
          </Typography>
          <Alert severity="info">
            See how students would be distributed across sections without changing student records.
          </Alert>
          {canRun ? (
            <Button variant="contained" onClick={() => void doSimulate()} disabled={!runRequest || loading}>
              Test Allocation
            </Button>
          ) : (
            <Alert severity="warning">You need permission to run allocation tests.</Alert>
          )}
          {simulation && (
            <AllocationPreviewErrorBoundary
              onReset={() => setSimulation(null)}
              onPreview={() => {
                setSimulation(null);
                void doSimulate();
              }}
              onTestAllocation={() => {
                setSimulation(null);
                void doSimulate();
              }}
            >
              <AllocationPreviewPanel
                result={simulation}
                groupingMode={groupingMode}
                eligibleStudents={eligiblePreviewWindow}
                eligibleStudentCount={matchedCount}
                loading={loading}
                canRun={canRun && Boolean(runRequest)}
                comparison={comparison}
                draft={sandboxDraft}
                showTechnicalDetails={showTechnicalDetails}
                onPreview={() => void doSimulate()}
                onSimulation={() => void doSimulate()}
                onCompare={() => void doCompare()}
                onBack={() => setActiveStep(4)}
                onSaveDraft={(name) => void doSaveDraft(name)}
              />
            </AllocationPreviewErrorBoundary>
          )}
        </Stack>
      )}

      {/* Step 6 — Allocation (scenario) */}
      {activeStep === 6 && (
        <Stack spacing={1.5}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Allocation
          </Typography>
          <Alert severity="info">
            Create a formal allocation from your selected academic scope, students, allocation rules, and section
            capacity. You may also continue with a tested allocation for review.
          </Alert>
          <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
            {canRun && (
              <Button variant="contained" onClick={() => void doGenerateScenario()} disabled={!runRequest || loading}>
                Generate Allocation
              </Button>
            )}
            {simulation?.scenarioId && (
              <Button
                variant="outlined"
                onClick={() => void loadScenarioDetail(simulation.scenarioId)}
                disabled={loading}
              >
                Use Tested Allocation
              </Button>
            )}
          </Stack>
          {(execution || scenarioDetail) && (
            <Stack spacing={1}>
              <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
                <Chip
                  color="info"
                  label={`Status: ${toGovernanceLifecycleDisplay(scenarioDetail?.lifecycleStatus)}`}
                />
                <Chip
                  label={`Allocation Score: ${scenarioDetail?.totalScore ?? execution?.score?.totalScore ?? "—"}`}
                />
              </Stack>
              <CapacityViolationBanner
                constraints={execution?.scenario?.constraints ?? simulation?.scenario?.constraints}
                proposedSummaries={sectionSummaries}
              />
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Section</TableCell>
                    <TableCell>Assigned</TableCell>
                    <TableCell>Capacity</TableCell>
                    <TableCell>Occupancy</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {sectionSummaries.map((s) => (
                    <TableRow key={s.sectionId}>
                      <TableCell>{s.sectionCode}</TableCell>
                      <TableCell>{s.assignedCount}</TableCell>
                      <TableCell>{s.maximumCapacity}</TableCell>
                      <TableCell>{s.occupancyPercent}%</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Stack>
          )}
        </Stack>
      )}

      {/* Step 7 — Review Allocation */}
      {activeStep === 7 && (
        <Stack spacing={1.5}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Review Allocation
          </Typography>
          {!activeScenarioId && (
            <Alert severity="warning">No allocation is loaded yet — complete Simulation / Allocation first.</Alert>
          )}
          <CapacityViolationBanner constraints={engineConstraints} proposedSummaries={sectionSummaries} />
          <AllocationGovernancePanel
            scenarioDetail={scenarioDetail}
            executionStatus={execution?.status ?? simulation?.status}
            executionResult={engineResult}
            governance={governance}
            reviewNotes={reviewNotes}
            rejectReason={rejectReason}
            onReviewNotesChange={setReviewNotes}
            onRejectReasonChange={setRejectReason}
            loading={loading}
            canReview={canReview}
            canApprove={canApprove}
            canReject={canReject}
            canArchive={canArchive}
            canReplay={canReplay}
            canCompare={canCompare}
            engineCompare={comparison}
            multiCompare={multiCompare}
            showTechnicalDetails={showTechnicalDetails}
            onRebuildAllocation={() => {
              setActiveStep(0);
              setMessage(MSG_REVIEW_SCOPE_THEN_GENERATE);
            }}
            onBack={goBack}
            onRefresh={() => activeScenarioId && void loadScenarioDetail(activeScenarioId)}
            onReview={() => void doReview()}
            onApprove={() => void doApprove()}
            onReject={() => void doReject()}
            onArchive={() => void doArchive()}
            onReplay={() => void doReplay()}
            onCompare={() => void doGovernanceCompare()}
          />
        </Stack>
      )}

      {/* Step 8 — Approve Allocation (same governance authority) */}
      {activeStep === 8 && (
        <Stack spacing={1.5}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Approve Allocation
          </Typography>
          <Alert severity="info">
            Confirm that you have reviewed the proposed student section assignments. Approval follows the server
            workflow and does not by itself permanently move students.
          </Alert>
          <CapacityViolationBanner constraints={engineConstraints} proposedSummaries={sectionSummaries} />
          <AllocationGovernancePanel
            scenarioDetail={scenarioDetail}
            executionStatus={execution?.status ?? simulation?.status}
            executionResult={engineResult}
            governance={governance}
            reviewNotes={reviewNotes}
            rejectReason={rejectReason}
            onReviewNotesChange={setReviewNotes}
            onRejectReasonChange={setRejectReason}
            loading={loading}
            canReview={canReview}
            canApprove={canApprove}
            canReject={canReject}
            canArchive={canArchive}
            canReplay={canReplay}
            canCompare={canCompare}
            engineCompare={comparison}
            multiCompare={multiCompare}
            showTechnicalDetails={showTechnicalDetails}
            onRebuildAllocation={() => {
              setActiveStep(0);
              setMessage(MSG_REVIEW_SCOPE_THEN_GENERATE);
            }}
            onBack={goBack}
            onRefresh={() => activeScenarioId && void loadScenarioDetail(activeScenarioId)}
            onReview={() => void doReview()}
            onApprove={() => void doApprove()}
            onReject={() => void doReject()}
            onArchive={() => void doArchive()}
            onReplay={() => void doReplay()}
            onCompare={() => void doGovernanceCompare()}
          />
        </Stack>
      )}

      <Stack direction="row" spacing={1} sx={{ mt: 3, justifyContent: "space-between" }}>
        <Button onClick={goBack} disabled={activeStep === 0 || loading}>
          Back
        </Button>
        {activeStep < WORKFLOW_STEPS.length - 1 ? (
          <Button
            variant="contained"
            onClick={() => void goNext()}
            disabled={loading || !canProceedFromStep(activeStep)}
          >
            Next
          </Button>
        ) : (
          <Typography variant="caption" color="text.secondary">
            Workflow complete — use Allocation Operations for archive, compare, or replay if needed.
          </Typography>
        )}
      </Stack>
    </Box>
  );
};

export default EnterpriseAllocationWorkspace;
