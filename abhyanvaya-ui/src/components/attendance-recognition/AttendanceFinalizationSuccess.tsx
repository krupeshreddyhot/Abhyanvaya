import {
  Box,
  Button,
  Card,
  CardContent,
  Stack,
  Typography,
} from "@mui/material";
import { memo } from "react";
import type { AttendanceBuildSummaryDto } from "../../services/attendanceRecognitionService";

type AttendanceFinalizationSuccessProps = {
  summary: AttendanceBuildSummaryDto;
  sessionId: string;
  onViewAttendance: () => void;
  onPrint: () => void;
  onReturn: () => void;
};

export const AttendanceFinalizationSuccess = memo(function AttendanceFinalizationSuccess({
  summary,
  sessionId,
  onViewAttendance,
  onPrint,
  onReturn,
}: AttendanceFinalizationSuccessProps) {
  const generatedLabel = summary.generatedUtc
    ? new Date(summary.generatedUtc).toLocaleString()
    : "—";

  return (
    <Card variant="outlined" sx={{ borderColor: "success.main", borderWidth: 2 }}>
      <CardContent>
        <Stack spacing={2}>
          <Typography variant="h5" color="success.main">
            Attendance generated
          </Typography>

          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: { xs: "1fr", sm: "repeat(2, minmax(0, 1fr))" },
              gap: 1.5,
            }}
          >
            <Typography variant="body2">Present: {summary.present}</Typography>
            <Typography variant="body2">Absent: {summary.absent}</Typography>
            <Typography variant="body2">Manual corrections: {summary.manualCorrections}</Typography>
            <Typography variant="body2">
              Duration: {summary.durationMilliseconds != null ? `${summary.durationMilliseconds} ms` : "—"}
            </Typography>
            <Typography variant="body2">Generated: {generatedLabel}</Typography>
            <Typography variant="body2">Session ID: {sessionId}</Typography>
          </Box>

          <Stack direction={{ xs: "column", sm: "row" }} spacing={1}>
            <Button variant="contained" onClick={onViewAttendance}>
              View attendance
            </Button>
            <Button variant="outlined" onClick={onPrint}>
              Print
            </Button>
            <Button variant="text" onClick={onReturn}>
              Return
            </Button>
          </Stack>
        </Stack>
      </CardContent>
    </Card>
  );
});
