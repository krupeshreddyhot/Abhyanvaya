import { Box, Card, CardContent, Chip, Stack, Typography, type ChipProps } from "@mui/material";
import AutorenewIcon from "@mui/icons-material/Autorenew";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import ErrorIcon from "@mui/icons-material/Error";
import HelpOutlineIcon from "@mui/icons-material/HelpOutlineOutlined";
import type { ReactNode } from "react";

/** Health level for one AI subsystem tile — drives the icon shape and chip color together (never color-only). */
export type AiSystemStatusLevel = "ready" | "starting" | "offline" | "unknown";

export type AiSystemStatusItem = {
  /** Subsystem name, e.g. "Photo Provider", "Recognition Engine". */
  label: string;
  /** Provider/engine display name, e.g. "ExamBranch", "InsightFace". Omit for subsystems with no vendor name (e.g. "Background Worker"). */
  detail?: string;
  status: AiSystemStatusLevel;
  /** Chip text. Defaults to a level-derived label ("Ready"/"Starting"/"Offline"/"Unknown") — override for subsystem-specific wording like "Running". */
  statusLabel?: string;
};

export type AiSystemStatusCardProps = {
  title?: string;
  items: AiSystemStatusItem[];
};

const STATUS_VISUALS: Record<AiSystemStatusLevel, { color: ChipProps["color"]; label: string; icon: ReactNode }> = {
  ready: { color: "success", label: "Ready", icon: <CheckCircleIcon fontSize="small" color="success" /> },
  starting: { color: "warning", label: "Starting", icon: <AutorenewIcon fontSize="small" color="warning" /> },
  offline: { color: "error", label: "Offline", icon: <ErrorIcon fontSize="small" color="error" /> },
  unknown: { color: "default", label: "Unknown", icon: <HelpOutlineIcon fontSize="small" color="disabled" /> },
};

/**
 * Reusable AI subsystem health dashboard (AI20.UI.5, redesigned in AI20.UI.8 into one compact card
 * per subsystem instead of rows in a single card — the "service health tile" pattern used by
 * enterprise consoles such as Azure Portal / AWS Console / Datadog). Purely presentational: the
 * caller supplies whichever subsystems/statuses are relevant via `items`, so the same component can
 * be reused by any future AI module page without changes.
 */
const AiSystemStatusCard = ({ title = "AI System Status", items }: AiSystemStatusCardProps) => (
  <Box>
    {title && (
      <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1.5 }}>
        {title}
      </Typography>
    )}
    <Box
      sx={{
        display: "grid",
        gridTemplateColumns: "repeat(auto-fit, minmax(190px, 1fr))",
        gap: 1.5,
      }}
    >
      {items.map((item) => {
        const visual = STATUS_VISUALS[item.status];
        return (
          <Card key={item.label} variant="outlined">
            <CardContent sx={{ "&:last-child": { pb: 1.5 }, p: 1.5 }}>
              <Stack spacing={0.75}>
                <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                  {visual.icon}
                  <Typography variant="body2" sx={{ fontWeight: 600 }} noWrap>
                    {item.label}
                  </Typography>
                </Stack>
                {item.detail && (
                  <Typography variant="body2" color="text.secondary" noWrap>
                    {item.detail}
                  </Typography>
                )}
                <Box>
                  <Chip size="small" label={item.statusLabel ?? visual.label} color={visual.color} variant="outlined" />
                </Box>
              </Stack>
            </CardContent>
          </Card>
        );
      })}
    </Box>
  </Box>
);

export default AiSystemStatusCard;
