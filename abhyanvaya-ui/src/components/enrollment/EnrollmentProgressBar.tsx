import { LinearProgress, Stack, Typography } from "@mui/material";
import type { BatchProgressDto } from "../../types/enrollment";
import { formatDuration, computeBatchProgressPercent } from "./enrollmentMappers";

type Props = {
  progress: BatchProgressDto;
  totalStudents: number;
  label?: string;
};

const EnrollmentProgressBar = ({ progress, totalStudents, label }: Props) => {
  const percent = computeBatchProgressPercent(progress, totalStudents);

  return (
    <Stack spacing={0.75} aria-label={label ?? "Batch progress"}>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center" }}>
        <Typography variant="body2" sx={{ fontWeight: 600 }}>
          {label ?? "Progress"}
        </Typography>
        <Typography variant="caption" color="text.secondary">
          {percent}%
          {progress.estimatedRemaining ? ` · ETA ${formatDuration(progress.estimatedRemaining)}` : ""}
        </Typography>
      </Stack>
      <LinearProgress variant="determinate" value={percent} />
      <Typography variant="caption" color="text.secondary">
        Processed {progress.completed + progress.failed + progress.cancelled} · Completed {progress.completed} · Failed{" "}
        {progress.failed} (this batch) · Pending retry {progress.queued}
        {progress.downloading + progress.validating + progress.embedding > 0
          ? ` · In flight ${progress.downloading + progress.validating + progress.embedding}`
          : ""}
      </Typography>
    </Stack>
  );
};

export default EnrollmentProgressBar;
