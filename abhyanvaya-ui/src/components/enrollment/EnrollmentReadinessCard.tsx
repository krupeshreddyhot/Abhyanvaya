import { Card, CardContent, Chip, Skeleton, Stack, Typography } from "@mui/material";
import { useEnrollmentDashboard } from "../../context/EnrollmentDashboardContext";

const readinessChip = (ready: boolean) => (
  <Chip size="small" label={ready ? "Ready" : "Not Ready"} color={ready ? "success" : "warning"} variant="outlined" />
);

const EnrollmentReadinessCard = () => {
  const { readiness, dashboard, loading } = useEnrollmentDashboard();

  if (loading && !readiness) {
    return <Skeleton variant="rounded" height={160} aria-label="Loading readiness" />;
  }

  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1.5 }}>
          Enrollment Readiness
        </Typography>
        <Stack spacing={1}>
          <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", gap: 1 }}>
            {readinessChip(readiness?.photoProviderReady ?? false)}
            {readinessChip(readiness?.storageReady ?? false)}
            {readinessChip(readiness?.recognitionReady ?? false)}
            {readinessChip(readiness?.workerReady ?? false)}
            {readinessChip(readiness?.configurationValid ?? false)}
          </Stack>
          <Typography variant="body2" color="text.secondary">
            Eligible students: {readiness?.eligibleStudents ?? dashboard?.eligibleStudents ?? 0}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Queue length: {dashboard?.queueLength ?? 0}
          </Typography>
          {readiness?.runningBatchId ? (
            <Typography variant="body2" color="warning.main">
              Running batch: {readiness.runningBatchId}
            </Typography>
          ) : null}
        </Stack>
      </CardContent>
    </Card>
  );
};

export default EnrollmentReadinessCard;
