import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Alert,
} from "@mui/material";
import { academicTouchButtonSx } from "./academicUiTokens";

export type AcademicConfirmDialogProps = {
  open: boolean;
  title: string;
  description: string;
  confirmLabel?: string;
  cancelLabel?: string;
  confirmColor?: "error" | "primary" | "warning";
  confirming?: boolean;
  onCancel: () => void;
  onConfirm: () => void;
};

/**
 * AI29.1D Prompt 17 — shared confirmation dialog (replaces window.confirm).
 * Matches existing FinalizeAttendanceDialog / AI31 dialog density.
 */
export default function AcademicConfirmDialog({
  open,
  title,
  description,
  confirmLabel = "Confirm",
  cancelLabel = "Cancel",
  confirmColor = "error",
  confirming = false,
  onCancel,
  onConfirm,
}: AcademicConfirmDialogProps) {
  return (
    <Dialog
      open={open}
      onClose={confirming ? undefined : onCancel}
      maxWidth="xs"
      fullWidth
      aria-labelledby="academic-confirm-title"
      aria-describedby="academic-confirm-description"
    >
      <DialogTitle id="academic-confirm-title" sx={{ fontWeight: 800 }}>
        {title}
      </DialogTitle>
      <DialogContent>
        <Alert id="academic-confirm-description" severity="warning" variant="outlined" sx={{ mb: 0 }}>
          {description}
        </Alert>
      </DialogContent>
      <DialogActions sx={{ px: 2, pb: 2, flexWrap: "wrap", gap: 1 }}>
        <Button onClick={onCancel} disabled={confirming} sx={academicTouchButtonSx}>
          {cancelLabel}
        </Button>
        <Button
          variant="contained"
          color={confirmColor}
          onClick={onConfirm}
          disabled={confirming}
          sx={academicTouchButtonSx}
        >
          {confirmLabel}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
