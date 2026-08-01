import { Box, Fab, Paper, Stack } from "@mui/material";
import PhotoCameraIcon from "@mui/icons-material/PhotoCamera";
import type { ReactNode } from "react";
import { MOBILE_TOUCH_TARGET_PX, safeAreaSx } from "./breakpoints";

export type OneHandedCaptureChromeProps = {
  children: ReactNode;
  onCapture?: () => void;
  captureDisabled?: boolean;
  captureLabel?: string;
};

/**
 * AI22.7C Phase 1.6 — thumb-reachable floating capture + safe-area padding.
 */
export function OneHandedCaptureChrome({
  children,
  onCapture,
  captureDisabled = false,
  captureLabel = "Capture",
}: OneHandedCaptureChromeProps) {
  return (
    <Box
      sx={{
        position: "relative",
        ...safeAreaSx,
        // Extra room for FAB after safe-area bottom inset.
        pb: onCapture
          ? "max(80px, calc(64px + env(safe-area-inset-bottom)))"
          : safeAreaSx.pb,
      }}
    >
      {children}
      {onCapture ? (
        <Fab
          color="primary"
          aria-label={captureLabel}
          disabled={captureDisabled}
          onClick={onCapture}
          sx={{
            position: "fixed",
            right: { xs: 16, sm: 24 },
            bottom: {
              xs: "max(20px, calc(16px + env(safe-area-inset-bottom)))",
              sm: 28,
            },
            zIndex: (theme) => theme.zIndex.speedDial,
            width: MOBILE_TOUCH_TARGET_PX + 16,
            height: MOBILE_TOUCH_TARGET_PX + 16,
          }}
        >
          <PhotoCameraIcon />
        </Fab>
      ) : null}
    </Box>
  );
}

export type MobileBottomNavProps = {
  children: ReactNode;
};

/** Sticky bottom strip for one-handed actions (presentation only). */
export function MobileBottomNav({ children }: MobileBottomNavProps) {
  return (
    <Paper
      elevation={8}
      square
      sx={{
        position: "fixed",
        left: 0,
        right: 0,
        bottom: 0,
        zIndex: (theme) => theme.zIndex.appBar - 1,
        borderTop: 1,
        borderColor: "divider",
        px: 1.5,
        py: 1,
        ...safeAreaSx,
      }}
      role="navigation"
      aria-label="Mobile classroom actions"
    >
      <Stack direction="row" spacing={1} sx={{ justifyContent: "space-around", alignItems: "center" }}>
        {children}
      </Stack>
    </Paper>
  );
}
