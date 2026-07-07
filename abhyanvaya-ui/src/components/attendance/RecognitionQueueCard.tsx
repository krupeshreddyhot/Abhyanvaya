import QueueOutlinedIcon from "@mui/icons-material/QueueOutlined";
import { Box, Card, CardContent, Stack, Typography } from "@mui/material";
import { RecognitionQueueChip, getRecognitionQueueVisual } from "../../utils/recognitionQueueDisplay";

export type RecognitionQueueCardProps = {
  queueStatus: number;
};

export const RecognitionQueueCard = ({ queueStatus }: RecognitionQueueCardProps) => {
  const visual = getRecognitionQueueVisual(queueStatus);

  return (
    <Card variant="outlined" sx={{ height: "100%", display: "flex", flexDirection: "column" }}>
      <CardContent sx={{ flex: 1, py: 1.25, px: 1.5, "&:last-child": { pb: 1.25 } }}>
        <Stack spacing={0.75} sx={{ height: "100%" }}>
          <Box sx={{ color: "primary.main", display: "flex", "& svg": { fontSize: 20 } }} aria-hidden>
            <QueueOutlinedIcon />
          </Box>
          <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600, lineHeight: 1.2 }}>
            Recognition Queue
          </Typography>
          <RecognitionQueueChip queueStatus={queueStatus} />
          <Typography variant="caption" color="text.secondary" sx={{ lineHeight: 1.35 }}>
            {visual.description}
          </Typography>
        </Stack>
      </CardContent>
    </Card>
  );
};

export default RecognitionQueueCard;
