import { Box, Chip, Stack, Typography } from "@mui/material";
import type { SessionTimeline as SessionTimelineDto } from "../../services/attendanceRecoveryService";

type Props = {
  timeline: SessionTimelineDto | null;
  compact?: boolean;
};

/** AI22.8.6.3 — enterprise session timeline (reuses retry/workflow history). */
const SessionTimeline = ({ timeline, compact }: Props) => {
  if (!timeline || timeline.events.length === 0) {
    return (
      <Typography variant="body2" color="text.secondary">
        No timeline events yet.
      </Typography>
    );
  }

  return (
    <Stack spacing={compact ? 0.75 : 1.25}>
      {timeline.events.map((e, idx) => (
        <Box
          key={`${e.operation}-${e.occurredUtc}-${idx}`}
          sx={{
            display: "grid",
            gridTemplateColumns: compact ? "1fr" : "140px 1fr",
            gap: 1,
            borderLeft: 3,
            borderColor: e.success ? "success.main" : "error.main",
            pl: 1.25,
          }}
        >
          <Typography variant="caption" color="text.secondary">
            {e.relativeTime}
          </Typography>
          <Box>
            <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }}>
              <Typography variant="body2" sx={{ fontWeight: 700 }}>
                {e.operation}
              </Typography>
              <Chip size="small" label={e.source} variant="outlined" />
              {!e.success && <Chip size="small" color="error" label="Failed" />}
            </Stack>
            {(e.reason || e.userDisplay || e.userId != null) && (
              <Typography variant="caption" color="text.secondary">
                {[e.userDisplay || (e.userId != null ? `User #${e.userId}` : null), e.reason]
                  .filter(Boolean)
                  .join(" · ")}
              </Typography>
            )}
          </Box>
        </Box>
      ))}
      {timeline.total > timeline.events.length && (
        <Typography variant="caption" color="text.secondary">
          Showing {timeline.events.length} of {timeline.total}
        </Typography>
      )}
    </Stack>
  );
};

export default SessionTimeline;
