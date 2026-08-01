import {
  Avatar,
  Box,
  Button,
  Chip,
  Divider,
  Paper,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import { memo } from "react";
import type { AttendanceRecognitionReviewDto } from "../../services/attendanceRecognitionService";
import { mediaAssetUrl } from "../../utils/mediaAssetUrl";
import {
  recognitionStatusColor,
  recognitionStatusLabel,
} from "../../utils/recognitionStatus";
import { EnterpriseConfidenceBadge } from "./EnterpriseConfidenceBadge";

type SelectedFaceDetailsPanelProps = {
  recognition: AttendanceRecognitionReviewDto | null;
  notes: string;
  disabled: boolean;
  actionLoading: boolean;
  onNotesChange: (notes: string) => void;
  onApprove: () => void;
  onReject: () => void;
  onIgnore: () => void;
  onAssign: () => void;
};

export const SelectedFaceDetailsPanel = memo(function SelectedFaceDetailsPanel({
  recognition,
  notes,
  disabled,
  actionLoading,
  onNotesChange,
  onApprove,
  onReject,
  onIgnore,
  onAssign,
}: SelectedFaceDetailsPanelProps) {
  if (!recognition) {
    return (
      <Paper variant="outlined" sx={{ p: 2, height: "100%" }}>
        <Typography variant="body2" color="text.secondary" align="center" sx={{ py: 6 }}>
          Select a face from the list to review details.
        </Typography>
      </Paper>
    );
  }

  const faceUrl = mediaAssetUrl(recognition.faceThumbnailUrl);
  const studentUrl = mediaAssetUrl(recognition.studentPhotoUrl);

  return (
    <Paper variant="outlined" sx={{ p: 2, height: "100%" }} aria-live="polite">
      <Typography variant="h6" gutterBottom>
        Selected face
      </Typography>

      <Stack spacing={2}>
        <Stack direction="row" spacing={1.5} sx={{ alignItems: "center" }}>
          <Avatar variant="rounded" src={faceUrl ?? undefined} sx={{ width: 72, height: 72 }}>
            #{recognition.faceNumber}
          </Avatar>
          <Avatar variant="rounded" src={studentUrl ?? undefined} sx={{ width: 56, height: 56 }}>
            {recognition.studentName?.charAt(0) ?? "?"}
          </Avatar>
          <Box>
            <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
              {recognition.studentName ?? "Unassigned"}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {recognition.studentNumber ?? "No student number"}
            </Typography>
            <Chip
              size="small"
              label={recognitionStatusLabel(recognition.status)}
              color={recognitionStatusColor(recognition.status)}
              sx={{ mt: 0.5 }}
            />
          </Box>
        </Stack>

        <EnterpriseConfidenceBadge confidence={recognition.confidence} />

        <Divider />

        <Box>
          <Typography variant="caption" color="text.secondary">
            Classroom image
          </Typography>
          <Typography variant="body2">Image {recognition.imageSequence ?? 1}</Typography>
        </Box>

        <Box>
          <Typography variant="caption" color="text.secondary">
            Bounding box
          </Typography>
          <Typography variant="body2">
            ({recognition.boundingBoxX}, {recognition.boundingBoxY}) ·{" "}
            {recognition.boundingBoxWidth}×{recognition.boundingBoxHeight}
          </Typography>
        </Box>

        {recognition.suggestedStudentName && (
          <Box>
            <Typography variant="caption" color="text.secondary">
              Suggested student
            </Typography>
            <Typography variant="body2">
              {recognition.suggestedStudentName} ({recognition.suggestedStudentNumber ?? "—"})
            </Typography>
          </Box>
        )}

        {recognition.manualOverrideStudentName && (
          <Box>
            <Typography variant="caption" color="text.secondary">
              Manual override
            </Typography>
            <Typography variant="body2">
              {recognition.manualOverrideStudentName} ({recognition.manualOverrideStudentNumber ?? "—"})
            </Typography>
          </Box>
        )}

        <TextField
          label="Review notes"
          size="small"
          fullWidth
          multiline
          minRows={2}
          value={notes}
          disabled={disabled}
          onChange={(event) => onNotesChange(event.target.value)}
        />

        <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", gap: 1 }}>
          <Tooltip title="Approve (A)">
            <span>
              <Button
                size="small"
                variant="contained"
                color="success"
                disabled={disabled || actionLoading}
                onClick={onApprove}
              >
                Approve
              </Button>
            </span>
          </Tooltip>
          <Tooltip title="Reject (R / Delete) — reason required">
            <span>
              <Button
                size="small"
                variant="outlined"
                color="error"
                disabled={disabled || actionLoading}
                onClick={onReject}
              >
                Reject
              </Button>
            </span>
          </Tooltip>
          <Tooltip title="Manual match (M)">
            <span>
              <Button
                size="small"
                variant="outlined"
                disabled={disabled || actionLoading}
                onClick={onAssign}
              >
                Manual match
              </Button>
            </span>
          </Tooltip>
          <Tooltip title="Mark unknown (I)">
            <span>
              <Button
                size="small"
                variant="outlined"
                disabled={disabled || actionLoading}
                onClick={onIgnore}
              >
                Mark unknown
              </Button>
            </span>
          </Tooltip>
        </Stack>
      </Stack>
    </Paper>
  );
});
