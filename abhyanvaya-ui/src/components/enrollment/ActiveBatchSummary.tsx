import { Alert, Paper, Stack, Typography } from "@mui/material";
import { useEnrollmentDashboard } from "../../context/EnrollmentDashboardContext";
import EnrollmentProgressBar from "./EnrollmentProgressBar";
import { BatchStatus } from "../../types/enrollment";
import { normalizeEnrollmentBatchId } from "../../api/enrollmentApiClient";

const ActiveBatchSummary = () => {
  const { dashboard, batches, batchProgress } = useEnrollmentDashboard();

  const runningBatchId = dashboard?.runningBatchId
    ? normalizeEnrollmentBatchId(dashboard.runningBatchId)
    : null;

  const activeBatch = runningBatchId
    ? batches.find((batch) => batch.batchId === runningBatchId) ?? null
    : batches.find(
        (batch) => batch.status === BatchStatus.Created || batch.status === BatchStatus.Running,
      ) ?? null;

  if (!activeBatch) {
    return null;
  }

  const progress = batchProgress[activeBatch.batchId];

  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Stack spacing={1.5}>
        <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
          Current Batch
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Batch {activeBatch.batchId.slice(0, 8)} · {activeBatch.totalStudents} students
        </Typography>
        {progress ? (
          <EnrollmentProgressBar progress={progress} totalStudents={activeBatch.totalStudents} label="Live progress" />
        ) : (
          <Alert severity="info" variant="outlined">
            Loading live progress…
          </Alert>
        )}
      </Stack>
    </Paper>
  );
};

export default ActiveBatchSummary;
