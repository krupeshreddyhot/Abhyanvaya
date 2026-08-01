import FullscreenExitIcon from "@mui/icons-material/FullscreenExit";
import FullscreenIcon from "@mui/icons-material/Fullscreen";
import HelpOutlineIcon from "@mui/icons-material/HelpOutlineOutlined";
import RedoIcon from "@mui/icons-material/Redo";
import TimerOutlinedIcon from "@mui/icons-material/TimerOutlined";
import UndoIcon from "@mui/icons-material/Undo";
import {
  Box,
  Button,
  Chip,
  IconButton,
  LinearProgress,
  Paper,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import type { SessionProductivityMetrics } from "../../utils/reviewAnalytics";
import { SessionProductivityStrip } from "./SessionProductivityStrip";

export type StickyReviewToolbarProps = {
  pendingCount: number;
  totalCount: number;
  selectedCount: number;
  sessionElapsedLabel: string;
  averageReviewLabel: string;
  remainingLabel: string;
  canUndo: boolean;
  canRedo: boolean;
  disabled: boolean;
  fullscreen?: boolean;
  productivity?: SessionProductivityMetrics | null;
  onApproveSelected: () => void;
  onRejectSelected: () => void;
  onManualMatchSelected: () => void;
  onMarkUnknownSelected: () => void;
  onUndo: () => void;
  onRedo: () => void;
  onToggleFullscreen?: () => void;
  onOpenShortcutHelp?: () => void;
};

/** AI22.7A Phase 4.4/4.5 + Phase 5.1/5.8 — sticky productivity toolbar. */
export function StickyReviewToolbar({
  pendingCount,
  totalCount,
  selectedCount,
  sessionElapsedLabel,
  averageReviewLabel,
  remainingLabel,
  canUndo,
  canRedo,
  disabled,
  fullscreen = false,
  productivity = null,
  onApproveSelected,
  onRejectSelected,
  onManualMatchSelected,
  onMarkUnknownSelected,
  onUndo,
  onRedo,
  onToggleFullscreen,
  onOpenShortcutHelp,
}: StickyReviewToolbarProps) {
  const progress = totalCount > 0 ? ((totalCount - pendingCount) / totalCount) * 100 : 0;

  return (
    <Paper
      variant="outlined"
      sx={{
        p: 1.5,
        position: "sticky",
        top: 0,
        zIndex: (theme) => theme.zIndex.appBar - 1,
        bgcolor: "background.paper",
        borderColor: "divider",
        "@media (prefers-reduced-motion: no-preference)": {
          transition: (theme) => theme.transitions.create(["box-shadow"], { duration: theme.transitions.duration.shorter }),
        },
      }}
      role="toolbar"
      aria-label="Sticky review productivity toolbar"
    >
      <Stack spacing={1.25}>
        <Stack
          direction={{ xs: "column", md: "row" }}
          spacing={1}
          sx={{ justifyContent: "space-between", alignItems: { md: "center" } }}
        >
          <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", gap: 0.75, alignItems: "center" }}>
            <Chip size="small" icon={<TimerOutlinedIcon />} label={`Session ${sessionElapsedLabel}`} />
            <Chip size="small" label={`Remaining ${remainingLabel}`} color={pendingCount > 0 ? "warning" : "success"} />
            <Chip size="small" label={`Avg ${averageReviewLabel}`} variant="outlined" />
            <Chip size="small" label={`${selectedCount} selected`} variant="outlined" />
            {fullscreen ? <Chip size="small" color="primary" label="Full screen" /> : null}
          </Stack>

          <Stack direction="row" spacing={0.75} sx={{ flexWrap: "wrap", gap: 0.75, alignItems: "center" }}>
            <Tooltip title={fullscreen ? "Exit full screen (Esc / F)" : "Full screen workspace (F)"}>
              <IconButton size="small" onClick={onToggleFullscreen} aria-label={fullscreen ? "Exit full screen" : "Enter full screen"}>
                {fullscreen ? <FullscreenExitIcon fontSize="small" /> : <FullscreenIcon fontSize="small" />}
              </IconButton>
            </Tooltip>
            <Tooltip title="Keyboard shortcuts (?)">
              <IconButton size="small" onClick={onOpenShortcutHelp} aria-label="Open shortcut help">
                <HelpOutlineIcon fontSize="small" />
              </IconButton>
            </Tooltip>
            <Tooltip title="Undo last review action (Ctrl+Z)">
              <span>
                <Button size="small" startIcon={<UndoIcon />} disabled={disabled || !canUndo} onClick={onUndo}>
                  Undo
                </Button>
              </span>
            </Tooltip>
            <Tooltip title="Redo (Ctrl+Y)">
              <span>
                <Button size="small" startIcon={<RedoIcon />} disabled={disabled || !canRedo} onClick={onRedo}>
                  Redo
                </Button>
              </span>
            </Tooltip>
            <Button size="small" variant="contained" color="success" disabled={disabled || selectedCount === 0} onClick={onApproveSelected}>
              Approve selected
            </Button>
            <Button size="small" variant="outlined" color="error" disabled={disabled || selectedCount === 0} onClick={onRejectSelected}>
              Reject selected
            </Button>
            <Button size="small" variant="outlined" disabled={disabled || selectedCount !== 1} onClick={onManualMatchSelected}>
              Manual match selected
            </Button>
            <Button size="small" variant="outlined" disabled={disabled || selectedCount === 0} onClick={onMarkUnknownSelected}>
              Mark unknown
            </Button>
          </Stack>
        </Stack>

        {productivity ? <SessionProductivityStrip metrics={productivity} /> : null}

        <Box>
          <Stack direction="row" sx={{ justifyContent: "space-between", mb: 0.5 }}>
            <Typography variant="caption" color="text.secondary">
              Review progress
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {totalCount - pendingCount}/{totalCount}
            </Typography>
          </Stack>
          <LinearProgress
            variant="determinate"
            value={progress}
            aria-label="Recognition review progress"
            sx={{ height: 8, borderRadius: 1 }}
          />
        </Box>

        <Typography variant="caption" color="text.secondary">
          Shortcuts: Space next · Enter approve · Del reject · Tab image · F fullscreen · H heat map · M mini map · ? help
        </Typography>
      </Stack>
    </Paper>
  );
}

export default StickyReviewToolbar;
