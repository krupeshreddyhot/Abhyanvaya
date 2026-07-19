import { Card, CardContent, Stack, Typography } from "@mui/material";
import type { ReactNode } from "react";

export type StatCardProps = {
  label: string;
  value: ReactNode;
  icon?: ReactNode;
  /** Optional semantic tint for the value (e.g. error.main for a "Failed" count). Defaults to theme text color. */
  valueColor?: string;
};

/**
 * Generic labelled-metric card (AI20.UI.2's Enrollment Summary tiles). Purely presentational — the
 * caller decides the label, value (including placeholder text like <c>"--"</c> before any data
 * exists), and icon, so this same card can back any future dashboard's summary row.
 */
const StatCard = ({ label, value, icon, valueColor }: StatCardProps) => (
  <Card variant="outlined">
    {/* AI20.UI.15: compact padding so five metric tiles + the cards above stay within a 1080p viewport. */}
    <CardContent sx={{ "&:last-child": { pb: 1.5 }, p: 1.5 }}>
      <Stack spacing={0.5}>
        <Stack direction="row" spacing={0.75} sx={{ alignItems: "center", color: "text.secondary" }}>
          {icon}
          <Typography variant="body2" noWrap>
            {label}
          </Typography>
        </Stack>
        <Typography variant="h5" sx={{ fontWeight: 700, color: valueColor }}>
          {value}
        </Typography>
      </Stack>
    </CardContent>
  </Card>
);

export default StatCard;
