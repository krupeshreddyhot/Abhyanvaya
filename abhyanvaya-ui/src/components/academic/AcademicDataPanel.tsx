import {
  Box,
  CircularProgress,
  Stack,
  TableContainer,
  Typography,
} from "@mui/material";
import InboxOutlinedIcon from "@mui/icons-material/InboxOutlined";
import type { ReactNode } from "react";
import EmptyStateCard from "../common/EmptyStateCard";
import AcademicHelpHint from "./AcademicHelpHint";
import { academicPanelSx, type AcademicUiAccent } from "./academicUiTokens";

export type AcademicDataPanelProps = {
  title?: string;
  accent?: AcademicUiAccent;
  loading?: boolean;
  loadingLabel?: string;
  empty?: boolean;
  emptyTitle?: string;
  emptyDescription?: string;
  emptyAction?: ReactNode;
  toolbar?: ReactNode;
  helpTitle?: string;
  helpBody?: string;
  /** When true, wraps children in TableContainer with horizontal overflow for tablet. */
  scrollTable?: boolean;
  children: ReactNode;
};

/**
 * AI29.1D Prompt 17 — card panel with loading / empty / scrollable table host.
 */
export default function AcademicDataPanel({
  title,
  accent = "academic",
  loading = false,
  loadingLabel = "Loading…",
  empty = false,
  emptyTitle = "Nothing to show",
  emptyDescription,
  emptyAction,
  toolbar,
  helpTitle,
  helpBody,
  scrollTable = true,
  children,
}: AcademicDataPanelProps) {
  return (
    <Box sx={{ ...academicPanelSx(accent), bgcolor: "background.paper" }} component="section">
      {(title || toolbar || helpTitle) && (
        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={1}
          useFlexGap
          sx={{ mb: 1, alignItems: { sm: "center" }, justifyContent: "space-between" }}
        >
          {title ? (
            <Stack direction="row" spacing={0.5} sx={{ alignItems: "center" }}>
              <Typography variant="subtitle2" sx={{ fontWeight: 800 }}>
                {title}
              </Typography>
              {helpTitle && helpBody ? <AcademicHelpHint title={helpTitle} body={helpBody} /> : null}
            </Stack>
          ) : (
            <span />
          )}
          {toolbar}
        </Stack>
      )}

      {loading ? (
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", py: 3, justifyContent: "center" }}>
          <CircularProgress size={22} aria-label={loadingLabel} />
          <Typography variant="body2" color="text.secondary">
            {loadingLabel}
          </Typography>
        </Stack>
      ) : empty ? (
        <EmptyStateCard
          icon={<InboxOutlinedIcon />}
          title={emptyTitle}
          description={emptyDescription}
          action={emptyAction}
        />
      ) : scrollTable ? (
        <TableContainer sx={{ overflowX: "auto", WebkitOverflowScrolling: "touch", maxWidth: "100%" }}>
          {children}
        </TableContainer>
      ) : (
        children
      )}
    </Box>
  );
}
