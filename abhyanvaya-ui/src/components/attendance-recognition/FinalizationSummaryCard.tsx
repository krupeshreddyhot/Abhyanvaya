import { Box, Card, CardContent, Typography } from "@mui/material";
import { memo } from "react";
import type { FinalizationStatusDto } from "../../services/attendanceRecognitionService";
import { AnimatedCount } from "../common/AnimatedCount";

type FinalizationSummaryCardProps = {
  status: FinalizationStatusDto | null;
};

const METRICS: { key: "studentsPresent" | "studentsAbsent" | "manualOverrides" | "unknownFaces" | "totalStudents"; label: string }[] = [
  { key: "studentsPresent", label: "Students present" },
  { key: "studentsAbsent", label: "Students absent" },
  { key: "manualOverrides", label: "Manual corrections" },
  { key: "unknownFaces", label: "Unknown faces" },
  { key: "totalStudents", label: "Total students" },
];

function resolveTone(status: FinalizationStatusDto | null): "success" | "warning" | "error" {
  if (!status) {
    return "warning";
  }

  if (status.canFinalize) {
    return "success";
  }

  if (status.attendanceAlreadyGenerated) {
    return "warning";
  }

  return "error";
}

export const FinalizationSummaryCard = memo(function FinalizationSummaryCard({
  status,
}: FinalizationSummaryCardProps) {
  if (!status) {
    return null;
  }

  const tone = resolveTone(status);
  const borderColor =
    tone === "success" ? "success.main" : tone === "warning" ? "warning.main" : "error.main";

  return (
    <Card variant="outlined" sx={{ borderColor, borderWidth: 2 }}>
      <CardContent>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
          <Typography variant="h6">Finalization summary</Typography>
          <Typography variant="caption" color={`${tone}.main`} sx={{ fontWeight: 700 }}>
            {status.canFinalize
              ? "Ready to finalize"
              : status.attendanceAlreadyGenerated
                ? "Attendance already generated"
                : "Finalization blocked"}
          </Typography>
        </Box>

        <Box
          sx={{
            display: "grid",
            gridTemplateColumns: {
              xs: "repeat(2, minmax(0, 1fr))",
              sm: "repeat(3, minmax(0, 1fr))",
              md: "repeat(5, minmax(0, 1fr))",
            },
            gap: 1.5,
          }}
        >
          {METRICS.map(({ key, label }) => (
            <Box
              key={key}
              sx={{
                border: 1,
                borderColor: "divider",
                borderRadius: 1,
                p: 1.25,
                textAlign: "center",
              }}
            >
              <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                {label}
              </Typography>
              <Typography variant="h6" sx={{ fontWeight: 700 }}>
                <AnimatedCount value={Number(status[key] ?? 0)} />
              </Typography>
            </Box>
          ))}
        </Box>
      </CardContent>
    </Card>
  );
});
