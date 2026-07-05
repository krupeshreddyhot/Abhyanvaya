import RateReviewOutlinedIcon from "@mui/icons-material/RateReviewOutlined";
import { Button, Card, CardContent, Fade, Stack, Typography } from "@mui/material";
import { Link as RouterLink } from "react-router-dom";

export type RecognitionReviewSectionProps = {
  sessionId: string;
};

export const RecognitionReviewSection = ({ sessionId }: RecognitionReviewSectionProps) => (
  <Fade in timeout={500}>
    <Card variant="outlined" aria-label="Recognition review">
      <CardContent>
        <Stack spacing={2}>
          <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
            <RateReviewOutlinedIcon color="primary" aria-hidden />
            <Typography variant="subtitle1" component="h3" sx={{ fontWeight: 700 }}>
              Recognition Review
            </Typography>
          </Stack>
          <Typography variant="body2" color="text.secondary">
            Recognition is complete. Review detected students, confirm matches, and finalize attendance.
          </Typography>
          <Button
            component={RouterLink}
            to={`/attendance/sessions/${sessionId}/review`}
            variant="contained"
            aria-label="Open recognition review page"
          >
            Open Review
          </Button>
        </Stack>
      </CardContent>
    </Card>
  </Fade>
);

export default RecognitionReviewSection;
