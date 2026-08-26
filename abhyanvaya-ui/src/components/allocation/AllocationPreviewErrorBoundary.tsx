import { Component, type ErrorInfo, type ReactNode } from "react";
import { Alert, Button, Stack, Typography } from "@mui/material";

type Props = {
  children: ReactNode;
  /** Called when the fault UI is dismissed (state cleared). */
  onReset?: () => void;
  /** AI29.1D.24B.4A.2 — re-run Preview after recovery (shared /allocation/simulate path). */
  onPreview?: () => void;
  /** AI29.1D.24B.4A.2 — re-run Test Allocation after recovery. */
  onTestAllocation?: () => void;
};

type State = { hasError: boolean };

/**
 * AI29.1D.24B.4A.2 — Prevent optional diagnostic render faults from blanking the workspace.
 *
 * Recovery UI must NEVER expose:
 * - stack traces / component stacks
 * - checksums
 * - engine internals
 * - authorization claims
 * - internal API paths
 *
 * Technical diagnostics remain gated by Allocation.Operations.View elsewhere.
 */
export default class AllocationPreviewErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false };

  static getDerivedStateFromError(): State {
    return { hasError: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    // Console only — never render stack / paths / claims into the administrator UI.
    console.error("[AllocationPreviewErrorBoundary]", error?.name, info?.componentStack ? "(componentStack redacted from UI)" : "");
  }

  private clearError = () => {
    this.props.onReset?.();
    this.setState({ hasError: false });
  };

  private recoverPreview = () => {
    // Call parent first so children can leave the failing tree before remount.
    this.props.onPreview?.();
    this.props.onReset?.();
    this.setState({ hasError: false });
  };

  private recoverTestAllocation = () => {
    this.props.onTestAllocation?.();
    this.props.onReset?.();
    this.setState({ hasError: false });
  };

  render() {
    if (this.state.hasError) {
      return (
        <Stack spacing={1.5} data-testid="allocation-preview-error-recovery">
          <Alert severity="error">
            <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5 }}>
              Allocation preview could not be displayed
            </Typography>
            The allocation test completed, but part of the result could not be shown. Student records were not
            changed. You can try Preview or Test Allocation again.
          </Alert>
          <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
            <Button variant="contained" onClick={this.recoverPreview} data-testid="allocation-preview-recover-preview">
              Preview
            </Button>
            <Button
              variant="outlined"
              onClick={this.recoverTestAllocation}
              data-testid="allocation-preview-recover-test"
            >
              Test Allocation
            </Button>
            <Button variant="text" onClick={this.clearError} data-testid="allocation-preview-recover-dismiss">
              Dismiss
            </Button>
          </Stack>
        </Stack>
      );
    }
    return this.props.children;
  }
}
