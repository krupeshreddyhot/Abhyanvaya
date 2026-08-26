import { Component, type ErrorInfo, type ReactNode } from "react";
import { Alert, Button, Stack, Typography } from "@mui/material";

type Props = { children: ReactNode };
type State = { error: Error | null };

/**
 * Prevents a white-screen crash on Mark Attendance (faculty cold start / Select edge cases).
 */
export default class AttendancePageErrorBoundary extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error("[AttendancePageErrorBoundary]", error, info.componentStack);
  }

  render() {
    if (!this.state.error) return this.props.children;

    return (
      <Stack spacing={2} sx={{ p: 2 }} role="alert">
        <Typography variant="h5" sx={{ fontWeight: 700 }}>
          Mark Attendance could not render
        </Typography>
        <Alert severity="error">
          {this.state.error.message || "Unexpected UI error."}
        </Alert>
        <Typography variant="body2" color="text.secondary">
          Try hard-refresh (Ctrl+F5). Manual attendance does not require a timetable assignment.
        </Typography>
        <Button
          variant="contained"
          onClick={() => {
            this.setState({ error: null });
            window.location.assign("/attendance?ai=0");
          }}
        >
          Reload attendance
        </Button>
      </Stack>
    );
  }
}
