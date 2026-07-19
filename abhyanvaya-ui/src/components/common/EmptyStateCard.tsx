import { Box, Card, CardContent, Stack, Typography } from "@mui/material";
import { alpha } from "@mui/material/styles";
import type { ReactNode } from "react";

export type EmptyStateCardProps = {
  icon: ReactNode;
  title: string;
  description?: string;
  /** Optional call-to-action slot (e.g. a "Retry" or "Create" button). Omit for a purely informational empty state. */
  action?: ReactNode;
};

/**
 * Professional empty-state card (AI20.UI.6, densified in AI20.UI.11) — replaces "blank table"
 * placeholders across the app (recent batches, failures, history, statistics, etc.). Generic and
 * content-agnostic: every caller supplies its own icon, copy, and optional action, so this single
 * component can back every empty list/table in every module. The icon sits inside a soft tinted
 * circle for a subtle "illustration" feel without pulling in any external asset — the tint is
 * derived from `theme.palette.primary.main` via `alpha()`, so it repaints correctly in dark mode.
 */
const EmptyStateCard = ({ icon, title, description, action }: EmptyStateCardProps) => (
  <Card variant="outlined" sx={{ backgroundColor: "action.hover" }}>
    <CardContent sx={{ "&:last-child": { pb: 2.5 }, py: 2.5 }}>
      <Stack spacing={1} sx={{ alignItems: "center", textAlign: "center" }}>
        <Box
          sx={{
            width: 56,
            height: 56,
            borderRadius: "50%",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            color: "primary.main",
            backgroundColor: (theme) => alpha(theme.palette.primary.main, 0.08),
          }}
        >
          {icon}
        </Box>
        <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
          {title}
        </Typography>
        {description && (
          <Typography variant="body2" color="text.secondary" sx={{ maxWidth: 420 }}>
            {description}
          </Typography>
        )}
        {action && <Box sx={{ pt: 0.5 }}>{action}</Box>}
      </Stack>
    </CardContent>
  </Card>
);

export default EmptyStateCard;
