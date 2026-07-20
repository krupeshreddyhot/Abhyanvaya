import {
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  LinearProgress,
  Stack,
  Step,
  StepLabel,
  Stepper,
  Tooltip,
  Typography,
} from "@mui/material";
import PauseCircleOutlineIcon from "@mui/icons-material/PauseCircleOutlined";
import ReplayOutlinedIcon from "@mui/icons-material/ReplayOutlined";
import CancelOutlinedIcon from "@mui/icons-material/CancelOutlined";
import type { BatchProgressDto } from "../../types/enrollment";
import {
  ENROLLMENT_STAGES,
  getCurrentStageIndex,
  getStagePercentage,
} from "../../utils/enrollmentStageUtils";
import { formatDuration } from "./enrollmentMappers";

type Props = {
  progress: BatchProgressDto;
  totalStudents: number;
  workerLabel?: string;
  onCancel?: () => void;
  onRetry?: () => void;
  canManage?: boolean;
};

const EnrollmentStageProgress = ({
  progress,
  totalStudents,
  workerLabel = "Background workers",
  onCancel,
  onRetry,
  canManage = false,
}: Props) => {
  const currentIdx = getCurrentStageIndex(progress.state);

  return (
    <Stack spacing={2} aria-label="Stage-aware enrollment progress">
      <Stepper activeStep={currentIdx} alternativeLabel>
        {ENROLLMENT_STAGES.map((stage, idx) => (
          <Step key={stage.key} completed={idx < currentIdx}>
            <StepLabel>{stage.label}</StepLabel>
          </Step>
        ))}
      </Stepper>

      <Card variant="outlined">
        <CardContent>
          <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
              {ENROLLMENT_STAGES[currentIdx]?.label ?? "Processing"}
            </Typography>
            <Chip
              size="small"
              label={`${progress.percentage.toFixed(0)}% overall`}
              color="primary"
              variant="outlined"
            />
          </Stack>
          <LinearProgress
            variant="determinate"
            value={Math.min(100, progress.percentage)}
            sx={{ mb: 1, height: 8, borderRadius: 1 }}
            aria-valuenow={progress.percentage}
          />
          <Typography variant="caption" color="text.secondary">
            ETA {formatDuration(progress.estimatedRemaining)} · {workerLabel}
          </Typography>
        </CardContent>
      </Card>

      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", md: "repeat(2, 1fr)" },
          gap: 1.5,
        }}
      >
        {ENROLLMENT_STAGES.map((stage, idx) => {
          const count = stage.getCount(progress, totalStudents);
          const pct = getStagePercentage(count, totalStudents);
          const isCurrent = idx === currentIdx;
          const isPast = idx < currentIdx;
          return (
            <Card
              key={stage.key}
              variant="outlined"
              sx={{
                borderColor: isCurrent ? "primary.main" : undefined,
                bgcolor: isCurrent ? "action.selected" : undefined,
              }}
            >
              <CardContent sx={{ py: 1.5, "&:last-child": { pb: 1.5 } }}>
                <Stack spacing={0.75}>
                  <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center" }}>
                    <Typography variant="body2" sx={{ fontWeight: isCurrent ? 700 : 500 }}>
                      {stage.label}
                    </Typography>
                    {isCurrent ? <Chip size="small" label="Current" color="primary" /> : null}
                    {isPast ? <Chip size="small" label="Done" color="success" variant="outlined" /> : null}
                  </Stack>
                  <Typography variant="caption" color="text.secondary">
                    {count} / {totalStudents} ({pct}%)
                  </Typography>
                  <LinearProgress
                    variant="determinate"
                    value={pct}
                    color={isCurrent ? "primary" : "inherit"}
                    sx={{ height: 6, borderRadius: 1 }}
                  />
                  {isCurrent && progress.estimatedRemaining ? (
                    <Typography variant="caption" color="text.secondary">
                      Est. remaining {formatDuration(progress.estimatedRemaining)}
                    </Typography>
                  ) : null}
                </Stack>
              </CardContent>
            </Card>
          );
        })}
      </Box>

      {canManage ? (
        <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap" }}>
          <Tooltip title="Pause is planned for a future release">
            <span>
              <Button size="small" startIcon={<PauseCircleOutlineIcon />} disabled>
                Pause
              </Button>
            </span>
          </Tooltip>
          {onRetry ? (
            <Button size="small" startIcon={<ReplayOutlinedIcon />} onClick={onRetry}>
              Retry
            </Button>
          ) : null}
          {onCancel ? (
            <Button size="small" color="warning" startIcon={<CancelOutlinedIcon />} onClick={onCancel}>
              Cancel
            </Button>
          ) : null}
        </Stack>
      ) : null}
    </Stack>
  );
};

export default EnrollmentStageProgress;
