import axios from "axios";
import { useCallback, useEffect, useMemo, useState } from "react";
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
import {
  filterRecognitions,
  type RecognitionReviewFilter,
} from "../utils/recognitionReviewFilters";
import { isPendingReview } from "../utils/recognitionStatus";

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

type RejectTarget = {
  recognitionIds: string[];
  batch: boolean;
};

const AttendanceRecognitionReviewPage = () => {
  const { sessionId = "" } = useParams<{ sessionId: string }>();
  const navigate = useNavigate();

  const [session, setSession] = useState<AttendanceSessionReviewDto | null>(null);
  const [recognitions, setRecognitions] = useState<AttendanceRecognitionReviewDto[]>([]);
  const [summary, setSummary] = useState<RecognitionSummaryDto | null>(null);
  const [finalizationStatus, setFinalizationStatus] = useState<FinalizationStatusDto | null>(null);
  const [history, setHistory] = useState<AttendanceRecognitionReviewHistoryDto[]>([]);
  const [auditEntries, setAuditEntries] = useState<AuditEntryDto[]>([]);
  const [notesById, setNotesById] = useState<Record<string, string>>({});
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [focusedId, setFocusedId] = useState<string | null>(null);
  const [activeFilters, setActiveFilters] = useState<Set<RecognitionReviewFilter>>(new Set());
  const [searchText, setSearchText] = useState("");
  const [assignRecognitionId, setAssignRecognitionId] = useState<string | null>(null);
  const [rejectTarget, setRejectTarget] = useState<RejectTarget | null>(null);

  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [finalizing, setFinalizing] = useState(false);
  const [finalizeDialogOpen, setFinalizeDialogOpen] = useState(false);
  const [finalizeProgressIndex, setFinalizeProgressIndex] = useState(0);
  const [finalizeResult, setFinalizeResult] = useState<AttendanceBuildSummaryDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const isApproved = session?.status === SESSION_APPROVED_STATUS;

  const filteredRecognitions = useMemo(
    () => filterRecognitions(recognitions, activeFilters, searchText),
    [recognitions, activeFilters, searchText]
  );

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
      const [sessionRes, recognitionsRes, summaryRes, historyRes, finalizationRes, auditRes] =
        await Promise.all([
          getAttendanceSession(sessionId),
          getSessionRecognitions(sessionId),
          getRecognitionSummary(sessionId),
          getSessionReviewHistory(sessionId),
          getFinalizationStatus(sessionId),
          getSessionAuditEntries(sessionId),
        ]);
      setSession(sessionRes.data);
      setRecognitions(recognitionsRes.data);
      setSummary(summaryRes.data);
      setHistory(historyRes.data);
      setFinalizationStatus(finalizationRes.data);
      setAuditEntries(auditRes.data);
      setNotesById(
        Object.fromEntries(
          recognitionsRes.data.map((row) => [row.recognitionId, row.reviewNotes ?? ""])
        )
      );
      setSelectedIds(new Set());
      setFocusedId(recognitionsRes.data[0]?.recognitionId ?? null);
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
    [recognitions]
  );

  const selectedOrPending = useMemo(() => {
    if (selectedIds.size > 0) {
      return recognitions.filter((row) => selectedIds.has(row.recognitionId));
    }

    return recognitions.filter((row) => isPendingReview(row.status, row.verifiedByTeacher));
  }, [recognitions, selectedIds]);

  const updateRecognition = useCallback((updated: AttendanceRecognitionReviewDto) => {
    setRecognitions((current) =>
      current.map((row) => (row.recognitionId === updated.recognitionId ? updated : row))
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
    [updateRecognition]
  );

  const runSingleAction = useCallback(
    async (
      recognitionId: string,
      action: RecognitionReviewActionValue,
      options?: { studentId?: number; reviewNotes?: string | null }
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
        applyMutation(row, response.data);
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
    [applyMutation, notesById, recognitions, refreshSummary]
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
          })
        );
        setSelectedIds(new Set());
        await refreshSummary();
        setMessage(
          action === RecognitionReviewAction.Approve
            ? "Batch approve completed."
            : action === RecognitionReviewAction.Reject
              ? "Batch reject completed."
              : "Batch mark unknown completed."
        );
      } catch (batchError) {
        setError(errMsg(batchError, "Batch review failed."));
      } finally {
        setActionLoading(false);
      }
    },
    [notesById, refreshSummary, selectedOrPending, sessionId]
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
    [rejectTarget, runBatchAction, runSingleAction]
  );

  const handleKeyboardAction = useCallback(
    (recognitionId: string, action: RecognitionReviewActionValue) => {
      if (action === RecognitionReviewAction.Reject) {
        openRejectDialog([recognitionId], false);
        return;
      }

      void runSingleAction(recognitionId, action);
    },
    [openRejectDialog, runSingleAction]
  );

  useRecognitionReviewKeyboard({
    focusedId,
    disabled: actionLoading || isApproved || rejectTarget != null,
    onAction: handleKeyboardAction,
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
        Math.min(current + 1, FINALIZE_PROGRESS_MESSAGES.length - 1)
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

  const focusRecognition = (recognitionId: string) => {
    setFocusedId(recognitionId);
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

        <Stack direction={{ xs: "column", sm: "row" }} spacing={1} role="toolbar" aria-label="Batch review actions">
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
                true
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

      {isApproved && (
        <Alert severity="info">This session is approved. Review actions are read-only.</Alert>
      )}
      {!canFinalize && finalizeBlockers.length > 0 && !isApproved && (
        <Alert severity="warning" id="finalize-blockers">
          {finalizeBlockers.join(" ")}
        </Alert>
      )}
      {error && <Alert severity="error">{error}</Alert>}
      {message && <Alert severity="success">{message}</Alert>}

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
        <FinalizationSummaryCard status={finalizationStatus} />

        <RecognitionReviewPanel
        session={session}
        summary={summary}
        recognitions={recognitions}
        filteredRecognitions={filteredRecognitions}
        history={history}
        auditEntries={auditEntries}
        focusedId={focusedId}
        selectedIds={selectedIds}
        activeFilters={activeFilters}
        searchText={searchText}
        notesById={notesById}
        isApproved={isApproved}
        actionLoading={actionLoading}
        pendingCount={pendingIds.length}
        selectedCount={selectedIds.size}
        allPendingSelected={pendingIds.length > 0 && selectedIds.size === pendingIds.length}
        somePendingSelected={selectedIds.size > 0}
        onSearchChange={setSearchText}
        onToggleFilter={toggleFilter}
        onClearFilters={() => setActiveFilters(new Set())}
        onToggleSelectAllPending={toggleSelectAllPending}
        onFocusRecognition={focusRecognition}
        onToggleSelected={toggleSelected}
        onNotesChange={(recognitionId, notes) =>
          setNotesById((current) => ({ ...current, [recognitionId]: notes }))
        }
        onApprove={(recognitionId) => void runSingleAction(recognitionId, RecognitionReviewAction.Approve)}
        onReject={(recognitionId) => openRejectDialog([recognitionId], false)}
        onIgnore={(recognitionId) => void runSingleAction(recognitionId, RecognitionReviewAction.Ignore)}
        onAssign={(recognitionId) => setAssignRecognitionId(recognitionId)}
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
            { studentId }
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
