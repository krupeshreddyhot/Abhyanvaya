import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useReducer,
  useRef,
  type ReactNode,
} from "react";
import { enrollmentApiClient } from "../api/enrollmentApiClient";
import { useAuth } from "./AuthContext";
import { useTenantContext } from "./TenantContextProvider";
import type {
  BatchDetailDto,
  BatchProgressDto,
  BatchSummary,
  CreateEnrollmentBatchApiRequest,
  EnrollmentConfigurationDto,
  EnrollmentDashboardDto,
  EnrollmentFilters,
  EnrollmentReadinessResult,
  EnrollmentSystemStatusDto,
  PagedResult,
} from "../types/enrollment";
import { getApiErrorMessage, getEnrollmentApiErrorMessage } from "../utils/apiErrorMessage";
import { BatchStatus } from "../types/enrollment";

type ToastState = { open: boolean; message: string; severity: "success" | "error" | "info" };

type EnrollmentState = {
  loading: boolean;
  error: string | null;
  dashboard: EnrollmentDashboardDto | null;
  systemStatus: EnrollmentSystemStatusDto | null;
  configuration: EnrollmentConfigurationDto | null;
  readiness: EnrollmentReadinessResult | null;
  batches: BatchSummary[];
  batchTotalCount: number;
  batchProgress: Record<string, BatchProgressDto>;
  selectedBatch: BatchDetailDto | null;
  collegeId: number | null;
  academicYear: number;
  toast: ToastState;
};

type EnrollmentAction =
  | { type: "SET_LOADING"; loading: boolean }
  | { type: "SET_ERROR"; error: string | null }
  | { type: "SET_DASHBOARD"; dashboard: EnrollmentDashboardDto; systemStatus: EnrollmentSystemStatusDto; configuration: EnrollmentConfigurationDto }
  | { type: "SET_READINESS"; readiness: EnrollmentReadinessResult }
  | { type: "SET_BATCHES"; result: PagedResult<BatchSummary> }
  | { type: "SET_PROGRESS"; progress: BatchProgressDto }
  | { type: "SET_SELECTED_BATCH"; batch: BatchDetailDto | null }
  | { type: "SET_COLLEGE"; collegeId: number | null }
  | { type: "SET_ACADEMIC_YEAR"; academicYear: number }
  | { type: "SHOW_TOAST"; message: string; severity: ToastState["severity"] }
  | { type: "HIDE_TOAST" };

const initialState = (): EnrollmentState => ({
  loading: true,
  error: null,
  dashboard: null,
  systemStatus: null,
  configuration: null,
  readiness: null,
  batches: [],
  batchTotalCount: 0,
  batchProgress: {},
  selectedBatch: null,
  collegeId: null,
  academicYear: new Date().getFullYear(),
  toast: { open: false, message: "", severity: "info" },
});

const reducer = (state: EnrollmentState, action: EnrollmentAction): EnrollmentState => {
  switch (action.type) {
    case "SET_LOADING":
      return { ...state, loading: action.loading };
    case "SET_ERROR":
      return { ...state, error: action.error };
    case "SET_DASHBOARD":
      return {
        ...state,
        dashboard: action.dashboard,
        systemStatus: action.systemStatus,
        configuration: action.configuration,
        error: null,
      };
    case "SET_READINESS":
      return { ...state, readiness: action.readiness };
    case "SET_BATCHES":
      return { ...state, batches: action.result.items, batchTotalCount: action.result.totalCount };
    case "SET_PROGRESS": {
      const progress = action.progress;
      const processed = progress.completed + progress.failed + progress.cancelled;
      const pending =
        progress.queued + progress.downloading + progress.validating + progress.embedding;
      return {
        ...state,
        batchProgress: { ...state.batchProgress, [progress.batchId]: progress },
        batches: state.batches.map((batch) =>
          batch.batchId === progress.batchId
            ? {
                ...batch,
                completedCount: progress.completed,
                uploadedWithoutEmbedding: progress.uploadedWithoutEmbedding,
                failedCount: progress.failed,
                pendingCount: pending,
                progressPercent: batch.totalStudents
                  ? (processed / batch.totalStudents) * 100
                  : batch.progressPercent,
              }
            : batch,
        ),
      };
    }
    case "SET_SELECTED_BATCH":
      return { ...state, selectedBatch: action.batch };
    case "SET_COLLEGE":
      return { ...state, collegeId: action.collegeId };
    case "SET_ACADEMIC_YEAR":
      return { ...state, academicYear: action.academicYear };
    case "SHOW_TOAST":
      return { ...state, toast: { open: true, message: action.message, severity: action.severity } };
    case "HIDE_TOAST":
      return { ...state, toast: { ...state.toast, open: false } };
    default:
      return state;
  }
};

