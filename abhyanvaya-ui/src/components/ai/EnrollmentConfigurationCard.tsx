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
import type { EnrollmentConfigurationDto } from "../../types/enrollment";

type ConfigItem = {
  icon: ReactNode;
  label: string;
  value: string;
  mono?: boolean;
};

const buildItems = (config: EnrollmentConfigurationDto): ConfigItem[] => [
  { icon: <CloudDownloadOutlinedIcon fontSize="small" />, label: "Photo Provider", value: config.photoProvider },
  { icon: <MemoryOutlinedIcon fontSize="small" />, label: "Embedding Engine", value: config.embeddingEngine },
  { icon: <FaceRetouchingNaturalOutlinedIcon fontSize="small" />, label: "Recognition Engine", value: config.recognitionEngine },
  { icon: <CloudOutlinedIcon fontSize="small" />, label: "Storage", value: config.storageProvider },
  { icon: <ReplayOutlinedIcon fontSize="small" />, label: "Retry Policy", value: config.retryPolicy },
  { icon: <LinearScaleOutlinedIcon fontSize="small" />, label: "Download Threads", value: String(config.downloadThreads) },
  { icon: <ImageOutlinedIcon fontSize="small" />, label: "Image Format", value: config.imageFormat },
  { icon: <DataObjectOutlinedIcon fontSize="small" />, label: "Embedding Size", value: `${config.embeddingDimensions} Dimensions` },
  {
    icon: <LinkOutlinedIcon fontSize="small" />,
    label: "Photo URL Template",
    value: config.photoUrlTemplate,
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

type Props = {
  configuration: EnrollmentConfigurationDto;
};

const EnrollmentConfigurationCard = ({ configuration }: Props) => {
  const items = buildItems(configuration);

  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1.5 }}>
          Enrollment Configuration
        </Typography>
        <Grid container spacing={1.5}>
          {items.map((item) => (
            <Grid key={item.label} size={{ xs: 12, sm: item.mono ? 12 : 6, lg: item.mono ? 12 : 4 }}>
              <ConfigRow item={item} />
            </Grid>
          ))}
        </Grid>
      </CardContent>
    </Card>
  );
};

export default EnrollmentConfigurationCard;
