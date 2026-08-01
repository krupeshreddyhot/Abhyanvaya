import { Box, Stack, Typography } from "@mui/material";
import { CONFIDENCE_LEGEND } from "../../utils/enterpriseConfidence";

/** AI22.7A Phase 4.3 — compact confidence color legend. */
export function ConfidenceLegend() {
  return (
    <Stack
      direction="row"
      spacing={1}
      sx={{ flexWrap: "wrap", gap: 1, alignItems: "center" }}
      role="list"
      aria-label="Confidence legend"
    >
      {CONFIDENCE_LEGEND.map((item) => (
        <Stack
          key={item.id}
          direction="row"
          spacing={0.5}
          sx={{ alignItems: "center" }}
          role="listitem"
        >
          <Box
            sx={{
              width: 10,
              height: 10,
              borderRadius: 0.5,
              bgcolor: item.color,
              flexShrink: 0,
            }}
            aria-hidden
          />
          <Typography variant="caption" color="text.secondary">
            {item.label} {item.stars}
          </Typography>
        </Stack>
      ))}
    </Stack>
  );
}

export default ConfidenceLegend;
