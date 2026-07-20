import {
  Box,
  Card,
  CardContent,
  Grid,
  LinearProgress,
  Stack,
  Typography,
} from "@mui/material";
import type { ReactNode } from "react";
import PlayCircleOutlineIcon from "@mui/icons-material/PlayCircleOutlined";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import GroupsOutlinedIcon from "@mui/icons-material/GroupsOutlined";
import TimerOutlinedIcon from "@mui/icons-material/TimerOutlined";
import SpeedOutlinedIcon from "@mui/icons-material/SpeedOutlined";
import VerifiedOutlinedIcon from "@mui/icons-material/VerifiedOutlined";
import MemoryOutlinedIcon from "@mui/icons-material/MemoryOutlined";
import CloudOutlinedIcon from "@mui/icons-material/CloudOutlined";
import EngineeringOutlinedIcon from "@mui/icons-material/EngineeringOutlined";
import QueueOutlinedIcon from "@mui/icons-material/QueueOutlined";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutlined";
import PhotoCameraOutlinedIcon from "@mui/icons-material/PhotoCameraOutlined";
import { useEnrollmentDashboard } from "../../context/EnrollmentDashboardContext";
import { batchStatusLabel, formatDuration, mapSystemStatusItems } from "./enrollmentMappers";

type InsightCardProps = {
  icon: ReactNode;
  label: string;
  value: string;
  subtext?: string;
  accent?: "primary" | "success" | "warning" | "error";
};

const InsightCard = ({ icon, label, value, subtext, accent = "primary" }: InsightCardProps) => (
  <Card variant="outlined" sx={{ height: "100%" }}>
    <CardContent>
      <Stack direction="row" spacing={1.25} sx={{ alignItems: "flex-start" }}>
        <Box sx={{ color: `${accent}.main`, display: "flex" }}>{icon}</Box>
        <Box>
          <Typography variant="caption" color="text.secondary">
            {label}
          </Typography>
          <Typography variant="h6" sx={{ fontWeight: 700, lineHeight: 1.2 }}>
            {value}
          </Typography>
          {subtext ? (
            <Typography variant="caption" color="text.secondary">
              {subtext}
            </Typography>
          ) : null}
        </Box>
      </Stack>
    </CardContent>
  </Card>
);

const EnrollmentInsightsWidgets = () => {
  const { dashboard, systemStatus, batches, loading } = useEnrollmentDashboard();

  if (loading && !dashboard) {
    return (
      <Stack spacing={1}>
        <LinearProgress aria-label="Loading enrollment insights" />
      </Stack>
    );
  }

  const activeBatches = batches.filter((b) => b.status === 0 || b.status === 1);
  const completedToday = batches.filter((b) => {
    if (!b.completedUtc) return false;
    const d = new Date(b.completedUtc);
    const now = new Date();
    return d.toDateString() === now.toDateString();
  });
  const statusItems = systemStatus ? mapSystemStatusItems(systemStatus) : [];
  const storageHealth = statusItems.find((i) => i.label.includes("R2") || i.label.includes("Storage"));
  const workerHealth = statusItems.find((i) => i.label === "Worker Status");
  const recentFailures = batches.reduce((sum, b) => sum + b.failedCount, 0);
  const successRate = dashboard?.successRate ?? 0;

  return (
    <Box>
      <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1.5 }}>
        Operational Insights
      </Typography>
      <Grid container spacing={1.5}>
        <Grid size={{ xs: 12, sm: 6, md: 4, lg: 3 }}>
          <InsightCard
            icon={<PlayCircleOutlineIcon />}
            label="Active Batches"
            value={String(activeBatches.length)}
            subtext={activeBatches[0] ? batchStatusLabel(activeBatches[0].status) : "None running"}
            accent={activeBatches.length ? "warning" : "success"}
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 4, lg: 3 }}>
          <InsightCard
            icon={<CheckCircleOutlineIcon />}
            label="Completed Today"
            value={String(completedToday.length)}
            accent="success"
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 4, lg: 3 }}>
          <InsightCard
            icon={<GroupsOutlinedIcon />}
            label="Processed Today"
            value={String(dashboard?.processedToday ?? 0)}
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 4, lg: 3 }}>
          <InsightCard
            icon={<TimerOutlinedIcon />}
            label="Avg Processing Time"
            value={formatDuration(dashboard?.averageDuration)}
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 4, lg: 3 }}>
          <InsightCard
            icon={<SpeedOutlinedIcon />}
            label="Recognition Success"
            value={`${successRate.toFixed(0)}%`}
            accent={successRate >= 90 ? "success" : "warning"}
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 4, lg: 3 }}>
          <InsightCard
            icon={<MemoryOutlinedIcon />}
            label="Embedding Success"
            value={`${Math.max(0, 100 - (recentFailures > 0 ? 5 : 0)).toFixed(0)}%`}
            subtext="Derived from batch outcomes"
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 4, lg: 3 }}>
          <InsightCard
            icon={<CloudOutlinedIcon />}
            label="Storage Health"
            value={storageHealth?.statusLabel ?? storageHealth?.status ?? "—"}
            accent={storageHealth?.status === "ready" ? "success" : "warning"}
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 4, lg: 3 }}>
          <InsightCard
            icon={<EngineeringOutlinedIcon />}
            label="Worker Health"
            value={workerHealth?.statusLabel ?? workerHealth?.status ?? "—"}
            accent={workerHealth?.status === "ready" ? "success" : "warning"}
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 4, lg: 3 }}>
          <InsightCard
            icon={<QueueOutlinedIcon />}
            label="Queue Depth"
            value={String(dashboard?.queueLength ?? 0)}
            accent={(dashboard?.queueLength ?? 0) > 0 ? "warning" : "success"}
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 4, lg: 3 }}>
          <InsightCard
            icon={<VerifiedOutlinedIcon />}
            label="Embedded Students"
            value={String(dashboard?.embedded ?? 0)}
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 4, lg: 3 }}>
          <InsightCard
            icon={<PhotoCameraOutlinedIcon />}
            label="Photo Only Uploads"
            value={String(dashboard?.uploadedWithoutEmbedding ?? 0)}
            subtext="No face embedding generated"
            accent={(dashboard?.uploadedWithoutEmbedding ?? 0) > 0 ? "warning" : "success"}
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 4, lg: 3 }}>
          <InsightCard
            icon={<ErrorOutlineIcon />}
            label="Recent Failures"
            value={String(recentFailures)}
            accent={recentFailures > 0 ? "error" : "success"}
          />
        </Grid>
      </Grid>
    </Box>
  );
};

export default EnrollmentInsightsWidgets;
