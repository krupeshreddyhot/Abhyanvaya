import {
  Alert,
  Box,
  Card,
  CardContent,
  Chip,
  Grid,
  Stack,
  Typography,
} from "@mui/material";
import type { ReactNode } from "react";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutlined";

export type SummaryCardItem = {
  icon: ReactNode;
  label: string;
  value: string;
  status?: "ready" | "warning" | "error" | "neutral";
  tooltip?: string;
};

type Props = {
  title?: string;
  items: SummaryCardItem[];
  warnings?: string[];
};

const statusColor = (status: SummaryCardItem["status"]) => {
  switch (status) {
    case "ready":
      return "success.main";
    case "warning":
      return "warning.main";
    case "error":
      return "error.main";
    default:
      return "text.secondary";
  }
};

const StatusIcon = ({ status }: { status: SummaryCardItem["status"] }) => {
  if (status === "ready") return <CheckCircleOutlineIcon fontSize="small" color="success" />;
  if (status === "warning") return <WarningAmberOutlinedIcon fontSize="small" color="warning" />;
  if (status === "error") return <ErrorOutlineIcon fontSize="small" color="error" />;
  return null;
};

const EnterpriseSummaryCard = ({ icon, label, value, status }: SummaryCardItem) => (
  <Card variant="outlined" sx={{ height: "100%" }}>
    <CardContent>
      <Stack direction="row" spacing={1} sx={{ alignItems: "flex-start" }}>
        <Box sx={{ color: statusColor(status), display: "flex", pt: 0.25 }}>{icon}</Box>
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Stack direction="row" spacing={0.5} sx={{ alignItems: "center", mb: 0.5 }}>
            <Typography variant="caption" color="text.secondary">
              {label}
            </Typography>
            <StatusIcon status={status} />
          </Stack>
          <Typography variant="body1" sx={{ fontWeight: 600, wordBreak: "break-word" }}>
            {value}
          </Typography>
        </Box>
      </Stack>
    </CardContent>
  </Card>
);

const EnterpriseSummaryGrid = ({ title = "Enrollment Summary", items, warnings = [] }: Props) => (
  <Stack spacing={2}>
    <Typography variant="h6" component="h3">
      {title}
    </Typography>
    {warnings.length > 0 ? (
      <Stack spacing={1}>
        {warnings.map((w) => (
          <Alert key={w} severity="warning" variant="outlined">
            {w}
          </Alert>
        ))}
      </Stack>
    ) : (
      <Chip icon={<CheckCircleOutlineIcon />} label="Readiness checks passed" color="success" variant="outlined" size="small" />
    )}
    <Grid container spacing={1.5}>
      {items.map((item) => (
        <Grid key={item.label} size={{ xs: 12, sm: 6, md: 4 }}>
          <EnterpriseSummaryCard {...item} />
        </Grid>
      ))}
    </Grid>
  </Stack>
);

export default EnterpriseSummaryGrid;
