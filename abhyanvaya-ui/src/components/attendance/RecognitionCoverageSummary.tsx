import {
  Box,
  Card,
  CardContent,
  Grid,
  Stack,
  Typography,
} from "@mui/material";
import type { AttendanceSessionImage } from "../../types/sessionImage";
import type { AIStatus } from "../../types/aiWorkflow";
import {
  getImageQualityIndicator,
  type ImageQualityIndicator,
} from "../../utils/imageQuality";
import { getRecognitionReadiness } from "../../utils/recognitionReadiness";

export type RecognitionCoverageSummaryProps = {
  images: AttendanceSessionImage[];
  detectedFaces: number;
  matchedFaces?: number;
  unknownFaces?: number;
  status: AIStatus;
  sessionStatusCode?: number | null;
  recognitionQueued?: boolean;
  queueStatus?: number | null;
  variant?: "summary" | "dashboard";
};

const Metric = ({ label, value }: { label: string; value: string }) => (
  <Stack spacing={0.25}>
    <Typography variant="caption" color="text.secondary">
      {label}
    </Typography>
    <Typography variant="body2" sx={{ fontWeight: 700 }}>
      {value}
    </Typography>
  </Stack>
);

const averageQuality = (images: AttendanceSessionImage[]): ImageQualityIndicator => {
  const ranked = images
    .map((image) => getImageQualityIndicator(image.blurScore))
    .filter((q) => q.rank > 0);
  if (ranked.length === 0) {
    return getImageQualityIndicator(null);
  }
  const avg = ranked.reduce((sum, q) => sum + q.rank, 0) / ranked.length;
  if (avg >= 4.5) return getImageQualityIndicator(220);
  if (avg >= 3.5) return getImageQualityIndicator(140);
  if (avg >= 2.5) return getImageQualityIndicator(90);
  if (avg >= 1.5) return getImageQualityIndicator(50);
  return getImageQualityIndicator(20);
};

const averageResolution = (images: AttendanceSessionImage[]): string => {
  const sized = images.filter((image) => image.width && image.height);
  if (sized.length === 0) {
    return "—";
  }
  const w = Math.round(sized.reduce((sum, i) => sum + (i.width ?? 0), 0) / sized.length);
  const h = Math.round(sized.reduce((sum, i) => sum + (i.height ?? 0), 0) / sized.length);
  return `${w} × ${h}`;
};

export const RecognitionCoverageSummary = ({
  images,
  detectedFaces,
  matchedFaces = 0,
  unknownFaces,
  status,
  sessionStatusCode,
  recognitionQueued,
  queueStatus,
  variant = "summary",
}: RecognitionCoverageSummaryProps) => {
  const readiness = getRecognitionReadiness({
    imageCount: images.length,
    status,
    sessionStatusCode,
    recognitionQueued,
    queueStatus,
    hasFailedImages: images.some((image) => image.status === 4),
  });
  const quality = averageQuality(images);
  const duplicateFaces = Math.max(0, detectedFaces - matchedFaces - (unknownFaces ?? 0));
  const progress =
    images.length === 0
      ? "0%"
      : `${Math.round((images.filter((i) => i.status === 3).length / images.length) * 100)}%`;

  if (variant === "dashboard") {
    return (
      <Card variant="outlined" aria-label="Recognition coverage dashboard">
        <CardContent sx={{ py: 1.75, "&:last-child": { pb: 1.75 } }}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.25 }}>
            Recognition Coverage
          </Typography>
          <Grid container spacing={1.5}>
            <Grid size={{ xs: 6, sm: 4, md: 3 }}>
              <Metric label="Images" value={String(images.length)} />
            </Grid>
            <Grid size={{ xs: 6, sm: 4, md: 3 }}>
              <Metric label="Detected Faces" value={String(detectedFaces)} />
            </Grid>
            <Grid size={{ xs: 6, sm: 4, md: 3 }}>
              <Metric label="Unique Students" value={String(matchedFaces)} />
            </Grid>
            <Grid size={{ xs: 6, sm: 4, md: 3 }}>
              <Metric label="Duplicate Faces" value={String(duplicateFaces)} />
            </Grid>
            <Grid size={{ xs: 6, sm: 4, md: 3 }}>
              <Metric label="Unknown Faces" value={String(unknownFaces ?? Math.max(0, detectedFaces - matchedFaces))} />
            </Grid>
            <Grid size={{ xs: 6, sm: 4, md: 3 }}>
              <Metric label="Average Image Quality" value={`${quality.stars} ${quality.shortLabel}`} />
            </Grid>
            <Grid size={{ xs: 6, sm: 4, md: 3 }}>
              <Metric label="Recognition Progress" value={progress} />
            </Grid>
            <Grid size={{ xs: 6, sm: 4, md: 3 }}>
              <Metric label="Readiness" value={readiness.label} />
            </Grid>
          </Grid>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card variant="outlined" aria-label="Recognition coverage summary">
      <CardContent sx={{ py: 1.75, "&:last-child": { pb: 1.75 } }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.25 }}>
          Recognition Coverage Summary
        </Typography>
        <Box
          sx={{
            display: "grid",
            gridTemplateColumns: { xs: "1fr 1fr", sm: "repeat(3, 1fr)", md: "repeat(6, 1fr)" },
            gap: 1.5,
          }}
        >
          <Metric label="Images Uploaded" value={String(images.length)} />
          <Metric label="Estimated Faces" value={detectedFaces > 0 ? String(detectedFaces) : "Pending"} />
          <Metric label="Image Quality" value={`${quality.stars} ${quality.shortLabel}`} />
          <Metric label="Average Resolution" value={averageResolution(images)} />
          <Metric label="Ready for Recognition" value={readiness.label} />
          <Metric label="Recognition Status" value={status} />
        </Box>
      </CardContent>
    </Card>
  );
};

export default RecognitionCoverageSummary;
