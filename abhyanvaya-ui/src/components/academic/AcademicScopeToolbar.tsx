import { Box, Paper, Stack, Typography } from "@mui/material";
import type { ReactNode } from "react";
import { academicToolbarPaperSx } from "./academicUiTokens";
import AcademicHelpHint from "./AcademicHelpHint";

export type AcademicScopeToolbarProps = {
  /** Scope selectors (AcademicScopeSelector) and compact filters */
  children: ReactNode;
  /** Primary actions (Load, Refresh, Create) — right-aligned on desktop */
  actions?: ReactNode;
  helpTitle?: string;
  helpBody?: string;
  label?: string;
};

/**
 * AI29.1D Prompt 17 — sticky densified scope toolbar (AI31 dashboard toolbar pattern).
 */
export default function AcademicScopeToolbar({
  children,
  actions,
  helpTitle,
  helpBody,
  label = "Academic scope",
}: AcademicScopeToolbarProps) {
  return (
    <Paper elevation={0} sx={academicToolbarPaperSx} component="section" aria-label={label}>
      <Stack
        direction={{ xs: "column", md: "row" }}
        spacing={1.25}
        useFlexGap
        sx={{ alignItems: { md: "flex-start" }, justifyContent: "space-between" }}
      >
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Stack direction="row" spacing={0.5} sx={{ alignItems: "center", mb: 0.75 }}>
            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700, letterSpacing: 0.4 }}>
              SCOPE
            </Typography>
            {helpTitle && helpBody ? <AcademicHelpHint title={helpTitle} body={helpBody} /> : null}
          </Stack>
          {children}
        </Box>
        {actions ? (
          <Stack
            direction="row"
            spacing={1}
            useFlexGap
            sx={{ flexWrap: "wrap", alignItems: "center", pt: { md: 2.5 } }}
          >
            {actions}
          </Stack>
        ) : null}
      </Stack>
    </Paper>
  );
}
