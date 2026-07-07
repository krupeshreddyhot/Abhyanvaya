import { Box, Paper, Stack, Typography } from "@mui/material";
import type { ReactElement } from "react";

export type WorkflowPlaceholderSectionProps = {
  icon: ReactElement;
  title: string;
  description: string;
  minHeight?: number;
  /** When true, section is part of the active guided workflow (no "Coming soon"). */
  active?: boolean;
};

export const WorkflowPlaceholderSection = ({
  icon,
  title,
  description,
  minHeight = 96,
  active = false,
}: WorkflowPlaceholderSectionProps) => (
  <Paper
    variant="outlined"
    sx={{
      p: 1.75,
      minHeight,
      borderStyle: active ? "solid" : "dashed",
      borderColor: active ? "primary.light" : "divider",
      bgcolor: active ? "background.paper" : "action.hover",
      display: "flex",
      alignItems: "center",
    }}
    aria-label={title}
  >
    <Stack direction="row" spacing={1.5} sx={{ alignItems: "center", width: "100%" }}>
      <Box
        sx={{
          color: active ? "primary.main" : "text.secondary",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          width: 40,
          height: 40,
          borderRadius: 1,
          bgcolor: "background.paper",
          border: 1,
          borderColor: active ? "primary.light" : "divider",
          flexShrink: 0,
        }}
      >
        {icon}
      </Box>
      <Box sx={{ minWidth: 0, flex: 1 }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700, lineHeight: 1.3 }}>
          {title}
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{ lineHeight: 1.4 }}>
          {description}
        </Typography>
        {!active && (
          <Typography variant="caption" color="text.disabled" sx={{ display: "block", mt: 0.5 }}>
            Coming soon
          </Typography>
        )}
      </Box>
    </Stack>
  </Paper>
);

export default WorkflowPlaceholderSection;
