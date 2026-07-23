import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from "react";
import { Alert, Snackbar } from "@mui/material";

export type AttendanceToastSeverity = "success" | "info" | "warning" | "error";

export type AttendanceToast = {
  message: string;
  severity: AttendanceToastSeverity;
};

type AttendanceSnackbarContextValue = {
  notify: (message: string, severity?: AttendanceToastSeverity) => void;
};

const AttendanceSnackbarContext = createContext<AttendanceSnackbarContextValue | null>(null);

export const AttendanceSnackbarProvider = ({ children }: { children: ReactNode }) => {
  const [toast, setToast] = useState<AttendanceToast | null>(null);

  const notify = useCallback((message: string, severity: AttendanceToastSeverity = "info") => {
    setToast({ message, severity });
  }, []);

  const value = useMemo(() => ({ notify }), [notify]);

  return (
    <AttendanceSnackbarContext.Provider value={value}>
      {children}
      <Snackbar
        open={Boolean(toast)}
        autoHideDuration={3500}
        onClose={() => setToast(null)}
        anchorOrigin={{ vertical: "bottom", horizontal: "center" }}
        role="status"
        aria-live="polite"
      >
        {toast ? (
          <Alert
            onClose={() => setToast(null)}
            severity={toast.severity}
            variant="filled"
            sx={{ width: "100%" }}
          >
            {toast.message}
          </Alert>
        ) : undefined}
      </Snackbar>
    </AttendanceSnackbarContext.Provider>
  );
};

export const useAttendanceSnackbar = (): AttendanceSnackbarContextValue => {
  const ctx = useContext(AttendanceSnackbarContext);
  if (!ctx) {
    return {
      notify: () => undefined,
    };
  }
  return ctx;
};
