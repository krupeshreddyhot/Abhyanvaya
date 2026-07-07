import { Box, LinearProgress, Typography } from "@mui/material";
import { memo } from "react";
import {
  confidenceBarValue,
  confidenceColor,
  formatConfidence,
  getConfidenceBand,
  CONFIDENCE_BANDS,
} from "../../utils/confidenceColor";

type ConfidenceBarProps = {
  score: number | null | undefined;
  compact?: boolean;
};

export const ConfidenceBar = memo(function ConfidenceBar({ score, compact }: ConfidenceBarProps) {
  const band = getConfidenceBand(score);
  const color = confidenceColor(score);

  return (
    <Box sx={{ width: "100%" }}>
      <Box sx={{ display: "flex", justifyContent: "space-between", mb: 0.5 }}>
        <Typography variant={compact ? "caption" : "body2"} sx={{ fontWeight: 600 }}>
          {formatConfidence(score)}
        </Typography>
        <Typography variant="caption" color="text.secondary">
          {CONFIDENCE_BANDS[band].label}
        </Typography>
      </Box>
      <LinearProgress
        variant="determinate"
        value={confidenceBarValue(score)}
        aria-label={`Confidence ${formatConfidence(score)}`}
        sx={{
          height: compact ? 6 : 8,
          borderRadius: 1,
          bgcolor: "action.hover",
          "& .MuiLinearProgress-bar": {
            bgcolor: color,
            transition: "transform 0.4s ease",
          },
        }}
      />
    </Box>
  );
});
