import {
  Box,
  Button,
  Card,
  CardActions,
  CardContent,
  Chip,
  Stack,
  Typography,
} from "@mui/material";
import { useNavigate } from "react-router-dom";
import {
  AttendanceRetryKind,
  type PendingAttendanceSession,
} from "../../services/attendanceRecoveryService";

type Props = {
  session: PendingAttendanceSession;
  touchSx?: object;
  onRetry?: (id: string, kind: number) => void;
  onCancel?: (id: string) => void;
  compact?: boolean;
};

const bandColor = (band?: string) => {
  switch (band) {
    case "Failed":
      return "error";
    case "NeedsReview":
      return "warning";
    case "RecognitionRunning":
      return "info";
    case "ExpiredSoon":
      return "secondary";
    default:
      return "default";
  }
};

const PendingSessionCard = ({ session: s, touchSx = {}, onRetry, onCancel, compact }: Props) => {
  const navigate = useNavigate();
  const title = s.displayTitle || s.subjectName || `Subject #${s.subjectId}`;
  const statusLabel = s.friendlyWorkflowLabel || s.workflowStatusName;
  const time = s.scheduledTimeLabel || "—";

  return (
    <Card
      variant="outlined"
      sx={{
        minWidth: compact ? 220 : 280,
        flex: compact ? "1 1 220px" : "1 1 280px",
        maxWidth: 420,
      }}
    >
      <CardContent sx={{ pb: 1 }}>
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", mb: 0.5, flexWrap: "wrap" }}>
          <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700 }}>
            {time}
          </Typography>
          <Chip size="small" label={statusLabel} color={bandColor(s.priorityBand) as "default"} />
          {typeof s.priorityScore === "number" && (
            <Chip size="small" variant="outlined" label={`P${s.priorityScore}`} />
          )}
          {s.slaLevel && (
            <Chip
              size="small"
              color={(s.slaBadgeColor as "success" | "warning" | "secondary" | "error" | "default") || "default"}
              label={`SLA ${s.slaLevel}${s.slaStatus ? ` · ${s.slaStatus}` : ""}`}
            />
          )}
        </Stack>
        <Typography sx={{ fontWeight: 700, fontSize: { xs: "1.05rem", sm: "1.1rem" } }}>{title}</Typography>
        <Typography variant="body2" color="text.secondary">
          {[s.courseName, s.groupName, s.semesterName].filter(Boolean).join(" · ") ||
            `Course #${s.courseId}`}
        </Typography>
        <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 0.5 }}>
          Elapsed {s.elapsedDisplay || `${(s.ageMinutes ?? s.elapsedMinutes).toFixed(0)}m`} · Retries {s.retryCount}
          {s.expectedRemainingMinutes != null ? ` · ~${s.expectedRemainingMinutes.toFixed(0)}m left` : ""}
          {s.expectedCompletionUtc
            ? ` · ETA ${new Date(s.expectedCompletionUtc).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}`
            : ""}
        </Typography>
        {s.failureReason && (
          <Typography variant="caption" color="error" sx={{ display: "block" }}>
            {s.failureReason}
          </Typography>
        )}
      </CardContent>
      <CardActions sx={{ flexWrap: "wrap", gap: 1, px: 2, pb: 2 }}>
        {s.canResume !== false && (
          <Button size="small" variant="contained" sx={touchSx} onClick={() => navigate(s.resumePath)}>
            Resume
          </Button>
        )}
        {s.canFinalize && (
          <Button size="small" sx={touchSx} onClick={() => navigate(s.resumePath)}>
            Finalize
          </Button>
        )}
        {s.canRetry && onRetry && (
          <Button
            size="small"
            color="warning"
            sx={touchSx}
            onClick={() => onRetry(s.sessionId, AttendanceRetryKind.RetryRecognition)}
          >
            Retry
          </Button>
        )}
        {s.canCancel && onCancel && (
          <Button size="small" color="inherit" sx={touchSx} onClick={() => onCancel(s.sessionId)}>
            Cancel
          </Button>
        )}
        <Box sx={{ flexGrow: 1 }} />
        <Button size="small" onClick={() => navigate(s.resumePath)}>
          Open
        </Button>
      </CardActions>
    </Card>
  );
};

export default PendingSessionCard;
