import { useEffect, useMemo, useState } from "react";
import axios from "axios";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import PendingSessionCard from "../../components/attendance-recovery/PendingSessionCard";
import {
  AttendanceRetryKind,
  cancelRecoverySession,
  getPendingAttendance,
  getPendingSessionQueue,
  retryAttendanceSession,
  type PendingAttendanceSession,
  type PendingSessionQueue,
} from "../../services/attendanceRecoveryService";

type Props = { touchSx: object };

const errMsg = (error: unknown, fallback: string) => {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as { detail?: string; title?: string; message?: string } | string | undefined;
    if (typeof data === "string" && data.trim()) return data;
    if (data && typeof data === "object") {
      return data.detail || data.message || data.title || fallback;
    }
    if (error.response?.status === 404) {
      return "Pending queue API was not found. Restart the API so AI22.8.5 endpoints are loaded.";
    }
    if (error.message) return error.message;
  }
  if (error instanceof Error && error.message) return error.message;
  return fallback;
};

const toQueue = (items: PendingAttendanceSession[]): PendingSessionQueue => ({
  items,
  total: items.length,
  failedCount: items.filter((s) => (s.priorityBand ?? s.workflowStatusName).toLowerCase().includes("fail")).length,
  needsReviewCount: items.filter((s) => (s.friendlyWorkflowLabel ?? s.workflowStatusName).toLowerCase().includes("review")).length,
  recognitionReadyCount: items.filter((s) => (s.friendlyWorkflowLabel ?? "").includes("Ready")).length,
  recognitionRunningCount: items.filter((s) => (s.friendlyWorkflowLabel ?? s.workflowStatusName).includes("Running")).length,
  sortedByPriority: false,
});

const FacultyPendingAttendancePanel = ({ touchSx }: Props) => {
  const [data, setData] = useState<PendingSessionQueue | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [msg, setMsg] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [query, setQuery] = useState("");
  const [band, setBand] = useState("");
  const [sortBy, setSortBy] = useState("priority");

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await getPendingSessionQueue({
        query: query || undefined,
        priorityBand: band || undefined,
        sortBy,
      });
      setData(res.data);
    } catch (queueError) {
      // Fallback for older API hosts / transient queue failures — still show pending sessions.
      try {
        const legacy = await getPendingAttendance();
        const bucket = legacy.data;
        const merged = [
          ...bucket.failedSessions,
          ...bucket.reviewPending,
          ...bucket.readyToFinalize,
          ...bucket.recognitionRunning,
          ...bucket.todaysPending,
          ...bucket.myPendingSessions,
        ];
        const unique = Array.from(new Map(merged.map((s) => [s.sessionId, s])).values());
        setData(toQueue(unique));
        setMsg("Loaded using classic pending API (queue endpoint unavailable). Restart API for full priority queue.");
      } catch {
        setError(errMsg(queueError, "Failed to load pending attendance queue."));
        setData(null);
      }
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
    const onRefresh = () => void load();
    window.addEventListener("attendance-recovery-refresh", onRefresh);
    return () => window.removeEventListener("attendance-recovery-refresh", onRefresh);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [band, sortBy]);

  const onRetry = async (id: string, kind: number) => {
    try {
      await retryAttendanceSession(id, kind);
      setMsg("Retry queued for failed stages only (completed stages not restarted).");
      await load();
    } catch (e) {
      setError(errMsg(e, "Retry failed."));
    }
  };

  const onCancel = async (id: string) => {
    try {
      await cancelRecoverySession(id);
      setMsg("Session cancelled from recovery queue.");
      await load();
    } catch (e) {
      setError(errMsg(e, "Cancel failed."));
    }
  };

  const items: PendingAttendanceSession[] = useMemo(() => data?.items ?? [], [data]);

  if (loading && !data) return <CircularProgress />;

  return (
    <Stack spacing={2} className="faculty-pending-attendance">
      <Alert severity="info">
        Enterprise pending queue — resumes the existing attendance session. No duplicates are created.
      </Alert>
      {msg && (
        <Alert severity="success" onClose={() => setMsg(null)}>
          {msg}
        </Alert>
      )}
      {error && (
        <Alert severity="error" onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      <Stack direction={{ xs: "column", sm: "row" }} spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
        <TextField
          size="small"
          label="Search"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          sx={{ minWidth: 180, flex: 1 }}
        />
        <FormControl size="small" sx={{ minWidth: 160 }}>
          <InputLabel>Priority band</InputLabel>
          <Select label="Priority band" value={band} onChange={(e) => setBand(e.target.value)}>
            <MenuItem value="">All</MenuItem>
            <MenuItem value="Failed">Failed</MenuItem>
            <MenuItem value="NeedsReview">Needs Review</MenuItem>
            <MenuItem value="RecognitionReady">Recognition Ready</MenuItem>
            <MenuItem value="RecognitionRunning">Recognition Running</MenuItem>
            <MenuItem value="ExpiredSoon">Expired Soon</MenuItem>
            <MenuItem value="RecentlyStarted">Recently Started</MenuItem>
          </Select>
        </FormControl>
        <FormControl size="small" sx={{ minWidth: 140 }}>
          <InputLabel>Sort</InputLabel>
          <Select label="Sort" value={sortBy} onChange={(e) => setSortBy(e.target.value)}>
            <MenuItem value="priority">Priority</MenuItem>
            <MenuItem value="time">Time</MenuItem>
            <MenuItem value="subject">Subject</MenuItem>
            <MenuItem value="age">Age</MenuItem>
            <MenuItem value="activity">Activity</MenuItem>
          </Select>
        </FormControl>
        <Button variant="contained" sx={touchSx} onClick={() => void load()}>
          Apply
        </Button>
      </Stack>

      {data && (
        <Typography variant="body2">
          {data.total} sessions · Failed {data.failedCount} · Review {data.needsReviewCount} · Ready{" "}
          {data.recognitionReadyCount} · Running {data.recognitionRunningCount}
          {data.sortedByPriority ? " · sorted by priority" : ""}
        </Typography>
      )}

      <Box>
        <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
          {items.map((s) => (
            <PendingSessionCard
              key={s.sessionId}
              session={s}
              touchSx={touchSx}
              onRetry={onRetry}
              onCancel={onCancel}
            />
          ))}
        </Stack>
        {items.length === 0 && !error && (
          <Typography variant="body2" color="text.secondary">
            No pending sessions.
          </Typography>
        )}
      </Box>

      <Box sx={{ display: "none" }}>{AttendanceRetryKind.RetryRecognition}</Box>
    </Stack>
  );
};

export default FacultyPendingAttendancePanel;
