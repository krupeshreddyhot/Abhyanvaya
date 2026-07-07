import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import {
  Box,
  Card,
  CardContent,
  IconButton,
  Snackbar,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import { useCallback, useState, type ReactNode } from "react";
import type { AIStatus } from "../../types/aiWorkflow";
import { normalizeGuidForCopy, shortenGuid } from "../../utils/guidDisplay";
import { AIStatusChip } from "../common/AIStatusChip";

export type SessionDashboardDetailRow = {
  label: string;
  value: string;
};

export type SessionDashboardCardProps = {
  icon: ReactNode;
  title: string;
  status?: AIStatus;
  headline?: string;
  subline?: string;
  metaLine?: string;
  eyebrow?: string;
  sessionId?: string;
  detailRows?: SessionDashboardDetailRow[];
  compact?: boolean;
};

const COMPACT_CARD_SX = {
  "&:last-child": { pb: 1.25 },
  px: 1.5,
  py: 1.25,
} as const;

export const SessionDashboardCard = ({
  icon,
  title,
  status,
  headline,
  subline,
  metaLine,
  eyebrow,
  sessionId,
  detailRows,
  compact = true,
}: SessionDashboardCardProps) => {
  const [copyOpen, setCopyOpen] = useState(false);
  const stackSpacing = compact ? 0.75 : 1.5;

  const handleCopySessionId = useCallback(async () => {
    if (!sessionId) {
      return;
    }

    try {
      await navigator.clipboard.writeText(normalizeGuidForCopy(sessionId));
      setCopyOpen(true);
    } catch {
      /* clipboard unavailable */
    }
  }, [sessionId]);

  const renderBody = () => {
    if (status) {
      return (
        <Stack spacing={0.5}>
          <AIStatusChip status={status} size="small" />
          {subline && (
            <Typography variant="caption" color="text.secondary" sx={{ lineHeight: 1.35 }}>
              {subline}
            </Typography>
          )}
        </Stack>
      );
    }

    if (sessionId) {
      return (
        <Stack spacing={0.5}>
          {eyebrow && (
            <Typography
              variant="overline"
              color="text.secondary"
              sx={{ lineHeight: 1.1, fontSize: "0.65rem" }}
            >
              {eyebrow}
            </Typography>
          )}
          <Typography variant="caption" color="text.secondary" sx={{ lineHeight: 1.2 }}>
            Session ID
          </Typography>
          <Stack direction="row" spacing={0.25} sx={{ alignItems: "center" }}>
            <Tooltip title={sessionId} placement="top" arrow>
              <Typography
                variant="body2"
                component="span"
                sx={{ fontWeight: 700, fontFamily: "monospace", letterSpacing: 0.4 }}
              >
                {shortenGuid(sessionId)}…
              </Typography>
            </Tooltip>
            <Tooltip title="Copy session ID" placement="top" arrow>
              <IconButton
                size="small"
                onClick={() => void handleCopySessionId()}
                aria-label="Copy session ID"
                sx={{ p: 0.5 }}
              >
                <ContentCopyIcon sx={{ fontSize: 16 }} />
              </IconButton>
            </Tooltip>
          </Stack>
        </Stack>
      );
    }

    return (
      <Stack spacing={0.35}>
        {eyebrow && (
          <Typography
            variant="overline"
            color="text.secondary"
            sx={{ lineHeight: 1.1, fontSize: "0.65rem" }}
          >
            {eyebrow}
          </Typography>
        )}
        {headline && (
          <Typography variant="body2" sx={{ fontWeight: 700, lineHeight: 1.3 }}>
            {headline}
          </Typography>
        )}
        {subline && (
          <Typography variant="caption" color="text.secondary" sx={{ lineHeight: 1.35 }}>
            {subline}
          </Typography>
        )}
        {metaLine && (
          <Typography variant="caption" color="text.secondary" sx={{ lineHeight: 1.35 }}>
            {metaLine}
          </Typography>
        )}
        {detailRows?.map((row) => (
          <Stack key={row.label} spacing={0}>
            <Typography variant="caption" color="text.secondary" sx={{ lineHeight: 1.2 }}>
              {row.label}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600, lineHeight: 1.35 }}>
              {row.value}
            </Typography>
          </Stack>
        ))}
      </Stack>
    );
  };

  return (
    <>
      <Card
        variant="outlined"
        sx={{
          height: "100%",
          display: "flex",
          flexDirection: "column",
        }}
      >
        <CardContent sx={{ flex: 1, display: "flex", flexDirection: "column", ...(compact ? COMPACT_CARD_SX : {}) }}>
          <Stack spacing={stackSpacing} sx={{ height: "100%" }}>
            <Box sx={{ color: "primary.main", display: "flex", "& svg": { fontSize: 20 } }} aria-hidden>
              {icon}
            </Box>
            <Typography
              variant="caption"
              color="text.secondary"
              sx={{ fontWeight: 600, lineHeight: 1.2, letterSpacing: 0.3 }}
            >
              {title}
            </Typography>
            {renderBody()}
          </Stack>
        </CardContent>
      </Card>

      <Snackbar
        open={copyOpen}
        autoHideDuration={2500}
        onClose={() => setCopyOpen(false)}
        message="Session ID copied"
        anchorOrigin={{ vertical: "bottom", horizontal: "center" }}
      />
    </>
  );
};

export default SessionDashboardCard;
