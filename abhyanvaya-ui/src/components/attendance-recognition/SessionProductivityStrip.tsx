import { Chip, Stack, Tooltip, Typography } from "@mui/material";
import type { SessionProductivityMetrics } from "../../utils/reviewAnalytics";

export type SessionProductivityStripProps = {
  metrics: SessionProductivityMetrics;
};

/** AI22.7A Phase 5.8 — session productivity metrics (no PII). */
export function SessionProductivityStrip({ metrics }: SessionProductivityStripProps) {
  return (
    <Stack
      direction="row"
      spacing={0.75}
      sx={{ flexWrap: "wrap", gap: 0.75, alignItems: "center" }}
      aria-label="Session productivity metrics"
    >
      <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700 }}>
        Session
      </Typography>
      <Tooltip title="Elapsed review time">
        <Chip size="small" variant="outlined" label={`⏱ ${metrics.elapsedLabel}`} />
      </Tooltip>
      <Tooltip title="Students reviewed">
        <Chip size="small" variant="outlined" label={`Students ${metrics.studentsReviewed}`} />
      </Tooltip>
      <Tooltip title="Faces reviewed">
        <Chip size="small" variant="outlined" label={`Faces ${metrics.facesReviewed}`} />
      </Tooltip>
      <Tooltip title="Reviews per minute">
        <Chip size="small" variant="outlined" label={`${metrics.reviewsPerMinute}/min`} />
      </Tooltip>
      <Tooltip title="Average decision time">
        <Chip size="small" variant="outlined" label={`Avg ${metrics.averageDecisionLabel}`} />
      </Tooltip>
      <Tooltip title="Manual corrections">
        <Chip size="small" variant="outlined" label={`Fixes ${metrics.manualCorrections}`} />
      </Tooltip>
      <Tooltip title="Approval rate">
        <Chip size="small" color="success" variant="outlined" label={`${metrics.approvalPercent}%`} />
      </Tooltip>
      <Tooltip title="Estimated completion">
        <Chip size="small" variant="outlined" label={`ETA ${metrics.estimatedCompletionLabel}`} />
      </Tooltip>
      <Tooltip title="Session productivity score">
        <Chip size="small" color="primary" label={`Score ${metrics.sessionScore}`} />
      </Tooltip>
    </Stack>
  );
}

export default SessionProductivityStrip;
