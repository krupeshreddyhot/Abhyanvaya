import {
  Dialog,
  DialogContent,
  DialogTitle,
  IconButton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";

export const KEYBOARD_PRODUCTIVITY_SHORTCUTS = [
  { keys: "Space", action: "Next face" },
  { keys: "Shift+Space", action: "Previous face" },
  { keys: "Enter", action: "Approve" },
  { keys: "Delete", action: "Reject" },
  { keys: "Tab", action: "Next image" },
  { keys: "Shift+Tab", action: "Previous image" },
  { keys: "Ctrl+Z", action: "Undo" },
  { keys: "Ctrl+Y", action: "Redo" },
  { keys: "F", action: "Toggle fullscreen" },
  { keys: "H", action: "Toggle heat map" },
  { keys: "M", action: "Toggle mini map" },
  { keys: "Ctrl+M", action: "Manual match (focused face)" },
  { keys: "?", action: "Shortcut help" },
  { keys: "Esc", action: "Exit fullscreen / close help" },
  { keys: "A / R / N / P", action: "Approve / Reject / Next / Previous (legacy)" },
] as const;

export type KeyboardShortcutHelpDialogProps = {
  open: boolean;
  onClose: () => void;
};

/** AI22.7A Phase 5.7 — keyboard productivity shortcut overlay. */
export function KeyboardShortcutHelpDialog({ open, onClose }: KeyboardShortcutHelpDialogProps) {
  return (
    <Dialog open={open} onClose={onClose} aria-labelledby="keyboard-help-title" maxWidth="sm" fullWidth>
      <DialogTitle id="keyboard-help-title" sx={{ pr: 6 }}>
        Keyboard Productivity Mode
        <IconButton
          aria-label="Close shortcut help"
          onClick={onClose}
          sx={{ position: "absolute", right: 8, top: 8 }}
        >
          <CloseIcon />
        </IconButton>
      </DialogTitle>
      <DialogContent>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
          Review without the mouse. Pan with middle-click or Alt+drag. Space advances the next face.
        </Typography>
        <Table size="small" aria-label="Keyboard shortcuts">
          <TableHead>
            <TableRow>
              <TableCell>Shortcut</TableCell>
              <TableCell>Action</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {KEYBOARD_PRODUCTIVITY_SHORTCUTS.map((row) => (
              <TableRow key={row.keys}>
                <TableCell>
                  <Stack component="kbd" sx={{ fontFamily: "monospace", fontWeight: 700 }}>
                    {row.keys}
                  </Stack>
                </TableCell>
                <TableCell>{row.action}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </DialogContent>
    </Dialog>
  );
}

export default KeyboardShortcutHelpDialog;
