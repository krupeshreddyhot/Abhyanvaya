import { Chip, Paper, Stack, Typography } from "@mui/material";
import {
  getRecognitionReadiness,
  type RecognitionReadinessInput,
} from "../../utils/recognitionReadiness";

export type RecognitionReadinessBannerProps = RecognitionReadinessInput;

export const RecognitionReadinessBanner = (props: RecognitionReadinessBannerProps) => {
  const view = getRecognitionReadiness(props);

  return (
    <Paper
      variant="outlined"
      aria-label={`Recognition readiness: ${view.label}`}
      sx={{ px: 1.5, py: 1.25 }}
    >
      <Stack direction={{ xs: "column", sm: "row" }} spacing={1} sx={{ alignItems: { sm: "center" } }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700, minWidth: 160 }}>
          Recognition Readiness
        </Typography>
        <Chip
          size="small"
          label={view.label}
          color={
            view.tone === "default"
              ? "default"
              : view.tone === "info"
                ? "info"
                : view.tone === "success"
                  ? "success"
                  : view.tone === "warning"
                    ? "warning"
                    : "error"
          }
        />
        <Typography variant="body2" color="text.secondary">
          {view.description}
        </Typography>
      </Stack>
    </Paper>
  );
};

export default RecognitionReadinessBanner;
