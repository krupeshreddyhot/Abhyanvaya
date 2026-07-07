import AnalyticsOutlinedIcon from "@mui/icons-material/AnalyticsOutlined";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import { Box, Card, CardContent, Fade, Grid, Grow, Stack, Typography } from "@mui/material";
import { AIStatus, type AIStatus as AIStatusType } from "../../types/aiWorkflow";
import { AnimatedCount } from "../common/AnimatedCount";
import { AIStatusChip } from "../common/AIStatusChip";

export type RecognitionProgressSummaryProps = {
  detectedFaces: number;
  matchedFaces: number;
  reviewedFaces: number;
  recognitionAccuracy: number | null;
  status: AIStatusType;
};

type SummaryMetricProps = {
  label: string;
  value: number;
  decimals?: number;
  suffix?: string;
};

const SummaryMetric = ({ label, value, decimals = 0, suffix = "" }: SummaryMetricProps) => (
  <Grow in timeout={400}>
    <Card variant="outlined" sx={{ height: "100%" }}>
      <CardContent sx={{ py: 1.5, "&:last-child": { pb: 1.5 } }}>
        <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
          {label}
        </Typography>
        <AnimatedCount value={value} decimals={decimals} suffix={suffix} variant="h6" />
      </CardContent>
    </Card>
  </Grow>
);

const PRE_RECOGNITION_STATUSES: ReadonlySet<AIStatusType> = new Set([
  AIStatus.Ready,
  AIStatus.Uploading,
  AIStatus.Pending,
]);

export const shouldShowRecognitionMetrics = (status: AIStatusType): boolean =>
  !PRE_RECOGNITION_STATUSES.has(status);

export const RecognitionProgressSummary = ({
  detectedFaces,
  matchedFaces,
  reviewedFaces,
  recognitionAccuracy,
  status,
}: RecognitionProgressSummaryProps) => {
  const showMetrics = shouldShowRecognitionMetrics(status);

  return (
    <Card variant="outlined" aria-label="Recognition progress">
      <CardContent>
        <Stack spacing={2}>
          <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
            <AnalyticsOutlinedIcon color="primary" aria-hidden />
            <Typography variant="subtitle1" component="h3" sx={{ fontWeight: 600 }}>
              Recognition Progress
            </Typography>
          </Stack>

          {showMetrics ? (
            <Fade in timeout={400}>
              <Box>
                <Grid container spacing={2}>
                  <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                    <SummaryMetric label="Detected Faces" value={detectedFaces} />
                  </Grid>
                  <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                    <SummaryMetric label="Matched Faces" value={matchedFaces} />
                  </Grid>
                  <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                    <SummaryMetric label="Reviewed Faces" value={reviewedFaces} />
                  </Grid>
                  <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                    {recognitionAccuracy == null ? (
                      <Grow in timeout={400}>
                        <Card variant="outlined" sx={{ height: "100%" }}>
                          <CardContent sx={{ py: 1.5, "&:last-child": { pb: 1.5 } }}>
                            <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                              Recognition Accuracy
                            </Typography>
                            <Typography variant="h6" component="p">
                              —
                            </Typography>
                          </CardContent>
                        </Card>
                      </Grow>
                    ) : (
                      <SummaryMetric
                        label="Recognition Accuracy"
                        value={recognitionAccuracy}
                        decimals={1}
                        suffix="%"
                      />
                    )}
                  </Grid>
                </Grid>

                <Box sx={{ mt: 2 }}>
                  <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 0.5 }}>
                    Current Status
                  </Typography>
                  <AIStatusChip status={status} />
                </Box>
              </Box>
            </Fade>
          ) : (
            <Stack direction="row" spacing={1.5} sx={{ alignItems: "flex-start" }}>
              <InfoOutlinedIcon color="info" sx={{ mt: 0.25 }} aria-hidden />
              <Typography variant="body1" color="text.secondary">
                Waiting for classroom photo…
              </Typography>
            </Stack>
          )}
        </Stack>
      </CardContent>
    </Card>
  );
};

export default RecognitionProgressSummary;
