import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import RadioButtonUncheckedIcon from "@mui/icons-material/RadioButtonUnchecked";
import {
  Box,
  Card,
  CardContent,
  LinearProgress,
  Stack,
  Typography,
} from "@mui/material";
import type { AIStatus, AIWorkflowStep } from "../../types/aiWorkflow";
import { BackendRecognitionQueueStatus } from "../../types/liveSessionStatus";
import { AIStatus as Status } from "../../types/aiWorkflow";

export type RecognitionProgressTimelineProps = {
  workflowStep: AIWorkflowStep;
  status: AIStatus;
  queueStatus?: number | null;
  progressPercent?: number;
  currentStage?: string;
  currentOperation?: string;
  elapsedMilliseconds?: number | null;
};

type TimelineStepKey = "Upload" | "Queued" | "Detect" | "Match" | "Review" | "Finalize";

const STEPS: TimelineStepKey[] = ["Upload", "Queued", "Detect", "Match", "Review", "Finalize"];

const resolveActiveIndex = (
  workflowStep: AIWorkflowStep,
  status: AIStatus,
  queueStatus?: number | null,
): number => {
  if (status === Status.Failed || queueStatus === BackendRecognitionQueueStatus.Failed) {
    return Math.max(0, STEPS.indexOf("Detect"));
  }

  if (
    status === Status.AwaitingReview ||
    status === Status.Completed ||
    queueStatus === BackendRecognitionQueueStatus.AwaitingReview ||
    queueStatus === BackendRecognitionQueueStatus.Completed
  ) {
    return STEPS.indexOf("Review");
  }

  if (status === Status.Matching || queueStatus === BackendRecognitionQueueStatus.Matching) {
    return STEPS.indexOf("Match");
  }

  if (
    status === Status.Processing ||
    queueStatus === BackendRecognitionQueueStatus.Detecting ||
    queueStatus === BackendRecognitionQueueStatus.Saving ||
    queueStatus === BackendRecognitionQueueStatus.WorkerPicked
  ) {
    return STEPS.indexOf("Detect");
  }

  if (
    status === Status.Pending ||
    status === Status.Uploading ||
    queueStatus === BackendRecognitionQueueStatus.Queued ||
    queueStatus === BackendRecognitionQueueStatus.Waiting
  ) {
    if (status === Status.Uploading) {
      return STEPS.indexOf("Upload");
    }
    return STEPS.indexOf("Queued");
  }

  switch (workflowStep) {
    case "Finalize":
      return STEPS.indexOf("Finalize");
    case "Review":
      return STEPS.indexOf("Review");
    case "Match":
      return STEPS.indexOf("Match");
    case "Detect":
      return STEPS.indexOf("Detect");
    default:
      return STEPS.indexOf("Upload");
  }
};

const formatDuration = (milliseconds?: number | null): string => {
  if (milliseconds == null || milliseconds <= 0) {
    return "—";
  }
  const totalSeconds = Math.floor(milliseconds / 1000);
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
};

export const RecognitionProgressTimeline = ({
  workflowStep,
  status,
  queueStatus,
  progressPercent = 0,
  currentStage,
  currentOperation,
  elapsedMilliseconds,
}: RecognitionProgressTimelineProps) => {
  const activeIndex = resolveActiveIndex(workflowStep, status, queueStatus);
  const failed = status === Status.Failed || queueStatus === BackendRecognitionQueueStatus.Failed;

  return (
    <Card variant="outlined" aria-label="Recognition progress timeline">
      <CardContent sx={{ py: 2 }}>
        <Stack spacing={2}>
          <Typography variant="subtitle1" component="h3" sx={{ fontWeight: 700 }}>
            Recognition Progress
          </Typography>

          <Stack
            direction={{ xs: "column", md: "row" }}
            spacing={1}
            sx={{ alignItems: { md: "center" }, flexWrap: "wrap" }}
            role="list"
            aria-label="Recognition stages"
          >
            {STEPS.map((step, index) => {
              const completed = index < activeIndex && !failed;
              const active = index === activeIndex;
              return (
                <Stack
                  key={step}
                  direction="row"
                  spacing={1}
                  role="listitem"
                  sx={{ alignItems: "center" }}
                  aria-current={active ? "step" : undefined}
                >
                  <Box
                    sx={{
                      display: "flex",
                      alignItems: "center",
                      gap: 0.75,
                      px: 1,
                      py: 0.5,
                      borderRadius: 1,
                      bgcolor: completed
                        ? "success.light"
                        : active
                          ? failed
                            ? "error.light"
                            : "primary.light"
                          : "action.hover",
                      color: completed || active ? "text.primary" : "text.secondary",
                      fontWeight: active ? 700 : 500,
                      transition: (theme) =>
                        theme.transitions.create(["background-color", "box-shadow"], {
                          duration: theme.transitions.duration.short,
                        }),
                      boxShadow: active ? 1 : 0,
                    }}
                  >
                    {completed ? (
                      <CheckCircleIcon fontSize="small" color="success" aria-hidden />
                    ) : (
                      <RadioButtonUncheckedIcon
                        fontSize="small"
                        color={active ? (failed ? "error" : "primary") : "disabled"}
                        aria-hidden
                      />
                    )}
                    <Typography variant="body2">{step}</Typography>
                  </Box>
                  {index < STEPS.length - 1 && (
                    <Typography
                      variant="body2"
                      color="text.disabled"
                      sx={{ display: { xs: "none", md: "block" } }}
                      aria-hidden
                    >
                      ↓
                    </Typography>
                  )}
                </Stack>
              );
            })}
          </Stack>

          <Box>
            <Stack direction="row" sx={{ justifyContent: "space-between", mb: 0.5 }}>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {currentStage ?? STEPS[activeIndex]}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {Math.round(progressPercent)}%
              </Typography>
            </Stack>
            <LinearProgress
              variant="determinate"
              value={Math.min(100, Math.max(0, progressPercent))}
              color={failed ? "error" : "primary"}
              aria-label="Recognition progress percent"
              sx={{ height: 8, borderRadius: 1 }}
            />
          </Box>

          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            {currentOperation && (
              <Typography variant="caption" color="text.secondary">
                {currentOperation}
              </Typography>
            )}
            <Typography variant="caption" color="text.secondary">
              Elapsed: {formatDuration(elapsedMilliseconds)}
            </Typography>
          </Stack>
        </Stack>
      </CardContent>
    </Card>
  );
};

export default RecognitionProgressTimeline;
