import HistoryIcon from "@mui/icons-material/History";
import {
  List,
  ListItem,
  ListItemText,
  Paper,
  Stack,
  Typography,
} from "@mui/material";
import { useMemo } from "react";
import {
  RecognitionReviewAction,
  type AttendanceRecognitionReviewHistoryDto,
  type AuditEntryDto,
} from "../../services/attendanceRecognitionService";

type AiActivityPanelProps = {
  history: AttendanceRecognitionReviewHistoryDto[];
  auditEntries?: AuditEntryDto[];
};

type ActivityRow = {
  id: string;
  title: string;
  when: string;
};

function mapAction(action: number): string {
  switch (action) {
    case RecognitionReviewAction.Approve:
      return "Student Approved";
    case RecognitionReviewAction.Reject:
      return "Student Rejected";
    case RecognitionReviewAction.Ignore:
      return "Marked Unknown";
    case RecognitionReviewAction.AssignStudent:
      return "Manual Match";
    case RecognitionReviewAction.Reset:
      return "Review Reset";
    default:
      return "Review Action";
  }
}

/** AI22.7A Phase 4.5 — compact AI / review activity feed. */
export function AiActivityPanel({ history, auditEntries = [] }: AiActivityPanelProps) {
  const rows = useMemo(() => {
    const reviewRows: ActivityRow[] = history.map((entry) => ({
      id: `h-${entry.id}`,
      title: mapAction(entry.reviewAction),
      when: entry.reviewedUtc,
    }));

    const auditRows: ActivityRow[] = auditEntries.map((entry) => ({
      id: `a-${entry.id}`,
      title:
        entry.action === 5
          ? "Recognition Completed / Attendance Generated"
          : entry.action === 7
            ? "Image Reprocessed"
            : "Session Event",
      when: entry.performedUtc,
    }));

    return [...reviewRows, ...auditRows]
      .sort((a, b) => new Date(b.when).getTime() - new Date(a.when).getTime())
      .slice(0, 12);
  }, [history, auditEntries]);

  return (
    <Paper variant="outlined" sx={{ p: 2 }} aria-label="AI activity panel">
      <Stack direction="row" spacing={1} sx={{ alignItems: "center", mb: 1 }}>
        <HistoryIcon fontSize="small" color="action" />
        <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
          AI Activity
        </Typography>
      </Stack>
      {rows.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          No review activity yet for this session.
        </Typography>
      ) : (
        <List dense disablePadding>
          {rows.map((row) => (
            <ListItem key={row.id} disableGutters sx={{ py: 0.25 }}>
              <ListItemText
                primary={row.title}
                secondary={new Date(row.when).toLocaleString()}
                slotProps={{
                  primary: { variant: "body2" },
                  secondary: { variant: "caption" },
                }}
              />
            </ListItem>
          ))}
        </List>
      )}
    </Paper>
  );
}

export default AiActivityPanel;
