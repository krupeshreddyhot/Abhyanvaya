import PhotoCameraIcon from "@mui/icons-material/PhotoCamera";
import { Box, Button, Paper, Stack, Typography } from "@mui/material";
import type { ReactNode } from "react";
import { MOBILE_TOUCH_TARGET_PX, safeAreaSx } from "./breakpoints";

export type MobileCaptureLayoutProps = {
  /** Primary capture / upload workspace (camera + drop zone). */
  capture: ReactNode;
  /** Compact status (progress, readiness) — kept visible on mobile. */
  status?: ReactNode;
  /** Secondary desktop panels — hidden on phone. */
  secondary?: ReactNode;
  /** Bottom sticky action label. */
  primaryActionLabel?: string;
  onPrimaryAction?: () => void;
  primaryActionDisabled?: boolean;
  showPrimaryAction?: boolean;
  title?: string;
};

/**
 * AI22.7C Phase 1.1 — Mobile Capture Workspace layout.
 * Presentation-only wrapper; does not change attendance workflow.
 */
export function MobileCaptureLayout({
  capture,
  status,
  secondary,
  primaryActionLabel = "Capture classroom photo",
  onPrimaryAction,
  primaryActionDisabled = false,
  showPrimaryAction = true,
  title = "Mobile Capture",
}: MobileCaptureLayoutProps) {
  return (
    <Box
      className="enterprise-mobile-capture"
      sx={{
        display: "flex",
        flexDirection: "column",
        gap: 1.5,
        minHeight: { xs: "60vh", sm: "auto" },
        ...safeAreaSx,
      }}
      data-mobility="mobile-capture"
    >
      <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
        {title}
      </Typography>

      <Paper
        variant="outlined"
        sx={{
          p: 1,
          flex: 1,
          minHeight: { xs: 280, sm: 360 },
          display: "flex",
          flexDirection: "column",
          bgcolor: "background.paper",
          "& video, & canvas, & img.enterprise-media": {
            width: "100%",
            maxHeight: { xs: "52vh", sm: "60vh" },
            objectFit: "contain",
            borderRadius: 1,
          },
        }}
      >
        {capture}
      </Paper>

      {status ? <Box sx={{ px: 0.5 }}>{status}</Box> : null}

      {/* Secondary panels stay in DOM for desktop parity when parent still renders them;
          this slot is intentionally unused on phone layouts that pass secondary separately. */}
      {secondary ? (
        <Box sx={{ display: { xs: "none", md: "block" } }} aria-hidden>
          {secondary}
        </Box>
      ) : null}

      {showPrimaryAction && onPrimaryAction ? (
        <Paper
          elevation={6}
          sx={{
            position: "sticky",
            bottom: 0,
            zIndex: (theme) => theme.zIndex.appBar - 2,
            p: 1.25,
            borderRadius: 2,
            border: 1,
            borderColor: "divider",
            bgcolor: "background.paper",
            ...safeAreaSx,
          }}
          role="toolbar"
          aria-label="Mobile capture actions"
        >
          <Stack spacing={1}>
            <Button
              fullWidth
              size="large"
              variant="contained"
              color="primary"
              startIcon={<PhotoCameraIcon />}
              disabled={primaryActionDisabled}
              onClick={onPrimaryAction}
              sx={{
                minHeight: MOBILE_TOUCH_TARGET_PX + 8,
                fontSize: "1.05rem",
                fontWeight: 700,
              }}
            >
              {primaryActionLabel}
            </Button>
          </Stack>
        </Paper>
      ) : null}
    </Box>
  );
}

export default MobileCaptureLayout;
