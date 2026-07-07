import SettingsOutlinedIcon from "@mui/icons-material/SettingsOutlined";
import {
  Box,
  Card,
  CardContent,
  Fade,
  LinearProgress,
  Stack,
  Typography,
} from "@mui/material";
import { RecognitionQueueTimeIcon } from "../../utils/recognitionQueueDisplay";

export type RecognitionProcessingPanelProps = {
  progressPercent: number;
  currentStage?: string;
  currentOperation?: string;
  estimatedRemainingMilliseconds?: number | null;
  currentFileName?: string;
  messages?: string[];
  elapsedMilliseconds?: number | null;
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

export const RecognitionProcessingPanel = ({
  progressPercent,
  currentStage,
  currentOperation,
  estimatedRemainingMilliseconds,
  currentFileName,
  messages = [],
  elapsedMilliseconds,
}: RecognitionProcessingPanelProps) => (
  <Fade in timeout={400}>
    <Card variant="outlined" aria-label="Recognition processing">
      <CardContent sx={{ py: 2 }}>
        <Stack spacing={2}>
          <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
            <SettingsOutlinedIcon color="primary" aria-hidden />
            <Typography variant="subtitle1" component="h3" sx={{ fontWeight: 700 }}>
              Processing Status
            </Typography>
          </Stack>

          <Box>
            <Stack direction="row" sx={{ justifyContent: "space-between", mb: 0.75 }}>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {currentStage ?? "Processing"}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {Math.round(progressPercent)}%
              </Typography>
            </Stack>
            <LinearProgress
              variant="determinate"
              value={Math.min(100, Math.max(0, progressPercent))}
              aria-label="Recognition progress"
              sx={{
                height: 8,
                borderRadius: 1,
                transition: (theme) =>
                  theme.transitions.create("transform", { duration: theme.transitions.duration.standard }),
              }}
            />
          </Box>

          {currentOperation && (
            <Typography variant="body2" color="text.secondary">
              {currentOperation}
            </Typography>
          )}

          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <Stack direction="row" spacing={0.75} sx={{ alignItems: "center" }}>
              <RecognitionQueueTimeIcon fontSize="small" color="action" aria-hidden />
              <Typography variant="caption" color="text.secondary">
                Elapsed: {formatDuration(elapsedMilliseconds)}
              </Typography>
            </Stack>
            {estimatedRemainingMilliseconds != null && (
              <Typography variant="caption" color="text.secondary">
                Est. remaining: {formatDuration(estimatedRemainingMilliseconds)}
              </Typography>
            )}
            {currentFileName && (
              <Typography variant="caption" color="text.secondary" sx={{ wordBreak: "break-all" }}>
                File: {currentFileName}
              </Typography>
            )}
          </Stack>

          {messages.length > 0 && (
            <Stack spacing={0.5}>
              {messages.slice(0, 6).map((message) => (
                <Typography key={message} variant="caption" color="text.secondary">
                  • {message}
                </Typography>
              ))}
            </Stack>
          )}
        </Stack>
      </CardContent>
    </Card>
  </Fade>
);

export default RecognitionProcessingPanel;
