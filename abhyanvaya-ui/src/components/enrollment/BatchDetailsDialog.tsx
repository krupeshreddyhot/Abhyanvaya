import {
  Dialog,
  DialogContent,
  DialogTitle,
  IconButton,
  Skeleton,
  Stack,
  Tab,
  Tabs,
  Typography,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import { useEffect, useState } from "react";
import { enrollmentApiClient } from "../../api/enrollmentApiClient";
import { useEnrollmentDashboard } from "../../context/EnrollmentDashboardContext";
import EnrollmentStageProgress from "./EnrollmentStageProgress";
import EnrollmentTimeline from "./EnrollmentTimeline";
import StudentEnrollmentGrid from "./StudentEnrollmentGrid";
import FailurePanel from "./FailurePanel";
import { batchStatusLabel } from "./enrollmentMappers";

type Props = {
  open: boolean;
  batchId: string | null;
  onClose: () => void;
};

const BatchDetailsDialog = ({ open, batchId, onClose }: Props) => {
  const { selectedBatch, loadBatchDetail, batchProgress, cancelBatch, retryBatch, canManage } =
    useEnrollmentDashboard();
  const [tab, setTab] = useState(0);

  useEffect(() => {
    if (open && batchId) {
      void loadBatchDetail(batchId);
    }
  }, [open, batchId, loadBatchDetail]);

  const progress = batchId ? batchProgress[batchId] : undefined;
  const loaded = selectedBatch && batchId === selectedBatch.batchId;

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="lg" aria-labelledby="batch-details-title">
      <DialogTitle id="batch-details-title">
        <Stack direction="row" sx={{ alignItems: "center", justifyContent: "space-between" }}>
          <Typography variant="h6">Batch Details</Typography>
          <IconButton onClick={onClose} aria-label="Close batch details">
            <CloseIcon />
          </IconButton>
        </Stack>
      </DialogTitle>
      <DialogContent dividers>
        {loaded ? (
          <Stack spacing={2}>
            <Typography variant="body2" color="text.secondary">
              Status: {batchStatusLabel(selectedBatch.status)} · Pipeline v{selectedBatch.pipelineVersion}
            </Typography>
            {progress ? (
              <EnrollmentStageProgress
                progress={progress}
                totalStudents={selectedBatch.totalStudents}
                canManage={canManage}
                onCancel={batchId ? () => void cancelBatch(batchId) : undefined}
                onRetry={batchId ? () => void retryBatch(batchId) : undefined}
              />
            ) : (
              <Skeleton variant="rounded" height={120} aria-label="Loading live progress" />
            )}
            <EnrollmentTimeline
              progress={progress}
              batch={{
                createdUtc: selectedBatch.createdUtc,
                startedUtc: selectedBatch.startedUtc,
                completedUtc: selectedBatch.completedUtc,
                totalStudents: selectedBatch.totalStudents,
                failedCount: selectedBatch.failedCount,
              }}
            />
            <Tabs value={tab} onChange={(_, v) => setTab(v)} aria-label="Batch detail tabs">
              <Tab label="Students" />
              <Tab label="Failures" />
            </Tabs>
            {tab === 0 && batchId ? <StudentEnrollmentGrid batchId={batchId} /> : null}
            {tab === 1 && batchId ? (
              <FailurePanel
                batchId={batchId}
                fetchStudents={(filters) => enrollmentApiClient.getBatchStudents(batchId, filters)}
              />
            ) : null}
          </Stack>
        ) : (
          <Stack spacing={1}>
            <Skeleton variant="text" width="40%" />
            <Skeleton variant="rounded" height={160} />
          </Stack>
        )}
      </DialogContent>
    </Dialog>
  );
};

export default BatchDetailsDialog;