type EnrollmentContextValue = EnrollmentState & {
  refreshDashboard: () => Promise<void>;
  refreshReadiness: (filters?: Partial<EnrollmentFilters>) => Promise<void>;
  refreshBatches: (filters?: EnrollmentFilters) => Promise<void>;
  loadBatchDetail: (batchId: string) => Promise<void>;
  createBatch: (request: CreateEnrollmentBatchApiRequest) => Promise<boolean>;
  cancelBatch: (batchId: string) => Promise<boolean>;
  retryBatch: (batchId: string) => Promise<boolean>;
  hideToast: () => void;
  canManage: boolean;
};

const EnrollmentDashboardContext = createContext<EnrollmentContextValue | null>(null);

export const EnrollmentDashboardProvider = ({ children }: { children: ReactNode }) => {
  const { token, user, hasPermission } = useAuth();
  const { context, hasOperationalContext, subscribe } = useTenantContext();
  const [state, dispatch] = useReducer(reducer, undefined, initialState);
  const progressRefreshTimer = useRef<number | null>(null);
  const canManage = hasPermission("Enrollment.Manage") || user?.role === "SuperAdmin";

  const refreshDashboard = useCallback(async () => {
    dispatch({ type: "SET_LOADING", loading: true });
    try {
      const res = await enrollmentApiClient.getDashboard(state.collegeId ?? undefined);
      dispatch({
        type: "SET_DASHBOARD",
        dashboard: res.data.dashboard,
        systemStatus: res.data.systemStatus,
        configuration: res.data.configuration,
      });
    } catch (err) {
      dispatch({ type: "SET_ERROR", error: getApiErrorMessage(err) });
    } finally {
      dispatch({ type: "SET_LOADING", loading: false });
    }
  }, [state.collegeId]);

  const refreshReadiness = useCallback(
    async (filters?: Partial<EnrollmentFilters>) => {
      if (!state.collegeId) return;
      try {
        const res = await enrollmentApiClient.getReadiness({
          collegeId: state.collegeId,
          academicYear: filters?.academicYear ?? state.academicYear,
          courseId: filters?.courseId,
          groupId: filters?.groupId,
          batch: filters?.batch,
          subjectId: filters?.subjectId,
        });
        dispatch({ type: "SET_READINESS", readiness: res.data });
      } catch (err) {
        dispatch({ type: "SET_ERROR", error: getApiErrorMessage(err) });
      }
    },
    [state.collegeId, state.academicYear],
  );

  const refreshBatches = useCallback(
    async (filters: EnrollmentFilters = { page: 1, pageSize: 10 }) => {
      try {
        const res = await enrollmentApiClient.getHistory({
          ...filters,
          collegeId: state.collegeId ?? filters.collegeId,
        });
        dispatch({ type: "SET_BATCHES", result: res.data });

        const active = res.data.items.filter(
          (batch) => batch.status === BatchStatus.Created || batch.status === BatchStatus.Running,
        );
        await Promise.all(
          active.map(async (batch) => {
            try {
              const progressRes = await enrollmentApiClient.getBatchProgress(batch.batchId);
              dispatch({ type: "SET_PROGRESS", progress: progressRes.data });
            } catch {
              // Progress endpoint may be unavailable before batch starts; list still renders.
            }
          }),
        );
      } catch (err) {
        dispatch({ type: "SET_ERROR", error: getApiErrorMessage(err) });
      }
    },
    [state.collegeId],
  );

  const scheduleProgressRefresh = useCallback(() => {
    if (progressRefreshTimer.current) {
      window.clearTimeout(progressRefreshTimer.current);
    }
    progressRefreshTimer.current = window.setTimeout(() => {
      void refreshBatches();
      void refreshDashboard();
    }, 3000);
  }, [refreshBatches, refreshDashboard]);

  const loadBatchDetail = useCallback(async (batchId: string) => {
    try {
      const res = await enrollmentApiClient.getBatch(batchId);
      dispatch({ type: "SET_SELECTED_BATCH", batch: res.data });
      try {
        if (hasOperationalContext) {
          await enrollmentApiClient.subscribeTenant();
        }
        await enrollmentApiClient.subscribeBatch(batchId);
      } catch (subscribeErr) {
        dispatch({
          type: "SHOW_TOAST",
          message: getApiErrorMessage(subscribeErr),
          severity: "info",
        });
      }
    } catch (err) {
      dispatch({ type: "SHOW_TOAST", message: getApiErrorMessage(err), severity: "error" });
    }
  }, [hasOperationalContext]);

  const createBatch = useCallback(
    async (request: CreateEnrollmentBatchApiRequest) => {
      try {
        const res = await enrollmentApiClient.createBatch(request);
        if (!res.data.succeeded) {
          dispatch({
            type: "SHOW_TOAST",
            message: res.data.failureMessage ?? "Batch creation failed.",
            severity: "error",
          });
          return false;
        }
        dispatch({ type: "SHOW_TOAST", message: "Enrollment batch created.", severity: "success" });
        await refreshDashboard();
        await refreshBatches();
        await refreshReadiness();
        return true;
      } catch (err) {
        dispatch({
          type: "SHOW_TOAST",
          message: getEnrollmentApiErrorMessage(err, "Batch creation failed."),
          severity: "error",
        });
        return false;
      }
    },
    [refreshBatches, refreshDashboard, refreshReadiness],
  );

  const cancelBatch = useCallback(
    async (batchId: string) => {
      try {
        const res = await enrollmentApiClient.cancelBatch(batchId);
        dispatch({
          type: "SHOW_TOAST",
          message: res.data.message ?? (res.data.applied ? "Batch cancelled." : "Cancel not applied."),
          severity: res.data.applied ? "success" : "info",
        });
        await refreshBatches();
        await refreshDashboard();
        return res.data.applied;
      } catch (err) {
        dispatch({ type: "SHOW_TOAST", message: getApiErrorMessage(err), severity: "error" });
        return false;
      }
    },
    [refreshBatches, refreshDashboard],
  );

  const retryBatch = useCallback(
    async (batchId: string) => {
      try {
        const res = await enrollmentApiClient.retryBatch(batchId);
        dispatch({
          type: "SHOW_TOAST",
          message: res.data.message ?? (res.data.applied ? "Batch retry queued." : "Retry not applied."),
          severity: res.data.applied ? "success" : "info",
        });
        await refreshBatches();
        return res.data.applied;
      } catch (err) {
        dispatch({ type: "SHOW_TOAST", message: getApiErrorMessage(err), severity: "error" });
        return false;
      }
    },
    [refreshBatches],
  );

  useEffect(() => {
    const collegeId = context?.selectedCollegeId ?? null;
    dispatch({ type: "SET_COLLEGE", collegeId });
  }, [context?.selectedCollegeId]);

  useEffect(() => {
    if (!token || !state.collegeId || !hasOperationalContext) return;
    void refreshDashboard();
    void refreshReadiness();
    void refreshBatches();
  }, [token, state.collegeId, hasOperationalContext, refreshDashboard, refreshReadiness, refreshBatches]);

  useEffect(() => {
    const unsubChanged = subscribe("ContextChanged", () => {
      void refreshDashboard();
      void refreshReadiness();
      void refreshBatches();
    });
    const unsubCleared = subscribe("ContextCleared", () => {
      void refreshDashboard();
      void refreshBatches();
    });
    const unsubExpired = subscribe("ContextExpired", () => {
      void refreshDashboard();
      void refreshBatches();
    });
    const unsubRestored = subscribe("ContextRestored", () => {
      void refreshDashboard();
      void refreshReadiness();
      void refreshBatches();
    });
    return () => {
      unsubChanged();
      unsubCleared();
      unsubExpired();
      unsubRestored();
    };
  }, [subscribe, refreshDashboard, refreshReadiness, refreshBatches]);

  useEffect(() => {
    if (!token || !hasOperationalContext) return;
    void enrollmentApiClient.subscribeTenant();
  }, [token, hasOperationalContext, context?.selectedCollegeId]);

  useEffect(() => {
    if (!token) return;

    void enrollmentApiClient
      .connectSignalR(token, {
        onBatchCreated: () => {
          void refreshBatches();
          void refreshDashboard();
        },
        onBatchProgress: (progress: BatchProgressDto) => {
          dispatch({ type: "SET_PROGRESS", progress });
          scheduleProgressRefresh();
        },
        onBatchCompleted: () => {
          void refreshBatches();
          void refreshDashboard();
          void refreshReadiness();
        },
        onBatchFailed: () => {
          void refreshBatches();
          void refreshDashboard();
        },
        onBatchCancelled: () => {
          void refreshBatches();
          void refreshDashboard();
          void refreshReadiness();
        },
      })
      .then(async () => {
        if (hasOperationalContext) {
          await enrollmentApiClient.subscribeTenant();
        }
      });

    return () => {
      void enrollmentApiClient.disconnectSignalR();
    };
  }, [token, hasOperationalContext, refreshBatches, refreshDashboard, refreshReadiness, scheduleProgressRefresh]);

  const value = useMemo<EnrollmentContextValue>(
    () => ({
      ...state,
      refreshDashboard,
      refreshReadiness,
      refreshBatches,
      loadBatchDetail,
      createBatch,
      cancelBatch,
      retryBatch,
      hideToast: () => dispatch({ type: "HIDE_TOAST" }),
      canManage,
    }),
    [
      state,
      refreshDashboard,
      refreshReadiness,
      refreshBatches,
      loadBatchDetail,
      createBatch,
      cancelBatch,
      retryBatch,
      canManage,
    ],
  );

  return <EnrollmentDashboardContext.Provider value={value}>{children}</EnrollmentDashboardContext.Provider>;
};

export const useEnrollmentDashboard = (): EnrollmentContextValue => {
  const ctx = useContext(EnrollmentDashboardContext);
  if (!ctx) {
    throw new Error("useEnrollmentDashboard must be used within EnrollmentDashboardProvider");
  }
  return ctx;
};
