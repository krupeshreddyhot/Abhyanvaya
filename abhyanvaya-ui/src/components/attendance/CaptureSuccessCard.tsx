import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import {
  Alert,
  Box,
  Button,
  Chip,
  Paper,
  Stack,
  Typography,
} from "@mui/material";
import type { CapturedFrame } from "../../types/photoAcquisition";
import {
  estimateFacesFromResolution,
  formatCaptureTime,
  getImageQualityIndicator,
} from "../../utils/imageQuality";

export type CaptureSuccessCardProps = {
  frame: CapturedFrame;
  disabled?: boolean;
  busy?: boolean;
  onRetake: () => void;
  onConfirm: () => void;
  confirmLabel?: string;
};

export const CaptureSuccessCard = ({
  frame,
  disabled = false,
  busy = false,
  onRetake,
  onConfirm,
  confirmLabel = "Use This Photo",
}: CaptureSuccessCardProps) => {
  const quality = getImageQualityIndicator(frame.blurScore);
  const resolution =
    frame.width && frame.height ? `${frame.width} × ${frame.height}` : "—";
  const facesEstimate = estimateFacesFromResolution(frame.width, frame.height);

  return (
    <Paper
      variant="outlined"
      aria-label="Capture success"
      sx={{ p: 2, borderColor: "success.light", bgcolor: "action.hover" }}
    >
      <Stack spacing={2}>
        <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
          <CheckCircleOutlineIcon color="success" aria-hidden />
          <Typography variant="subtitle1" component="h3" sx={{ fontWeight: 700 }}>
            Photo Captured Successfully
          </Typography>
        </Stack>

        <Box
          component="img"
          src={frame.previewUrl}
          alt="Captured classroom photo preview"
          sx={{
            width: "100%",
            maxHeight: 360,
            objectFit: "contain",
            borderRadius: 1,
            border: 1,
            borderColor: "divider",
            bgcolor: "background.default",
          }}
        />

        <Stack spacing={1}>
          <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", gap: 1 }}>
            <Chip
              label={`${quality.stars} ${quality.label}`}
              color={quality.rank >= 4 ? "success" : quality.rank >= 3 ? "default" : "warning"}
              size="small"
              aria-label={`Image quality ${quality.label}`}
            />
            <Chip label="Ready for Recognition" color="info" size="small" variant="outlined" />
          </Stack>

          <Typography variant="body2">
            <strong>Image Quality:</strong> {quality.label}
          </Typography>
          <Typography variant="body2">
            <strong>Estimated Faces Detected:</strong> {facesEstimate}
          </Typography>
          <Typography variant="body2">
            <strong>Capture Time:</strong> {formatCaptureTime(frame.capturedAt)}
          </Typography>
          <Typography variant="body2">
            <strong>Image Resolution:</strong> {resolution}
          </Typography>
        </Stack>

        {quality.rank > 0 && quality.rank < 3 && (
          <Alert severity="warning">
            Image quality is low. Consider retaking for better face recognition.
          </Alert>
        )}

        <Stack direction={{ xs: "column", sm: "row" }} spacing={1}>
          <Button variant="outlined" onClick={onRetake} disabled={disabled || busy} fullWidth>
            Retake
          </Button>
          <Button
            variant="contained"
            onClick={onConfirm}
            disabled={disabled || busy}
            fullWidth
            aria-label={`${confirmLabel} — confirm upload`}
          >
            {confirmLabel}
          </Button>
        </Stack>

        <Typography variant="caption" color="text.secondary">
          Photo is not uploaded until you confirm.
        </Typography>
      </Stack>
    </Paper>
  );
};

export default CaptureSuccessCard;
