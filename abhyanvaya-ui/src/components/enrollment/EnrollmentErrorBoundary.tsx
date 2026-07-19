import { Component, type ErrorInfo, type ReactNode } from "react";
import { Alert, Box, Button, Typography } from "@mui/material";

type Props = { children: ReactNode };
type State = { hasError: boolean; message: string };

export default class EnrollmentErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false, message: "" };

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, message: error.message };
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    console.error("Enrollment UI error", error, info);
  }

  render() {
    if (this.state.hasError) {
      return (
        <Box role="alert" sx={{ p: 3 }}>
          <Alert severity="error" sx={{ mb: 2 }}>
            {this.state.message || "Something went wrong loading enrollment data."}
          </Alert>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Refresh the page or contact support if the problem persists.
          </Typography>
          <Button variant="outlined" onClick={() => window.location.reload()}>
            Reload page
          </Button>
        </Box>
      );
    }

    return this.props.children;
  }
}
