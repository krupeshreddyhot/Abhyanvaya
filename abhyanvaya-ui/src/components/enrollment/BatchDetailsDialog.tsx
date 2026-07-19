import {
  Dialog,
  DialogContent,
  DialogTitle,
  IconButton,
  Stack,
  Tab,
  Tabs,
  Typography,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import { useEffect, useState } from "react";
import { enrollmentApiClient } from "../../api/enrollmentApiClient";
import { useEnrollmentDashboard } from "../../context/EnrollmentDashboardContext";
import EnrollmentProgressBar from "./EnrollmentProgressBar";
import StudentEnrollmentGrid from "./StudentEnrollmentGrid";
import FailurePanel from "./FailurePanel";
import { batchStatusLabel, formatDuration } from "./enrollmentMappers";

type Props = {
  open: boolean;
  batchId: string | null;
  onClose: () => void;
};

const BatchDetailsDialog = ({ open, batchId, onClose }: Props) => {
  const { selectedBatch, loadBatchDetail, batchProgress } = useEnrollmentDashboard();
  const [tab, setTab] = useState(0);

  useEffect(() => {
    if (open && batchId) {
      void loadBatchDetail(batchId);
    }
  }, [open, batchId, loadBatchDetail]);

  const progress = batchId ? batchProgress[batchId] : undefined;

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
        {selectedBatch && batchId === selectedBatch.batchId ? (
          <Stack spacing={2}>
            <Typography variant="body2" color="text.secondary">
              Status: {batchStatusLabel(selectedBatch.status)} · Pipeline v{selectedBatch.pipelineVersion}
            </Typography>
            {progress ? <EnrollmentProgressBar progress={progress} label="Live progress" /> : null}
            <Typography variant="body2">
              ETA: {formatDuration(selectedBatch.estimatedRemaining)}
            </Typography>
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
          <Typography variant="body2" color="text.secondary">
            Loading batch details…
          </Typography>
        )}
      </DialogContent>
    </Dialog>
  );
};

export default BatchDetailsDialog;
