import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  Typography,
} from "@mui/material";
import type { FinalizationStatusDto } from "../../services/attendanceRecognitionService";

type FinalizeAttendanceDialogProps = {
  open: boolean;
  status: FinalizationStatusDto | null;
  sessionId: string;
  onClose: () => void;
  onConfirm: () => void;
  confirming?: boolean;
};

export function FinalizeAttendanceDialog({
  open,
  status,
  sessionId,
  onClose,
  onConfirm,
  confirming = false,
}: FinalizeAttendanceDialogProps) {
  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>Finalize attendance</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          <Typography variant="body2">
            Session: {sessionId}
            {status?.attendanceDate
              ? ` · ${new Date(status.attendanceDate).toLocaleDateString()}`
              : ""}
          </Typography>
          <Typography variant="body2">
            Faculty: {status?.facultyName ?? "—"}
          </Typography>
          <Typography variant="body2">
            Subject: {status?.subjectName ?? "—"}
          </Typography>

          <Stack spacing={0.5}>
            <Typography variant="body2">Present: {status?.studentsPresent ?? 0}</Typography>
            <Typography variant="body2">Absent: {status?.studentsAbsent ?? 0}</Typography>
            <Typography variant="body2">Manual corrections: {status?.manualOverrides ?? 0}</Typography>
            <Typography variant="body2">Unknown faces: {status?.unknownFaces ?? 0}</Typography>
          </Stack>

          <Alert severity="warning">
            Official attendance will be generated. This action cannot be undone from the review screen.
          </Alert>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={confirming}>
          Cancel
        </Button>
        <Button
          variant="contained"
          color="success"
          disabled={confirming || !status?.canFinalize}
          onClick={onConfirm}
        >
          {confirming ? "Finalizing…" : "Confirm finalization"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
