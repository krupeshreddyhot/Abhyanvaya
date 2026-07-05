import { Box, Card, CardContent, Grid, Typography } from "@mui/material";
import { memo } from "react";
import type { RecognitionStatisticsDto } from "../../services/attendanceRecognitionService";
import { AnimatedCount } from "../common/AnimatedCount";

type RecognitionSummaryCardProps = {
  statistics: RecognitionStatisticsDto | null;
  canFinalize: boolean;
};

const METRICS: { key: keyof RecognitionStatisticsDto; label: string }[] = [
  { key: "detectedFaces", label: "Detected faces" },
  { key: "matched", label: "Matched" },
  { key: "approved", label: "Approved" },
  { key: "rejected", label: "Rejected" },
  { key: "manualOverrides", label: "Manual corrections" },
];

export const RecognitionSummaryCard = memo(function RecognitionSummaryCard({
  statistics,
  canFinalize,
}: RecognitionSummaryCardProps) {
  if (!statistics) {
    return null;
  }

  return (
    <Card variant="outlined">
      <CardContent>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
          <Typography variant="h6">Recognition summary</Typography>
          <Typography variant="caption" color={canFinalize ? "success.main" : "warning.main"}>
            {canFinalize ? "Ready to finalize" : "Review incomplete"}
          </Typography>
        </Box>

        <Grid container spacing={1.5}>
          {METRICS.map(({ key, label }) => (
            <Grid key={key} size={{ xs: 6, sm: 4, md: 2.4 }}>
              <Box
                sx={{
                  border: 1,
                  borderColor: "divider",
                  borderRadius: 1,
                  p: 1.25,
                  textAlign: "center",
                  height: "100%",
                }}
              >
                <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                  {label}
                </Typography>
                <Typography variant="h6" sx={{ fontWeight: 700 }}>
                  <AnimatedCount value={Number(statistics[key] ?? 0)} />
                </Typography>
              </Box>
            </Grid>
          ))}
          <Grid size={{ xs: 12, sm: 6, md: 2.4 }}>
            <Box
              sx={{
                border: 1,
                borderColor: "divider",
                borderRadius: 1,
                p: 1.25,
                textAlign: "center",
                height: "100%",
              }}
            >
              <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                Avg confidence
              </Typography>
              <Typography variant="h6" sx={{ fontWeight: 700 }}>
                {statistics.averageConfidence != null
                  ? `${statistics.averageConfidence.toFixed(1)}%`
                  : "—"}
              </Typography>
            </Box>
          </Grid>
        </Grid>
      </CardContent>
    </Card>
  );
});
