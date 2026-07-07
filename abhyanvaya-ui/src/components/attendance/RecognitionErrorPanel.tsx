import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import RefreshOutlinedIcon from "@mui/icons-material/RefreshOutlined";
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Alert,
  Button,
  Card,
  CardContent,
  Stack,
  Typography,
} from "@mui/material";

export type RecognitionErrorPanelProps = {
  errorCode?: string;
  processingError?: string;
  onRetry?: () => void;
  retryDisabled?: boolean;
};

export const RecognitionErrorPanel = ({
  errorCode,
  processingError,
  onRetry,
  retryDisabled = false,
}: RecognitionErrorPanelProps) => {
  if (!errorCode && !processingError) {
    return null;
  }

  const headline =
    errorCode === "NoFacesFound"
      ? "No faces were detected in the classroom photo."
      : errorCode === "ImageTooBlurry"
        ? "The classroom photo appears too blurry for recognition."
        : errorCode === "Timeout"
          ? "Recognition timed out."
          : errorCode === "Cancelled"
            ? "This attendance session was cancelled."
            : "Recognition failed.";

  return (
    <Card variant="outlined">
      <CardContent>
        <Stack spacing={2}>
          <Alert severity="error" role="alert">
            <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
              {headline}
            </Typography>
            {processingError && (
              <Typography variant="body2" sx={{ mt: 0.5 }}>
                {processingError}
              </Typography>
            )}
            {errorCode && (
              <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 0.5 }}>
                Error code: {errorCode}
              </Typography>
            )}
          </Alert>

          {onRetry && errorCode !== "Cancelled" && (
            <Button
              variant="contained"
              startIcon={<RefreshOutlinedIcon />}
              onClick={() => void onRetry()}
              disabled={retryDisabled}
              aria-label="Retry recognition upload"
            >
              Retry
            </Button>
          )}

          {(errorCode || processingError) && (
            <Accordion disableGutters elevation={0} sx={{ border: 1, borderColor: "divider" }}>
              <AccordionSummary expandIcon={<ExpandMoreIcon />}>
                <Typography variant="body2">Technical details</Typography>
              </AccordionSummary>
              <AccordionDetails>
                <Typography variant="caption" component="pre" sx={{ whiteSpace: "pre-wrap", m: 0 }}>
                  {JSON.stringify({ errorCode, processingError }, null, 2)}
                </Typography>
              </AccordionDetails>
            </Accordion>
          )}
        </Stack>
      </CardContent>
    </Card>
  );
};

export default RecognitionErrorPanel;
