import { Box, Divider, Paper, Stack, Typography } from "@mui/material";
import { memo, useMemo } from "react";
import {
  RecognitionReviewAction,
  type AttendanceRecognitionReviewHistoryDto,
  type AuditEntryDto,
} from "../../services/attendanceRecognitionService";

type RecognitionReviewTimelineProps = {
  history: AttendanceRecognitionReviewHistoryDto[];
  auditEntries?: AuditEntryDto[];
};

const AUDIT_ACTION_APPROVED = 5;

function actionLabel(action: number): string {
  switch (action) {
    case RecognitionReviewAction.Approve:
      return "Approved";
    case RecognitionReviewAction.Reject:
      return "Rejected";
    case RecognitionReviewAction.Ignore:
      return "Marked unknown";
    case RecognitionReviewAction.AssignStudent:
      return "Manual override";
    case RecognitionReviewAction.Reset:
      return "Reset";
    default:
      return "Review action";
  }
}

function auditLabel(entry: AuditEntryDto): string {
  if (entry.action === AUDIT_ACTION_APPROVED) {
    return "Attendance generated · Finalized";
  }

  return entry.action === 7 ? "Reviewed" : "Session event";
}

function parseDurationMs(newValues: string | null): string | null {
  if (!newValues) {
    return null;
  }

  try {
    const parsed = JSON.parse(newValues) as { durationMilliseconds?: number };
    return parsed.durationMilliseconds != null ? `${parsed.durationMilliseconds} ms` : null;
  } catch {
    return null;
  }
}

type TimelineRow = {
  id: string;
  timestamp: string;
  title: string;
  user?: string | null;
  detail?: string | null;
};

export const RecognitionReviewTimeline = memo(function RecognitionReviewTimeline({
  history,
  auditEntries = [],
}: RecognitionReviewTimelineProps) {
  const entries = useMemo(() => {
    const rows: TimelineRow[] = [
      ...history.map((entry) => ({
        id: `review-${entry.id}`,
        timestamp: entry.reviewedUtc,
        title: actionLabel(entry.reviewAction),
        user: entry.reviewedByUsername,
        detail: entry.reviewNotes,
      })),
      ...auditEntries.map((entry) => {
        const duration = parseDurationMs(entry.newValues);
        return {
          id: `audit-${entry.id}`,
          timestamp: entry.performedUtc,
          title: auditLabel(entry),
          user: entry.performedByUsername,
          detail: duration ? `Duration: ${duration}` : undefined,
        };
      }),
    ];

    return rows.sort(
      (a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime()
    );
  }, [auditEntries, history]);

  if (entries.length === 0) {
    return (
      <Paper variant="outlined" sx={{ p: 2 }}>
        <Typography variant="body2" color="text.secondary">
          Review timeline will appear after teacher actions are recorded.
        </Typography>
      </Paper>
    );
  }

  return (
    <Paper variant="outlined" sx={{ p: 2, maxHeight: 280, overflowY: "auto" }} aria-label="Review timeline">
      <Typography variant="subtitle2" gutterBottom>
        Review timeline
      </Typography>
      <Stack divider={<Divider flexItem />} spacing={1}>
        {entries.slice(0, 100).map((entry) => (
          <Box key={entry.id}>
            <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
              {new Date(entry.timestamp).toLocaleString()}
            </Typography>
            <Typography variant="body2">
              {entry.title}
              {entry.user ? ` · ${entry.user}` : ""}
            </Typography>
            {entry.detail && (
              <Typography variant="caption" color="text.secondary">
                {entry.detail}
              </Typography>
            )}
          </Box>
        ))}
      </Stack>
    </Paper>
  );
});
