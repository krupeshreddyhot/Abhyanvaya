import { useCallback, useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  FormControl,
  Grid,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import {
  Timeline,
  TimelineConnector,
  TimelineContent,
  TimelineDot,
  TimelineItem,
  TimelineOppositeContent,
  TimelineSeparator,
} from "@mui/lab";
import { PermissionKeys } from "../../../../auth/permissionKeys";
import { useAuth } from "../../../../context/AuthContext";
import {
  ApprovalDecision,
  decideApprovalStep,
  getApprovalTimeline,
  listApprovalQueue,
  TimetableApprovalRequestStatus,
  type TimetableApprovalRequestDto,
  type TimetableApprovalTimelineDto,
} from "../../../../services/schedulingService";
import { errMsg, parseOptionalSelectNumber } from "../schedulingFormUtils";
import {
  APPROVAL_DECISION_LABELS,
  APPROVAL_REQUEST_STATUS_LABELS,
} from "./governanceEnumLabels";

const ApprovalQueuePage = () => {
  const { hasPermission } = useAuth();
  const canDecide = hasPermission(PermissionKeys.SchedulingApprove);

  const [filterStatus, setFilterStatus] = useState<TimetableApprovalRequestStatus | "">("");
  const [queue, setQueue] = useState<TimetableApprovalRequestDto[]>([]);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [timeline, setTimeline] = useState<TimetableApprovalTimelineDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [timelineLoading, setTimelineLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [comments, setComments] = useState("");
  const [deciding, setDeciding] = useState(false);

  const selected = queue.find((q) => q.id === selectedId) ?? null;

  const loadQueue = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await listApprovalQueue(filterStatus === "" ? undefined : filterStatus);
      setQueue(res.data);
      setSelectedId((prev) => prev ?? (res.data[0]?.id ?? null));
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  }, [filterStatus]);

  useEffect(() => {
    setSelectedId(null);
  }, [filterStatus]);

  useEffect(() => {
    void loadQueue();
  }, [loadQueue]);

  useEffect(() => {
    if (!selectedId) {
      setTimeline(null);
      return;
    }
    void (async () => {
      setTimelineLoading(true);
      try {
        const res = await getApprovalTimeline(selectedId);
        setTimeline(res.data);
      } catch (e) {
        setError(errMsg(e));
      } finally {
        setTimelineLoading(false);
      }
    })();
  }, [selectedId]);

  const handleDecide = async (decision: ApprovalDecision) => {
    if (!selected) return;
    setDeciding(true);
    setError(null);
    try {
      if (
        (decision === ApprovalDecision.Rejected || decision === ApprovalDecision.Returned) &&
        !comments.trim()
      ) {
        setError("Comment is required when rejecting or returning for changes.");
        setDeciding(false);
        return;
      }
      await decideApprovalStep({
        requestId: selected.id,
        stepOrder: selected.currentStepOrder,
        decision,
        comments: comments.trim() || null,
        decisionNotes: comments.trim() || null,
      });
      setComments("");
      setMessage(`Decision recorded: ${APPROVAL_DECISION_LABELS[decision]}.`);
      await loadQueue();
      const tl = await getApprovalTimeline(selected.id);
      setTimeline(tl.data);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setDeciding(false);
    }
  };

  return (
    <Stack spacing={2}>
      <Box sx={{display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap"}}>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />} variant="text">
          Scheduling
        </Button>
        <Typography variant="h5" sx={{flexGrow: 1}}>
          Approval queue
        </Typography>
        <FormControl size="small" sx={{ minWidth: 160 }}>
          <InputLabel>Status</InputLabel>
          <Select
            label="Status"
            value={filterStatus}
            onChange={(e) =>
              setFilterStatus(parseOptionalSelectNumber(e.target.value) as TimetableApprovalRequestStatus | "")
            }
          >
            <MenuItem value="">All</MenuItem>
            {Object.entries(APPROVAL_REQUEST_STATUS_LABELS).map(([k, v]) => (
              <MenuItem key={k} value={Number(k)}>
                {v}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
      </Box>

      {error && <Alert severity="error" onClose={() => setError(null)}>{error}</Alert>}
      {message && <Alert severity="success" onClose={() => setMessage(null)}>{message}</Alert>}

      {loading ? (
        <Box sx={{display: "flex", justifyContent: "center", p: 4}}>
          <CircularProgress />
        </Box>
      ) : (
        <Grid container spacing={2}>
          <Grid size={{ xs: 12, md: 4 }}>
            <Stack spacing={1}>
              {queue.length === 0 && (
                <Typography color="text.secondary">No approval requests in queue.</Typography>
              )}
              {queue.map((q) => (
                <Box key={q.id} onClick={() => setSelectedId(q.id)}
                  sx={{
                    p: 1.5,
                    border: 1,
                    borderColor: selectedId === q.id ? "primary.main" : "divider",
                    borderRadius: 1,
                    cursor: "pointer",
                    bgcolor: selectedId === q.id ? "action.selected" : undefined,
                  }}
                >
                  <Typography variant="subtitle2">{q.timetableName ?? `Timetable #${q.timetableId}`}</Typography>
                  <Typography variant="caption" color="text.secondary" sx={{display: "block"}}>
                    {q.versionName ?? `Version #${q.scheduleVersionId}`}
                  </Typography>
                  <Chip size="small" label={APPROVAL_REQUEST_STATUS_LABELS[q.status]} sx={{ mt: 0.5 }} />
                </Box>
              ))}
            </Stack>
          </Grid>

          <Grid size={{ xs: 12, md: 8 }}>
            {!selected ? (
              <Typography color="text.secondary">Select a request to view details.</Typography>
            ) : (
              <Stack spacing={2}>
                <Box>
                  <Typography variant="h6">{selected.timetableName}</Typography>
                  <Typography variant="body2" color="text.secondary">
                    Submitted {new Date(selected.submittedUtc).toLocaleString()} · Step {selected.currentStepOrder}
                  </Typography>
                </Box>

                <Box>
                  <Typography variant="subtitle2" gutterBottom>
                    Approval steps
                  </Typography>
                  {selected.steps.map((s) => (
                    <Box key={s.id} sx={{mb: 1, pl: 1, borderLeft: 2, borderColor: "divider"}}>
                      <Typography variant="body2">
                        Step {s.stepOrder}: {s.roleKey}
                      </Typography>
                      <Chip size="small" label={APPROVAL_REQUEST_STATUS_LABELS[s.status]} sx={{ mr: 1 }} />
                      {s.comments && (
                        <Typography variant="caption" color="text.secondary">
                          {s.comments}
                        </Typography>
                      )}
                    </Box>
                  ))}
                </Box>

                <Box>
                  <Typography variant="subtitle2" gutterBottom>
                    Timeline
                  </Typography>
                  {timelineLoading ? (
                    <CircularProgress size={24} />
                  ) : timeline ? (
                    <Timeline position="right" sx={{ p: 0, m: 0 }}>
                      {timeline.events.map((ev, i) => (
                        <TimelineItem key={`${ev.stepOrder}-${ev.occurredUtc}-${i}`}>
                          <TimelineOppositeContent color="text.secondary" sx={{ flex: 0.3, fontSize: "0.75rem" }}>
                            {new Date(ev.occurredUtc).toLocaleString()}
                          </TimelineOppositeContent>
                          <TimelineSeparator>
                            <TimelineDot color={ev.decision === ApprovalDecision.Approved ? "success" : "grey"} />
                            {i < timeline.events.length - 1 && <TimelineConnector />}
                          </TimelineSeparator>
                          <TimelineContent>
                            <Typography variant="body2">
                              Step {ev.stepOrder}
                              {ev.decision != null && ` — ${APPROVAL_DECISION_LABELS[ev.decision]}`}
                            </Typography>
                            {ev.oldStatus != null && ev.newStatus != null && (
                              <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                                Status {ev.oldStatus} → {ev.newStatus}
                              </Typography>
                            )}
                            {ev.comments && (
                              <Typography variant="caption" color="text.secondary">
                                {ev.comments}
                              </Typography>
                            )}
                          </TimelineContent>
                        </TimelineItem>
                      ))}
                    </Timeline>
                  ) : null}
                  {timeline?.decisions && timeline.decisions.length > 0 && (
                    <Box sx={{mt: 2}}>
                      <Typography variant="subtitle2" gutterBottom>
                        Decision history
                      </Typography>
                      {timeline.decisions.map((d) => (
                        <Typography key={d.id} variant="caption" color="text.secondary" sx={{ display: "block" }}>
                          {new Date(d.occurredUtc).toLocaleString()} · {d.action}
                          {d.comment ? ` — ${d.comment}` : ""}
                        </Typography>
                      ))}
                    </Box>
                  )}
                </Box>

                {canDecide &&
                  (selected.status === TimetableApprovalRequestStatus.Pending ||
                    selected.status === TimetableApprovalRequestStatus.InReview) && (
                    <Box>
                      <Typography variant="subtitle2" gutterBottom>
                        Decide
                      </Typography>
                      <TextField
                        label="Comment / decision notes (required for Reject / Return)"
                        value={comments}
                        onChange={(e) => setComments(e.target.value)}
                        fullWidth
                        multiline
                        rows={2}
                        sx={{ mb: 1 }}
                      />
                      <Stack direction="row" spacing={1}>
                        <Button
                          variant="contained"
                          color="success"
                          disabled={deciding}
                          onClick={() => void handleDecide(ApprovalDecision.Approved)}
                        >
                          Approve
                        </Button>
                        <Button
                          variant="outlined"
                          color="warning"
                          disabled={deciding}
                          onClick={() => void handleDecide(ApprovalDecision.Returned)}
                        >
                          Return for changes
                        </Button>
                        <Button
                          variant="outlined"
                          color="error"
                          disabled={deciding}
                          onClick={() => void handleDecide(ApprovalDecision.Rejected)}
                        >
                          Reject
                        </Button>
                      </Stack>
                    </Box>
                  )}
              </Stack>
            )}
          </Grid>
        </Grid>
      )}
    </Stack>
  );
};

export default ApprovalQueuePage;
