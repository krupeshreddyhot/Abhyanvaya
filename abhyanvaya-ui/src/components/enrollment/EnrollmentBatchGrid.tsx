import {
  Box,
  Chip,
  IconButton,
  LinearProgress,
  Paper,
  Skeleton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
} from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import VisibilityOutlinedIcon from "@mui/icons-material/VisibilityOutlined";
import CancelOutlinedIcon from "@mui/icons-material/CancelOutlined";
import ReplayOutlinedIcon from "@mui/icons-material/ReplayOutlined";
import { useEnrollmentDashboard } from "../../context/EnrollmentDashboardContext";
import EnrollmentProgressBar from "./EnrollmentProgressBar";
import { batchStatusLabel, resolveBatchProgressPercent, resolveBatchStudentCounts } from "./enrollmentMappers";
import { BatchStatus } from "../../types/enrollment";

type Props = {
  onViewBatch: (batchId: string) => void;
};

const EnrollmentBatchGrid = ({ onViewBatch }: Props) => {
  const { batches, batchProgress, loading, refreshBatches, cancelBatch, retryBatch, canManage } =
    useEnrollmentDashboard();

  if (loading && batches.length === 0) {
    return <Skeleton variant="rounded" height={240} aria-label="Loading batches" />;
  }

  if (batches.length === 0) {
    return (
      <Paper variant="outlined" sx={{ p: 3 }}>
        <Typography variant="body2" color="text.secondary">
          No enrollment batches yet. Start a batch when readiness checks pass.
        </Typography>
      </Paper>
    );
  }

  return (
    <Box>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}>
        <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
          Recent Enrollment Batches
        </Typography>
        <Tooltip title="Refresh batch list">
          <IconButton size="small" onClick={() => void refreshBatches()} aria-label="Refresh batches">
            <RefreshIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </Stack>
      <TableContainer component={Paper} variant="outlined">
        <Table size="small" aria-label="Enrollment batches">
          <TableHead>
            <TableRow>
              <TableCell>Batch</TableCell>
              <TableCell>Status</TableCell>
              <TableCell align="right">Students</TableCell>
              <TableCell>Progress</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {batches.map((batch) => {
              const progress = batchProgress[batch.batchId];
              const counts = resolveBatchStudentCounts(batch, progress);
              const percent = resolveBatchProgressPercent(batch, progress);
              return (
                <TableRow key={batch.batchId} hover>
                  <TableCell>
                    <Typography variant="body2" sx={{ fontFamily: "monospace", fontSize: "0.75rem" }}>
                      {batch.batchId.slice(0, 8)}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {new Date(batch.createdUtc).toLocaleString()}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <Chip size="small" label={batchStatusLabel(batch.status)} variant="outlined" />
                  </TableCell>
                  <TableCell align="right">
                    <Typography variant="body2">{counts.label}</Typography>
                    {counts.failed > 0 ? (
                      <Typography variant="caption" color="error.main">
                        {counts.failed} failed
                      </Typography>
                    ) : null}
                  </TableCell>
                  <TableCell sx={{ minWidth: 200 }}>
                    {progress ? (
                      <EnrollmentProgressBar progress={progress} totalStudents={batch.totalStudents} />
                    ) : (
                      <Stack spacing={0.5}>
                        <LinearProgress variant="determinate" value={percent} />
                        <Typography variant="caption" color="text.secondary">
                          {percent}% processed
                        </Typography>
                      </Stack>
                    )}
                  </TableCell>
                  <TableCell align="right">
                    <Stack direction="row" spacing={0.5} sx={{ justifyContent: "flex-end" }}>
                      <Tooltip title="View details">
                        <IconButton size="small" onClick={() => onViewBatch(batch.batchId)} aria-label="View batch">
                          <VisibilityOutlinedIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                      {canManage && (batch.status === BatchStatus.Running || batch.status === BatchStatus.Created) ? (
                        <Tooltip title="Cancel batch">
                          <IconButton
                            size="small"
                            onClick={() => void cancelBatch(batch.batchId)}
                            aria-label="Cancel batch"
                          >
                            <CancelOutlinedIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      ) : null}
                      {canManage &&
                      (batch.status === BatchStatus.PartiallyFailed || batch.status === BatchStatus.Cancelled) ? (
                        <Tooltip title="Retry batch">
                          <IconButton size="small" onClick={() => void retryBatch(batch.batchId)} aria-label="Retry batch">
                            <ReplayOutlinedIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      ) : null}
                    </Stack>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </TableContainer>
    </Box>
  );
};

export default EnrollmentBatchGrid;
