import { Box, Card, CardContent, Grid, Stack, Typography } from "@mui/material";
import CloudDownloadOutlinedIcon from "@mui/icons-material/CloudDownloadOutlined";
import LinkOutlinedIcon from "@mui/icons-material/LinkOutlined";
import MemoryOutlinedIcon from "@mui/icons-material/MemoryOutlined";
import FaceRetouchingNaturalOutlinedIcon from "@mui/icons-material/FaceRetouchingNaturalOutlined";
import CloudOutlinedIcon from "@mui/icons-material/CloudOutlined";
import ReplayOutlinedIcon from "@mui/icons-material/ReplayOutlined";
import LinearScaleOutlinedIcon from "@mui/icons-material/LinearScaleOutlined";
import ImageOutlinedIcon from "@mui/icons-material/ImageOutlined";
import DataObjectOutlinedIcon from "@mui/icons-material/DataObjectOutlined";
import type { ReactNode } from "react";

type ConfigItem = {
  icon: ReactNode;
  label: string;
  value: string;
  /** Render the value monospace and let the row span the full width (for long URLs/keys). */
  mono?: boolean;
};

/**
 * Mock-only enrollment configuration snapshot (AI20.UI.9, redesigned in AI20.UI.13 into a
 * responsive icon+label+value grid). Read-only, no editing, no config loading — every value below
 * is a static placeholder until the real Photo Provider / Embedding Engine configuration surfaces
 * are implemented. Purely presentational (accepts no props today); once real configuration exists,
 * this becomes a container that fetches and maps into the same `ConfigItem[]` shape.
 */
const ENROLLMENT_CONFIG_ITEMS: ConfigItem[] = [
  { icon: <CloudDownloadOutlinedIcon fontSize="small" />, label: "Photo Provider", value: "ExamBranch" },
  { icon: <MemoryOutlinedIcon fontSize="small" />, label: "Embedding Engine", value: "InsightFace" },
  { icon: <FaceRetouchingNaturalOutlinedIcon fontSize="small" />, label: "Recognition Engine", value: "InsightFace" },
  { icon: <CloudOutlinedIcon fontSize="small" />, label: "Storage", value: "Cloudflare R2" },
  { icon: <ReplayOutlinedIcon fontSize="small" />, label: "Retry Policy", value: "3 Attempts" },
  { icon: <LinearScaleOutlinedIcon fontSize="small" />, label: "Download Threads", value: "4" },
  { icon: <ImageOutlinedIcon fontSize="small" />, label: "Image Format", value: "JPEG" },
  { icon: <DataObjectOutlinedIcon fontSize="small" />, label: "Embedding Size", value: "512 Dimensions" },
  {
    icon: <LinkOutlinedIcon fontSize="small" />,
    label: "Photo URL Template",
    value: "https://exambranch.com/PHOTOS/{collegeCode}/{batch}/{studentNumber}.jpg",
    mono: true,
  },
];

const ConfigRow = ({ item }: { item: ConfigItem }) => (
  <Stack
    direction="row"
    spacing={1.25}
    sx={{
      alignItems: "flex-start",
      py: 0.75,
      px: 1,
      borderRadius: 1,
      backgroundColor: "action.hover",
      height: "100%",
    }}
  >
    <Box sx={{ color: "text.secondary", display: "flex", pt: 0.25 }}>{item.icon}</Box>
    <Box sx={{ minWidth: 0, flexGrow: 1 }}>
      <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
        {item.label}
      </Typography>
      <Typography
        variant="body2"
        sx={{
          fontWeight: 600,
          wordBreak: "break-word",
          ...(item.mono && { fontFamily: "monospace", fontSize: "0.8125rem", fontWeight: 500 }),
        }}
      >
        {item.value}
      </Typography>
    </Box>
  </Stack>
);

const EnrollmentConfigurationCard = () => (
  <Card variant="outlined">
    <CardContent>
      <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1.5 }}>
        Enrollment Configuration
      </Typography>
      <Grid container spacing={1.5}>
        {ENROLLMENT_CONFIG_ITEMS.map((item) => (
          <Grid key={item.label} size={{ xs: 12, sm: item.mono ? 12 : 6, lg: item.mono ? 12 : 4 }}>
            <ConfigRow item={item} />
          </Grid>
        ))}
      </Grid>
    </CardContent>
  </Card>
);

export default EnrollmentConfigurationCard;
