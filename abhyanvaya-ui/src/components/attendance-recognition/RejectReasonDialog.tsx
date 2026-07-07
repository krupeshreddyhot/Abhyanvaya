import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  TextField,
} from "@mui/material";
import { useEffect, useState } from "react";

type RejectReasonDialogProps = {
  open: boolean;
  onClose: () => void;
  onConfirm: (reason: string) => void;
};

export function RejectReasonDialog({ open, onClose, onConfirm }: RejectReasonDialogProps) {
  const [reason, setReason] = useState("");

  useEffect(() => {
    if (open) {
      setReason("");
    }
  }, [open]);

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="xs">
      <DialogTitle>Reject recognition</DialogTitle>
      <DialogContent>
        <TextField
          autoFocus
          label="Reason (required)"
          fullWidth
          multiline
          minRows={2}
          value={reason}
          onChange={(event) => setReason(event.target.value)}
          sx={{ mt: 1 }}
        />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button
          color="error"
          variant="contained"
          disabled={!reason.trim()}
          onClick={() => onConfirm(reason.trim())}
        >
          Reject
        </Button>
      </DialogActions>
    </Dialog>
  );
}
