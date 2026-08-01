import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  Typography,
} from "@mui/material";
import { useMemo, useState } from "react";
import {
  formatAccessibilityReport,
  runAccessibilityChecker,
  type AccessibilityReport,
} from "./accessibilityChecker";

export type AccessibilityReportDialogProps = {
  open: boolean;
  onClose: () => void;
};

/** AI22.7B Phase 5.3 — in-app accessibility report dialog. */
export function AccessibilityReportDialog({ open, onClose }: AccessibilityReportDialogProps) {
  const [report, setReport] = useState<AccessibilityReport | null>(null);

  const text = useMemo(() => (report ? formatAccessibilityReport(report) : ""), [report]);

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="md" aria-labelledby="a11y-report-title">
      <DialogTitle id="a11y-report-title">Accessibility Report (WCAG 2.2 AA heuristics)</DialogTitle>
      <DialogContent>
        <Stack spacing={1.5}>
          <Typography variant="body2" color="text.secondary">
            Runs a lightweight on-page checker for missing labels, alt text, and sampled contrast. Not a
            substitute for axe or manual keyboard testing.
          </Typography>
          {report ? (
            <Typography
              component="pre"
              variant="caption"
              sx={{
                p: 1.5,
                borderRadius: 1,
                bgcolor: "action.hover",
                whiteSpace: "pre-wrap",
                fontFamily: "monospace",
                maxHeight: 360,
                overflow: "auto",
              }}
            >
              {text}
            </Typography>
          ) : (
            <Typography variant="body2">Click Run checker to generate a report for the current page.</Typography>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Close</Button>
        <Button
          variant="contained"
          onClick={() => setReport(runAccessibilityChecker(document))}
          aria-label="Run accessibility checker"
        >
          Run checker
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export default AccessibilityReportDialog;
