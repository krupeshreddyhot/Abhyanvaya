import { Box, Menu, MenuItem, Paper } from "@mui/material";
import { useTheme } from "@mui/material/styles";
import type { ReactNode } from "react";
import { TABLET_TOUCH_TARGET_PX } from "./breakpoints";
import { useMobilitySurface } from "./useMobilitySurface";

export type TabletReviewShellProps = {
  toolbar: ReactNode;
  photo: ReactNode;
  list: ReactNode;
  details: ReactNode;
  floatingActions?: ReactNode;
  photoFlex?: number;
  listFlex?: number;
};

/**
 * AI22.7C Phase 1.2 — Tablet Review Workspace shell.
 * Two-column landscape on tablets; stacks on narrow phones; desktop parent may ignore.
 */
export function TabletReviewShell({
  toolbar,
  photo,
  list,
  details,
  floatingActions,
  photoFlex = 42,
  listFlex = 32,
}: TabletReviewShellProps) {
  const { isTabletReview, isLandscape, isPhone } = useMobilitySurface();
  const theme = useTheme();
  const detailsFlex = Math.max(18, 100 - photoFlex - listFlex);

  if (!isTabletReview && !isPhone) {
    // Desktop: render a transparent pass-through grid identical to caller expectations.
    return (
      <Box data-mobility="desktop-passthrough">
        {toolbar}
        <Box
          sx={{
            display: "grid",
            gridTemplateColumns: `minmax(200px, ${photoFlex}%) minmax(0, ${listFlex}%) minmax(220px, ${detailsFlex}%)`,
            gap: 2,
            mt: 2,
          }}
        >
          {photo}
          {list}
          {details}
        </Box>
        {floatingActions}
      </Box>
    );
  }

  return (
    <Box data-mobility="tablet-review" sx={{ position: "relative" }}>
      <Paper
        variant="outlined"
        sx={{
          position: "sticky",
          top: 0,
          zIndex: theme.zIndex.appBar - 1,
          mb: 1.5,
          p: 1,
          "& .MuiButton-root, & .MuiIconButton-root": {
            minHeight: TABLET_TOUCH_TARGET_PX,
            minWidth: TABLET_TOUCH_TARGET_PX,
          },
          "& .MuiChip-root": { height: 32 },
        }}
      >
        {toolbar}
      </Paper>

      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: isPhone
            ? "1fr"
            : isLandscape
              ? `minmax(240px, ${photoFlex}%) minmax(0, ${listFlex + detailsFlex}%)`
              : "1fr",
          gap: 1.5,
          minHeight: isLandscape ? "min(70vh, 860px)" : undefined,
          "& .MuiCard-root, & [role='option']": {
            minHeight: TABLET_TOUCH_TARGET_PX + 8,
          },
        }}
      >
        <Box sx={{ minWidth: 0 }}>{photo}</Box>
        <Box
          sx={{
            display: "grid",
            gridTemplateColumns: isPhone || !isLandscape ? "1fr" : `minmax(0, 58%) minmax(200px, 42%)`,
            gap: 1.5,
            minWidth: 0,
          }}
        >
          {list}
          {details}
        </Box>
      </Box>

      {floatingActions ? (
        <Box
          sx={{
            position: "fixed",
            right: 16,
            bottom: "max(16px, env(safe-area-inset-bottom))",
            zIndex: theme.zIndex.speedDial,
            display: "flex",
            flexDirection: "column",
            gap: 1,
          }}
        >
          {floatingActions}
        </Box>
      ) : null}
    </Box>
  );
}

export type GestureContextMenuProps = {
  anchor: { left: number; top: number } | null;
  open: boolean;
  onClose: () => void;
  onResetView?: () => void;
  onFitScreen?: () => void;
};

/** Long-press context menu for viewer (Phase 1.3). */
export function GestureContextMenu({
  anchor,
  open,
  onClose,
  onResetView,
  onFitScreen,
}: GestureContextMenuProps) {
  return (
    <Menu
      open={open}
      onClose={onClose}
      anchorReference="anchorPosition"
      anchorPosition={anchor ? { top: anchor.top, left: anchor.left } : undefined}
    >
      <MenuItem
        onClick={() => {
          onResetView?.();
          onClose();
        }}
      >
        Reset view
      </MenuItem>
      <MenuItem
        onClick={() => {
          onFitScreen?.();
          onClose();
        }}
      >
        Fit screen
      </MenuItem>
    </Menu>
  );
}
