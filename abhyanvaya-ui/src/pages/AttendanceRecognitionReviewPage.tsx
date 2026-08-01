import axios from "axios";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Paper,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import {
  AssignStudentDialog,
  AttendanceFinalizationSuccess,
  FinalizationSummaryCard,
  FinalizeAttendanceDialog,
  RecognitionReviewPanel,
  RejectReasonDialog,
} from "../components/attendance-recognition";
import { useRecognitionReviewKeyboard } from "../hooks/useRecognitionReviewKeyboard";
import { useReviewUndoRedo } from "../hooks/useReviewUndoRedo";
import {
  finalizeAttendanceSession,
  getAttendanceSession,
  getFinalizationStatus,
  getRecognitionSummary,
  getSessionAuditEntries,
  getSessionRecognitions,
  getSessionReviewHistory,
  mergeReviewUpdate,
  RecognitionReviewAction,
  reviewRecognition,
  reviewRecognitionBatch,
  type AttendanceBuildSummaryDto,
  type AttendanceRecognitionReviewDto,
  type AttendanceSessionReviewDto,
  type AuditEntryDto,
  type FinalizationStatusDto,
  type RecognitionReviewActionValue,
  type RecognitionSummaryDto,
  type AttendanceRecognitionReviewHistoryDto,
} from "../services/attendanceRecognitionService";
import { useReviewFullscreen } from "../context/ReviewFullscreenContext";
import { listClassroomImages, reorderClassroomImages } from "../services/attendanceSessionService";
import type { AttendanceSessionImage } from "../types/sessionImage";
import { mediaAssetUrl } from "../utils/mediaAssetUrl";
import {
  filterRecognitions,
  type RecognitionReviewFilter,
} from "../utils/recognitionReviewFilters";
import { isPendingReview } from "../utils/recognitionStatus";
import {
  buildReviewAnalytics,
  buildSessionProductivity,
} from "../utils/reviewAnalytics";
import {
  getLastImageSequence,
  loadReviewWorkspacePrefs,
  saveReviewWorkspacePrefs,
  setLastImageSequence,
} from "../utils/reviewWorkspacePrefs";
import {
  applySmartQueue,
  countBySmartCategory,
  estimateReviewMinutesRemaining,
  type SmartQueueCategory,
} from "../utils/smartReviewQueue";

const SESSION_APPROVED_STATUS = 4;

const FINALIZE_PROGRESS_MESSAGES = [
  "Validating",
  "Building attendance",
  "Saving attendance",
  "Completing session",
] as const;

function errMsg(error: unknown, fallback: string): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data;
    if (typeof data === "string" && data.trim()) {
      return data;
    }
    if (data && typeof data === "object" && "title" in data && typeof data.title === "string") {
      return data.title;
    }
  }

  return fallback;
}

function formatElapsed(ms: number): string {
  const totalSec = Math.floor(ms / 1000);
  const m = Math.floor(totalSec / 60);
  const s = totalSec % 60;
  return `${m}:${s.toString().padStart(2, "0")}`;
}

type RejectTarget = {
  recognitionIds: string[];
  batch: boolean;
};

