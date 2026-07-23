import CloseIcon from "@mui/icons-material/Close";
import {
  Box,
  Chip,
  Dialog,
  DialogContent,
  DialogTitle,
  Divider,
  IconButton,
  Stack,
  Typography,
} from "@mui/material";
import type { AttendanceSessionImage } from "../../types/sessionImage";
import { formatCaptureTime, getImageQualityIndicator } from "../../utils/imageQuality";
import { formatFileSizeLabel, formatResolution } from "../../utils/fileDisplay";

export type ClassroomImageDetailsDialogProps = {
  open: boolean;
  image: AttendanceSessionImage | null;
  onClose: () => void;
};

const DetailRow = ({ label, value }: { label: string; value: string }) => (
  <Stack spacing={0.25} sx={{ py: 0.75 }}>
    <Typography variant="caption" color="text.secondary">
      {label}
    </Typography>
    <Typography variant="body2" sx={{ fontWeight: 600, wordBreak: "break-word" }}>
      {value}
    </Typography>
  </Stack>
);

const formatGps = (lat?: number | null, lng?: number | null): string => {
  if (lat == null || lng == null) {
    return "Not available";
  }
  return `${lat.toFixed(5)}, ${lng.toFixed(5)}`;
};

export const ClassroomImageDetailsDialog = ({
  open,
  image,
  onClose,
}: ClassroomImageDetailsDialogProps) => {
  if (!image) {
    return null;
  }

  const quality = getImageQualityIndicator(image.blurScore);
  const captureTime = formatCaptureTime(image.captureTimestamp ?? image.uploadedUtc);
  const recognitionStatus = image.batchStatus || "Waiting";

  return (
    <Dialog
      open={open}
      onClose={onClose}
      fullWidth
      maxWidth="sm"
      aria-labelledby="classroom-image-details-title"
    >
      <DialogTitle id="classroom-image-details-title" sx={{ pr: 6 }}>
        Image Details — #{image.imageSequence}
        <IconButton
          onClick={onClose}
          aria-label="Close image details"
          sx={{ position: "absolute", right: 8, top: 8 }}
        >
          <CloseIcon />
        </IconButton>
      </DialogTitle>
      <DialogContent dividers>
        <Stack spacing={1.5}>
          {image.imageUrl && (
            <Box
              component="img"
              src={image.imageUrl}
              alt={image.originalFileName ?? `Image ${image.imageSequence}`}
              sx={{
                width: "100%",
                maxHeight: 280,
                objectFit: "contain",
                borderRadius: 1,
                border: 1,
                borderColor: "divider",
                bgcolor: "action.hover",
              }}
            />
          )}

          <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", gap: 1 }}>
            <Chip size="small" label={`Batch: ${recognitionStatus}`} color="info" />
            <Chip size="small" label={`${quality.stars} ${quality.label}`} />
            <Chip size="small" variant="outlined" label={image.acquisitionMethod ?? "Upload"} />
          </Stack>

          <Divider />

          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" },
              gap: 1,
            }}
          >
            <DetailRow label="Filename" value={image.originalFileName ?? "—"} />
            <DetailRow label="Capture Timestamp" value={captureTime} />
            <DetailRow label="Device Information" value={image.captureDevice ?? "—"} />
            <DetailRow
              label="GPS Metadata"
              value={formatGps(image.captureLatitude, image.captureLongitude)}
            />
            <DetailRow label="File Size" value={formatFileSizeLabel(image.fileSize ?? undefined)} />
            <DetailRow
              label="Resolution"
              value={formatResolution(image.width ?? undefined, image.height ?? undefined)}
            />
            <DetailRow label="Recognition Status" value={recognitionStatus} />
            <DetailRow label="Batch Status" value={image.batchStatus ?? recognitionStatus} />
            <DetailRow
              label="Faces Detected"
              value={image.detectedFaceCount > 0 ? String(image.detectedFaceCount) : "Pending"}
            />
            <DetailRow
              label="Orientation"
              value={image.orientation != null ? String(image.orientation) : "—"}
            />
          </Box>

          {image.processingError && (
            <Typography variant="body2" color="error" role="alert">
              {image.processingError}
            </Typography>
          )}
        </Stack>
      </DialogContent>
    </Dialog>
  );
};

export default ClassroomImageDetailsDialog;
