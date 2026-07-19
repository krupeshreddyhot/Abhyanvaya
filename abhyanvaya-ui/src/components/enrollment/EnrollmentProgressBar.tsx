import { LinearProgress, Stack, Typography } from "@mui/material";
import type { BatchProgressDto } from "../../types/enrollment";
import { formatDuration } from "./enrollmentMappers";

type Props = {
  progress: BatchProgressDto;
  label?: string;
};

const EnrollmentProgressBar = ({ progress, label }: Props) => (
  <Stack spacing={0.75} aria-label={label ?? "Batch progress"}>
    <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center" }}>
      <Typography variant="body2" sx={{ fontWeight: 600 }}>
        {label ?? "Progress"}
      </Typography>
      <Typography variant="caption" color="text.secondary">
        {progress.percentage.toFixed(0)}%
        {progress.estimatedRemaining ? ` · ETA ${formatDuration(progress.estimatedRemaining)}` : ""}
      </Typography>
    </Stack>
    <LinearProgress variant="determinate" value={Math.min(100, progress.percentage)} />
    <Typography variant="caption" color="text.secondary">
      Completed {progress.completed} · Failed {progress.failed} · Queued {progress.queued}
    </Typography>
  </Stack>
);

export default EnrollmentProgressBar;