const AttendanceRecognitionReviewPage = () => {
  const { sessionId = "" } = useParams<{ sessionId: string }>();
  const navigate = useNavigate();
  const { fullscreen, setFullscreen, toggleFullscreen } = useReviewFullscreen();
  const initialPrefs = useMemo(() => loadReviewWorkspacePrefs(), []);

  const [session, setSession] = useState<AttendanceSessionReviewDto | null>(null);
  const [recognitions, setRecognitions] = useState<AttendanceRecognitionReviewDto[]>([]);
  const [sessionImages, setSessionImages] = useState<AttendanceSessionImage[]>([]);
  const [activeImageSequence, setActiveImageSequence] = useState(1);
  const [summary, setSummary] = useState<RecognitionSummaryDto | null>(null);
  const [finalizationStatus, setFinalizationStatus] = useState<FinalizationStatusDto | null>(null);
  const [history, setHistory] = useState<AttendanceRecognitionReviewHistoryDto[]>([]);
  const [auditEntries, setAuditEntries] = useState<AuditEntryDto[]>([]);
  const [notesById, setNotesById] = useState<Record<string, string>>({});
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [focusedId, setFocusedId] = useState<string | null>(null);
  const [activeFilters, setActiveFilters] = useState<Set<RecognitionReviewFilter>>(new Set());
  const [searchText, setSearchText] = useState("");
  const [hideHighConfidence, setHideHighConfidence] = useState(false);
  const [assignRecognitionId, setAssignRecognitionId] = useState<string | null>(null);
  const [rejectTarget, setRejectTarget] = useState<RejectTarget | null>(null);
  const [heatMapEnabled, setHeatMapEnabled] = useState(initialPrefs.heatMapEnabled);
  const [heatMapOpacity, setHeatMapOpacity] = useState(initialPrefs.heatMapOpacity);
  const [miniMapVisible, setMiniMapVisible] = useState(initialPrefs.miniMapVisible);
  const [smartQueueCategory, setSmartQueueCategory] = useState<SmartQueueCategory | "all">("all");
  const [smartQueueOnlyPending, setSmartQueueOnlyPending] = useState(initialPrefs.smartQueueOnlyPending);
  const [shortcutHelpOpen, setShortcutHelpOpen] = useState(false);

  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [finalizing, setFinalizing] = useState(false);
  const [finalizeDialogOpen, setFinalizeDialogOpen] = useState(false);
  const [finalizeProgressIndex, setFinalizeProgressIndex] = useState(0);
  const [finalizeResult, setFinalizeResult] = useState<AttendanceBuildSummaryDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [elapsedMs, setElapsedMs] = useState(0);
  const sessionStartedAt = useRef(Date.now());
  const reviewActionTimes = useRef<number[]>([]);
  const reviewTicks = useRef(0);
  const [, setReviewTicks] = useState(0);
  const undoRedo = useReviewUndoRedo();
  const pushUndo = undoRedo.pushAction;

  const isApproved = session?.status === SESSION_APPROVED_STATUS;

  const filteredRecognitions = useMemo(() => {
    const base = filterRecognitions(recognitions, activeFilters, searchText, {
      hideHighConfidence,
    });
    return applySmartQueue(base, {
      category: smartQueueCategory,
      onlyPending: smartQueueOnlyPending,
      collapseApproved: smartQueueOnlyPending,
    });
  }, [
    recognitions,
    activeFilters,
    searchText,
    hideHighConfidence,
    smartQueueCategory,
    smartQueueOnlyPending,
  ]);

  const smartQueueCounts = useMemo(() => countBySmartCategory(recognitions), [recognitions]);
  const smartQueuePendingCount = useMemo(
    () => recognitions.filter((row) => isPendingReview(row.status, row.verifiedByTeacher)).length,
    [recognitions],
  );

  const averageDecisionMs = useMemo(() => {
    const samples = reviewActionTimes.current;
    if (samples.length < 2) {
      return 12_000;
    }
    let total = 0;
    for (let i = 1; i < samples.length; i += 1) {
      total += samples[i] - samples[i - 1];
    }
    return total / (samples.length - 1);
  }, [reviewTicks]);

  const analytics = useMemo(
    () =>
      buildReviewAnalytics({
        imageCount: sessionImages.length || (session?.originalImageUrl ? 1 : 0),
        recognitions,
        statistics: summary?.statistics ?? null,
        elapsedMs,
        averageDecisionMs,
        pendingCount: smartQueuePendingCount,
      }),
    [
      sessionImages.length,
      session?.originalImageUrl,
      recognitions,
      summary?.statistics,
      elapsedMs,
      averageDecisionMs,
      smartQueuePendingCount,
    ],
  );

  const productivity = useMemo(
    () =>
      buildSessionProductivity({
        elapsedMs,
        recognitions,
        decisionTimesMs: reviewActionTimes.current,
        pendingCount: smartQueuePendingCount,
      }),
    [elapsedMs, recognitions, smartQueuePendingCount, reviewTicks],
  );

  useEffect(() => {
    sessionStartedAt.current = Date.now();
    const timer = window.setInterval(() => {
      setElapsedMs(Date.now() - sessionStartedAt.current);
    }, 1000);
    return () => window.clearInterval(timer);
  }, [sessionId]);

  useEffect(() => {
    // Restore remembered fullscreen preference for this workspace.
    setFullscreen(initialPrefs.fullscreen);
    return () => setFullscreen(false);
  }, [initialPrefs.fullscreen, setFullscreen]);

  useEffect(() => {
    saveReviewWorkspacePrefs({
      fullscreen,
      heatMapEnabled,
      heatMapOpacity,
      miniMapVisible,
      smartQueueOnlyPending,
    });
  }, [fullscreen, heatMapEnabled, heatMapOpacity, miniMapVisible, smartQueueOnlyPending]);

  const averageReviewLabel = useMemo(() => {
    const samples = reviewActionTimes.current;
    if (samples.length < 2) {
      return "—";
    }
    let total = 0;
    for (let i = 1; i < samples.length; i += 1) {
      total += samples[i] - samples[i - 1];
    }
    const avg = total / (samples.length - 1);
    return formatElapsed(avg);
  }, [reviewTicks, history.length]);

  const refreshSummary = useCallback(async () => {
    if (!sessionId) {
      return;
    }

    const [summaryRes, historyRes, finalizationRes, auditRes] = await Promise.all([
      getRecognitionSummary(sessionId),
      getSessionReviewHistory(sessionId),
      getFinalizationStatus(sessionId),
      getSessionAuditEntries(sessionId),
    ]);
    setSummary(summaryRes.data);
    setHistory(historyRes.data);
    setFinalizationStatus(finalizationRes.data);
    setAuditEntries(auditRes.data);
  }, [sessionId]);

  const loadData = useCallback(async () => {
    if (!sessionId) {
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const [sessionRes, recognitionsRes, summaryRes, historyRes, finalizationRes, auditRes, images] =
        await Promise.all([
          getAttendanceSession(sessionId),
          getSessionRecognitions(sessionId),
          getRecognitionSummary(sessionId),
          getSessionReviewHistory(sessionId),
          getFinalizationStatus(sessionId),
          getSessionAuditEntries(sessionId),
          listClassroomImages(sessionId).catch(() => [] as AttendanceSessionImage[]),
        ]);
      setSession(sessionRes.data);
      setRecognitions(recognitionsRes.data);
      setSessionImages(images);
      setSummary(summaryRes.data);
      setHistory(historyRes.data);
      setFinalizationStatus(finalizationRes.data);
      setAuditEntries(auditRes.data);
      setNotesById(
        Object.fromEntries(
          recognitionsRes.data.map((row) => [row.recognitionId, row.reviewNotes ?? ""]),
        ),
      );
      setSelectedIds(new Set());
      const remembered = getLastImageSequence(sessionId);
      const first = recognitionsRes.data[0];
      const initialSequence =
        remembered ??
        first?.imageSequence ??
        images[0]?.imageSequence ??
        1;
      setActiveImageSequence(initialSequence);
      const focusForImage =
        recognitionsRes.data.find((row) => (row.imageSequence ?? 1) === initialSequence) ?? first;
      setFocusedId(focusForImage?.recognitionId ?? null);

      // Phase 4.1 — warm image cache for smooth navigator switches
      for (const image of images) {
        const url = mediaAssetUrl(image.imageUrl);
        if (url) {
          const preload = new Image();
          preload.src = url;
        }
      }
    } catch (loadError) {
      setError(errMsg(loadError, "Failed to load attendance recognition review."));
    } finally {
      setLoading(false);
    }
  }, [sessionId]);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  const pendingIds = useMemo(
    () =>
      recognitions
        .filter((row) => isPendingReview(row.status, row.verifiedByTeacher))
        .map((row) => row.recognitionId),
    [recognitions],
  );

  const selectedOrPending = useMemo(() => {
    if (selectedIds.size > 0) {
      return recognitions.filter((row) => selectedIds.has(row.recognitionId));
    }

    return recognitions.filter((row) => isPendingReview(row.status, row.verifiedByTeacher));
  }, [recognitions, selectedIds]);

  const updateRecognition = useCallback((updated: AttendanceRecognitionReviewDto) => {
    setRecognitions((current) =>
      current.map((row) => (row.recognitionId === updated.recognitionId ? updated : row)),
    );
    setNotesById((current) => ({
      ...current,
      [updated.recognitionId]: updated.reviewNotes ?? "",
    }));
  }, []);

  const applyMutation = useCallback(
    (row: AttendanceRecognitionReviewDto, updated: Parameters<typeof mergeReviewUpdate>[1]) => {
      updateRecognition(mergeReviewUpdate(row, updated));
    },
    [updateRecognition],
  );

  const markReviewTiming = useCallback(() => {
    reviewActionTimes.current = [...reviewActionTimes.current, Date.now()].slice(-40);
    reviewTicks.current += 1;
    setReviewTicks(reviewTicks.current);
  }, []);

  const runSingleAction = useCallback(
    async (
      recognitionId: string,
      action: RecognitionReviewActionValue,
      options?: { studentId?: number; reviewNotes?: string | null; trackUndo?: boolean },
    ): Promise<boolean> => {
      const row = recognitions.find((item) => item.recognitionId === recognitionId);
      if (!row) {
        return false;
      }

      setActionLoading(true);
      setError(null);
      setMessage(null);
      try {
        const reviewNotes =
          options?.reviewNotes !== undefined
            ? options.reviewNotes
            : notesById[recognitionId]?.trim() || null;

        const response = await reviewRecognition({
          recognitionId,
          action,
          studentId: options?.studentId,
          reviewNotes,
        });
        if (options?.trackUndo !== false && action !== RecognitionReviewAction.Reset) {
          pushUndo({
            recognitionId,
            action,
            previous: row,
            redoStudentId: options?.studentId,
          });
        }
        applyMutation(row, response.data);
        markReviewTiming();
        await refreshSummary();
        setMessage("Recognition updated.");
        return true;
      } catch (actionError) {
        setError(errMsg(actionError, "Failed to update recognition."));
        return false;
      } finally {
        setActionLoading(false);
      }
    },
    [applyMutation, markReviewTiming, notesById, pushUndo, recognitions, refreshSummary],
  );

  const runBatchAction = useCallback(
    async (action: RecognitionReviewActionValue, reviewNotes?: string | null) => {
      if (!sessionId || selectedOrPending.length === 0) {
        setError("No recognitions selected for batch review.");
        return;
      }

      setActionLoading(true);
      setError(null);
      setMessage(null);
      try {
        const response = await reviewRecognitionBatch({
          attendanceSessionId: sessionId,
          reviews: selectedOrPending.map((row) => ({
            recognitionId: row.recognitionId,
            action,
            reviewNotes:
              reviewNotes !== undefined
                ? reviewNotes
                : notesById[row.recognitionId]?.trim() || null,
          })),
        });
        const updatedById = new Map(response.data.map((row) => [row.id, row]));
        setRecognitions((current) =>
          current.map((row) => {
            const updated = updatedById.get(row.recognitionId);
            return updated ? mergeReviewUpdate(row, updated) : row;
          }),
        );
        setSelectedIds(new Set());
        markReviewTiming();
        await refreshSummary();
        setMessage(
          action === RecognitionReviewAction.Approve
            ? "Batch approve completed."
            : action === RecognitionReviewAction.Reject
              ? "Batch reject completed."
              : "Batch mark unknown completed.",
        );
      } catch (batchError) {
        setError(errMsg(batchError, "Batch review failed."));
      } finally {
        setActionLoading(false);
      }
    },
    [markReviewTiming, notesById, refreshSummary, selectedOrPending, sessionId],
  );

  const openRejectDialog = useCallback((recognitionIds: string[], batch: boolean) => {
    setRejectTarget({ recognitionIds, batch });
  }, []);

  const handleConfirmReject = useCallback(
    async (reason: string) => {
      if (!rejectTarget) {
        return;
      }

      const { recognitionIds, batch } = rejectTarget;
      setRejectTarget(null);

      if (batch) {
        await runBatchAction(RecognitionReviewAction.Reject, reason);
        return;
      }

      const recognitionId = recognitionIds[0];
      if (recognitionId) {
        setNotesById((current) => ({ ...current, [recognitionId]: reason }));
        await runSingleAction(recognitionId, RecognitionReviewAction.Reject, {
          reviewNotes: reason,
        });
      }
    },
    [rejectTarget, runBatchAction, runSingleAction],
  );

  const handleKeyboardAction = useCallback(
    (recognitionId: string, action: RecognitionReviewActionValue) => {
      if (action === RecognitionReviewAction.Reject) {
        openRejectDialog([recognitionId], false);
        return;
      }

      void runSingleAction(recognitionId, action);
    },
    [openRejectDialog, runSingleAction],
  );

  const handleActiveImageSequenceChange = useCallback(
    (sequence: number) => {
      setActiveImageSequence(sequence);
      setLastImageSequence(sessionId, sequence);
      const firstOnImage = recognitions.find((row) => (row.imageSequence ?? 1) === sequence);
      if (firstOnImage) {
        setFocusedId(firstOnImage.recognitionId);
      }
    },
    [recognitions, sessionId],
  );

  const handleReorderImages = useCallback(
    async (orderedIds: string[]) => {
      if (!sessionId || orderedIds.length === 0) {
        return;
      }
      try {
        const listed = await reorderClassroomImages(sessionId, orderedIds);
        setSessionImages(listed);
        setMessage("Classroom images reordered.");
      } catch (reorderError) {
        setError(errMsg(reorderError, "Failed to reorder classroom images."));
      }
    },
    [sessionId],
  );

  const focusRecognition = useCallback(
    (recognitionId: string) => {
      setFocusedId(recognitionId);
      const row = recognitions.find((item) => item.recognitionId === recognitionId);
      if (row) {
        const sequence = row.imageSequence ?? 1;
        setActiveImageSequence(sequence);
        setLastImageSequence(sessionId, sequence);
      }
    },
    [recognitions, sessionId],
  );

  const focusAdjacent = useCallback(
    (delta: number) => {
      if (filteredRecognitions.length === 0) {
        return;
      }
      const index = filteredRecognitions.findIndex((row) => row.recognitionId === focusedId);
      const nextIndex =
        index < 0
          ? 0
          : Math.min(filteredRecognitions.length - 1, Math.max(0, index + delta));
      const next = filteredRecognitions[nextIndex];
      if (next) {
        focusRecognition(next.recognitionId);
      }
    },
    [filteredRecognitions, focusedId, focusRecognition],
  );

  const focusAdjacentImage = useCallback(
    (delta: number) => {
      const ordered = [...sessionImages].sort((a, b) => a.imageSequence - b.imageSequence);
      if (ordered.length === 0) {
        return;
      }
      const index = ordered.findIndex((image) => image.imageSequence === activeImageSequence);
      const nextIndex =
        index < 0
          ? 0
          : Math.min(ordered.length - 1, Math.max(0, index + delta));
      const next = ordered[nextIndex];
      if (next) {
        handleActiveImageSequenceChange(next.imageSequence);
      }
    },
    [activeImageSequence, handleActiveImageSequenceChange, sessionImages],
  );

  const handleUndo = useCallback(async () => {
    const entry = undoRedo.popUndo();
    if (!entry) {
      return;
    }
    const ok = await runSingleAction(entry.recognitionId, RecognitionReviewAction.Reset, {
      trackUndo: false,
    });
    if (ok) {
      undoRedo.commitUndo(entry);
      setMessage("Undid last review action.");
    } else {
      undoRedo.commitRedo(entry);
    }
  }, [runSingleAction, undoRedo]);

  const handleRedo = useCallback(async () => {
    const entry = undoRedo.popRedo();
    if (!entry) {
      return;
    }
    const ok = await runSingleAction(entry.recognitionId, entry.action, {
      studentId: entry.redoStudentId,
      trackUndo: false,
    });
    if (ok) {
      undoRedo.commitRedo(entry);
      setMessage("Redid review action.");
    } else {
      undoRedo.commitUndo(entry);
    }
  }, [runSingleAction, undoRedo]);

  useRecognitionReviewKeyboard({
    focusedId,
    disabled: actionLoading || isApproved || rejectTarget != null,
    onAction: handleKeyboardAction,
    onNext: () => focusAdjacent(1),
    onPrevious: () => focusAdjacent(-1),
    onNextImage: () => focusAdjacentImage(1),
    onPreviousImage: () => focusAdjacentImage(-1),
    onManualMatch: (recognitionId) => setAssignRecognitionId(recognitionId),
    onUndo: () => void handleUndo(),
    onRedo: () => void handleRedo(),
    onToggleFullscreen: toggleFullscreen,
    onToggleHeatMap: () => setHeatMapEnabled((current) => !current),
    onToggleMiniMap: () => setMiniMapVisible((current) => !current),
    onToggleHelp: () => setShortcutHelpOpen((current) => !current),
    onExitFullscreen: () => {
      if (shortcutHelpOpen) {
        setShortcutHelpOpen(false);
        return;
      }
      setFullscreen(false);
    },
  });

  const handleConfirmFinalize = async () => {
    if (!sessionId || !finalizationStatus?.canFinalize) {
      return;
    }

    setFinalizeDialogOpen(false);
    setFinalizing(true);
    setFinalizeProgressIndex(0);
    setError(null);
    setMessage(null);

    const progressTimer = window.setInterval(() => {
      setFinalizeProgressIndex((current) =>
        Math.min(current + 1, FINALIZE_PROGRESS_MESSAGES.length - 1),
      );
    }, 700);

    try {
      const response = await finalizeAttendanceSession(sessionId);
      setFinalizeResult(response.data);
      setMessage("Attendance session finalized.");
      await loadData();
    } catch (finalizeError) {
      setError(errMsg(finalizeError, "Failed to finalize attendance session."));
    } finally {
      window.clearInterval(progressTimer);
      setFinalizing(false);
    }
  };

  const toggleSelected = (recognitionId: string) => {
    setSelectedIds((current) => {
      const next = new Set(current);
      if (next.has(recognitionId)) {
        next.delete(recognitionId);
      } else {
        next.add(recognitionId);
      }
      return next;
    });
  };

  const toggleSelectAllPending = () => {
    if (selectedIds.size === pendingIds.length && pendingIds.length > 0) {
      setSelectedIds(new Set());
      return;
    }

    setSelectedIds(new Set(pendingIds));
  };

  const toggleFilter = (filter: RecognitionReviewFilter) => {
    setActiveFilters((current) => {
      const next = new Set(current);
      if (next.has(filter)) {
        next.delete(filter);
      } else {
        next.add(filter);
      }
      return next;
    });
  };

  const canFinalize = finalizationStatus?.canFinalize ?? false;
  const finalizeBlockers = finalizationStatus?.blockingReasons ?? [];
  const finalizeTooltip =
    finalizeBlockers.length > 0
      ? finalizeBlockers.join(" ")
      : canFinalize
        ? "Generate official attendance for this session"
        : "Complete review before finalizing";

  if (!sessionId) {
    return <Alert severity="error">Session id is required.</Alert>;
  }

  if (loading) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", py: 8 }}>
        <CircularProgress aria-label="Loading recognition review" />
      </Box>
    );
  }

  return (
    <Stack spacing={2} role="main" aria-label="Attendance recognition review">
      {!fullscreen && (
        <Stack
          direction={{ xs: "column", md: "row" }}
          spacing={1}
          sx={{
            justifyContent: "space-between",
            alignItems: { xs: "stretch", md: "center" },
          }}
        >
          <Box>
            <Typography variant="h4" component="h1">
              Recognition review
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Session {sessionId}
              {session?.attendanceDate
                ? ` · ${new Date(session.attendanceDate).toLocaleDateString()}`
                : ""}
            </Typography>
          </Box>

          <Stack
            direction={{ xs: "column", sm: "row" }}
            spacing={1}
            role="toolbar"
            aria-label="Batch review actions"
          >
            <Button
              variant="outlined"
              disabled={actionLoading || isApproved || selectedOrPending.length === 0}
              onClick={() => void runBatchAction(RecognitionReviewAction.Approve)}
            >
              Approve selected
            </Button>
            <Button
              variant="outlined"
              color="error"
              disabled={actionLoading || isApproved || selectedOrPending.length === 0}
              onClick={() =>
                openRejectDialog(
                  selectedOrPending.map((row) => row.recognitionId),
                  true,
                )
              }
            >
              Reject selected
            </Button>
            <Button
              variant="outlined"
              disabled={actionLoading || isApproved || selectedOrPending.length === 0}
              onClick={() => void runBatchAction(RecognitionReviewAction.Ignore)}
            >
              Mark unknown
            </Button>
            <Tooltip title={finalizeTooltip}>
              <span>
                <Button
                  variant="contained"
                  color="success"
                  disabled={finalizing || isApproved || !canFinalize}
                  onClick={() => setFinalizeDialogOpen(true)}
                  aria-describedby={finalizeBlockers.length > 0 ? "finalize-blockers" : undefined}
                >
                  Finalize attendance
                </Button>
              </span>
            </Tooltip>
          </Stack>
        </Stack>
      )}

      {isApproved && !fullscreen && (
        <Alert severity="info">This session is approved. Review actions are read-only.</Alert>
      )}
      {!canFinalize && finalizeBlockers.length > 0 && !isApproved && !fullscreen && (
        <Alert severity="warning" id="finalize-blockers">
          {finalizeBlockers.join(" ")}
        </Alert>
      )}
      {error && <Alert severity="error">{error}</Alert>}
      {message && !fullscreen && <Alert severity="success">{message}</Alert>}

      {finalizing && (
        <Paper variant="outlined" sx={{ p: 2 }} aria-live="polite">
          <Stack direction="row" spacing={2} sx={{ alignItems: "center" }}>
            <CircularProgress size={24} />
            <Typography variant="body2">
              {FINALIZE_PROGRESS_MESSAGES[finalizeProgressIndex]}…
            </Typography>
          </Stack>
        </Paper>
      )}

      {finalizeResult && (
        <AttendanceFinalizationSuccess
          summary={finalizeResult}
          sessionId={sessionId}
          onViewAttendance={() => navigate("/attendance", { state: { switchToManual: true } })}
          onPrint={() => window.print()}
          onReturn={() => navigate("/attendance", { state: { switchToManual: true } })}
        />
      )}

      <Box sx={{ opacity: finalizing ? 0.6 : 1, pointerEvents: finalizing ? "none" : "auto" }}>
        {!fullscreen && <FinalizationSummaryCard status={finalizationStatus} />}

        <RecognitionReviewPanel
          session={session}
          summary={summary}
          recognitions={recognitions}
          filteredRecognitions={filteredRecognitions}
          history={history}
          auditEntries={auditEntries}
          sessionImages={sessionImages}
          activeImageSequence={activeImageSequence}
          onActiveImageSequenceChange={handleActiveImageSequenceChange}
          onReorderImages={(orderedIds) => void handleReorderImages(orderedIds)}
          focusedId={focusedId}
          selectedIds={selectedIds}
          activeFilters={activeFilters}
          searchText={searchText}
          hideHighConfidence={hideHighConfidence}
          notesById={notesById}
          isApproved={isApproved}
          actionLoading={actionLoading}
          pendingCount={pendingIds.length}
          selectedCount={selectedIds.size}
          allPendingSelected={pendingIds.length > 0 && selectedIds.size === pendingIds.length}
          somePendingSelected={selectedIds.size > 0}
          sessionElapsedLabel={formatElapsed(elapsedMs)}
          averageReviewLabel={averageReviewLabel}
          remainingLabel={`${pendingIds.length} · ~${estimateReviewMinutesRemaining(smartQueuePendingCount, averageDecisionMs)}m`}
          canUndo={undoRedo.canUndo}
          canRedo={undoRedo.canRedo}
          fullscreen={fullscreen}
          heatMapEnabled={heatMapEnabled}
          heatMapOpacity={heatMapOpacity}
          miniMapVisible={miniMapVisible}
          smartQueueCategory={smartQueueCategory}
          smartQueueOnlyPending={smartQueueOnlyPending}
          smartQueueCounts={smartQueueCounts}
          smartQueuePendingCount={smartQueuePendingCount}
          smartQueueEstimatedMinutes={estimateReviewMinutesRemaining(
            smartQueuePendingCount,
            averageDecisionMs,
          )}
          analytics={analytics}
          productivity={productivity}
          onSearchChange={setSearchText}
          onToggleFilter={toggleFilter}
          onClearFilters={() => setActiveFilters(new Set())}
          onHideHighConfidenceChange={setHideHighConfidence}
          onToggleSelectAllPending={toggleSelectAllPending}
          onFocusRecognition={focusRecognition}
          onToggleSelected={toggleSelected}
          onNotesChange={(recognitionId, notes) =>
            setNotesById((current) => ({ ...current, [recognitionId]: notes }))
          }
          onApprove={(recognitionId) =>
            void runSingleAction(recognitionId, RecognitionReviewAction.Approve)
          }
          onReject={(recognitionId) => openRejectDialog([recognitionId], false)}
          onIgnore={(recognitionId) =>
            void runSingleAction(recognitionId, RecognitionReviewAction.Ignore)
          }
          onAssign={(recognitionId) => setAssignRecognitionId(recognitionId)}
          onApproveSelected={() => void runBatchAction(RecognitionReviewAction.Approve)}
          onRejectSelected={() =>
            openRejectDialog(
              selectedIds.size > 0
                ? [...selectedIds]
                : selectedOrPending.map((row) => row.recognitionId),
              true,
            )
          }
          onManualMatchSelected={() => {
            const only = [...selectedIds][0];
            if (only) {
              setAssignRecognitionId(only);
            }
          }}
          onMarkUnknownSelected={() => void runBatchAction(RecognitionReviewAction.Ignore)}
          onUndo={() => void handleUndo()}
          onRedo={() => void handleRedo()}
          onToggleFullscreen={toggleFullscreen}
          onHeatMapEnabledChange={setHeatMapEnabled}
          onHeatMapOpacityChange={setHeatMapOpacity}
          onSmartQueueCategoryChange={setSmartQueueCategory}
          onSmartQueueOnlyPendingChange={setSmartQueueOnlyPending}
          shortcutHelpOpen={shortcutHelpOpen}
          onShortcutHelpOpenChange={setShortcutHelpOpen}
        />
      </Box>

      <AssignStudentDialog
        open={assignRecognitionId != null}
        onClose={() => setAssignRecognitionId(null)}
        onAssign={async (studentId) => {
          if (!assignRecognitionId) {
            return;
          }

          const ok = await runSingleAction(
            assignRecognitionId,
            RecognitionReviewAction.AssignStudent,
            { studentId },
          );
          if (!ok) {
            throw new Error("Assign failed");
          }
          setAssignRecognitionId(null);
        }}
      />

      <RejectReasonDialog
        open={rejectTarget != null}
        onClose={() => setRejectTarget(null)}
        onConfirm={(reason) => void handleConfirmReject(reason)}
      />

      <FinalizeAttendanceDialog
        open={finalizeDialogOpen}
        status={finalizationStatus}
        sessionId={sessionId}
        onClose={() => setFinalizeDialogOpen(false)}
        onConfirm={() => void handleConfirmFinalize()}
        confirming={finalizing}
      />
    </Stack>
  );
};

export default AttendanceRecognitionReviewPage;
