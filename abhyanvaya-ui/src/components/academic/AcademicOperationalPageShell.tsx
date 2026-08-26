import { Alert, Box, Stack, Typography } from "@mui/material";
import type { ReactNode } from "react";
import { academicPageShellSx } from "./academicUiTokens";

export type AcademicOperationalPageShellProps = {
  title: string;
  subtitle?: ReactNode;
  /** Typically AcademicContextBreadcrumb */
  breadcrumb?: ReactNode;
  /** Sticky scope toolbar / primary actions */
  toolbar?: ReactNode;
  /** Secondary nav / links under title */
  headerActions?: ReactNode;
  error?: string | null;
  onClearError?: () => void;
  message?: string | null;
  onClearMessage?: () => void;
  children: ReactNode;
  /** Optional aria label for the main landmark */
  ariaLabel?: string;
};

/**
 * AI29.1D Prompt 17 — shared page chrome aligned with AI31 Enterprise Dashboard.
 * Presentation only; callers keep their data loaders and handlers.
 */
export default function AcademicOperationalPageShell({
  title,
  subtitle,
  breadcrumb,
  toolbar,
  headerActions,
  error,
  onClearError,
  message,
  onClearMessage,
  children,
  ariaLabel,
}: AcademicOperationalPageShellProps) {
  return (
    <Box component="main" aria-label={ariaLabel ?? title} sx={academicPageShellSx}>
      <Stack
        direction={{ xs: "column", sm: "row" }}
        spacing={1}
        useFlexGap
        sx={{ flexWrap: "wrap", alignItems: { sm: "flex-start" }, mb: 0.75 }}
      >
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Typography variant="h5" sx={{ fontWeight: 800, letterSpacing: "-0.02em" }}>
            {title}
          </Typography>
          {breadcrumb ? <Box sx={{ mt: 0.5 }}>{breadcrumb}</Box> : null}
          {subtitle ? (
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
              {subtitle}
            </Typography>
          ) : null}
        </Box>
        {headerActions ? (
          <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap", alignItems: "center" }}>
            {headerActions}
          </Stack>
        ) : null}
      </Stack>

      {error ? (
        <Alert severity="error" sx={{ mb: 1.25 }} onClose={onClearError}>
          {error}
        </Alert>
      ) : null}
      {message ? (
        <Alert severity="success" sx={{ mb: 1.25 }} onClose={onClearMessage}>
          {message}
        </Alert>
      ) : null}

      {toolbar}
      {children}
    </Box>
  );
}
