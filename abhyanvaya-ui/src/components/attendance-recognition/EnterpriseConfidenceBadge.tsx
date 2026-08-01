import { Chip, Stack, Typography } from "@mui/material";
import { getEnterpriseConfidence } from "../../utils/enterpriseConfidence";

export type EnterpriseConfidenceBadgeProps = {
  confidence: number | null | undefined;
  compact?: boolean;
};

/** AI22.7A Phase 4.3 — star + label confidence badge (no AI changes). */
export function EnterpriseConfidenceBadge({
  confidence,
  compact = false,
}: EnterpriseConfidenceBadgeProps) {
  const view = getEnterpriseConfidence(confidence);

  if (compact) {
    return (
      <Chip
        size="small"
        label={`${view.percentLabel} ${view.stars}`}
        sx={{
          bgcolor: `${view.bboxColor}22`,
          color: view.bboxColor,
          fontWeight: 700,
          border: `1px solid ${view.bboxColor}`,
        }}
        aria-label={`Confidence ${view.percentLabel} ${view.label}`}
      />
    );
  }

  return (
    <Stack spacing={0.25} aria-label={`Confidence ${view.percentLabel} ${view.label}`}>
      <Typography variant="body2" sx={{ fontWeight: 700, color: view.bboxColor }}>
        {view.percentLabel} {view.stars}
      </Typography>
      <Typography variant="caption" color="text.secondary">
        {view.label}
      </Typography>
    </Stack>
  );
}

export default EnterpriseConfidenceBadge;
