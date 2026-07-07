import {
  Box,
  Button,
  Card,
  CardActions,
  CardContent,
  Checkbox,
  Chip,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import type { AttendanceRecognitionDto } from "../../services/attendanceRecognitionService";
import { confidenceColor, formatConfidence } from "../../utils/confidenceColor";
import { mediaAssetUrl } from "../../utils/mediaAssetUrl";
import {
  isPendingReview,
  recognitionStatusColor,
  recognitionStatusLabel,
} from "../../utils/recognitionStatus";

type RecognitionFaceCardProps = {
  recognition: AttendanceRecognitionDto;
  faceIndex: number;
  notes: string;
  selected: boolean;
  focused: boolean;
  disabled: boolean;
  actionLoading: boolean;
  onSelect: () => void;
  onFocus: () => void;
  onNotesChange: (notes: string) => void;
  onApprove: () => void;
  onReject: () => void;
  onIgnore: () => void;
  onAssign: () => void;
};

export function RecognitionFaceCard({
  recognition,
  faceIndex,
  notes,
  selected,
  focused,
  disabled,
  actionLoading,
  onSelect,
  onFocus,
  onNotesChange,
  onApprove,
  onReject,
  onIgnore,
  onAssign,
}: RecognitionFaceCardProps) {
  const pending = isPendingReview(recognition.recognitionStatus, recognition.verifiedByTeacher);
  const thumbnailUrl = mediaAssetUrl(recognition.thumbnailUrl);
  const confidence = recognition.confidenceScore;

  return (
    <Card
      variant="outlined"
      tabIndex={0}
      onClick={onFocus}
      onFocus={onFocus}
      sx={{
        height: "100%",
        display: "flex",
        flexDirection: "column",
        outline: "none",
        borderColor: focused ? "primary.main" : selected ? "primary.light" : "divider",
        borderWidth: focused ? 2 : 1,
        boxShadow: focused ? 4 : selected ? 2 : 0,
        transition: "border-color 0.15s, box-shadow 0.15s",
        "&:hover": {
          borderColor: focused ? "primary.main" : "action.selected",
        },
      }}
    >
      <Box sx={{ position: "relative" }}>
        {thumbnailUrl ? (
          <Box
            component="img"
            src={thumbnailUrl}
            alt={`Face ${faceIndex + 1}`}
            sx={{
              width: "100%",
              height: { xs: 160, sm: 180 },
              objectFit: "cover",
              display: "block",
              bgcolor: "grey.200",
            }}
          />
        ) : (
          <Box
            sx={{
              width: "100%",
              height: { xs: 160, sm: 180 },
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              bgcolor: "grey.200",
            }}
          >
            <Typography variant="h4" color="text.secondary">
              #{faceIndex + 1}
            </Typography>
          </Box>
        )}

        <Checkbox
          checked={selected}
          disabled={disabled || !pending}
          onClick={(event) => event.stopPropagation()}
          onChange={(event) => {
            event.stopPropagation();
            onSelect();
          }}
          sx={{
            position: "absolute",
            top: 4,
            right: 4,
            bgcolor: "rgba(255,255,255,0.88)",
            borderRadius: 1,
            p: 0.25,
          }}
        />

        <Chip
          size="small"
          label={`#${faceIndex + 1}`}
          sx={{
            position: "absolute",
            top: 8,
            left: 8,
            bgcolor: "rgba(0,0,0,0.65)",
            color: "common.white",
            fontWeight: 600,
          }}
        />
      </Box>

      <CardContent sx={{ flexGrow: 1, pb: 1 }}>
        <Stack spacing={1}>
          <Box>
            <Typography variant="subtitle1" sx={{ fontWeight: 700, lineHeight: 1.3 }}>
              {recognition.studentName ?? "Unassigned"}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {recognition.studentNumber ?? "No student number"}
            </Typography>
          </Box>

          <Stack direction="row" spacing={0.75} sx={{ flexWrap: "wrap", gap: 0.75 }}>
            <Chip
              size="small"
              label={formatConfidence(confidence)}
              sx={{
                bgcolor: confidenceColor(confidence),
                color: "common.white",
                fontWeight: 600,
              }}
            />
            <Chip
              size="small"
              label={recognitionStatusLabel(recognition.recognitionStatus)}
              color={recognitionStatusColor(recognition.recognitionStatus)}
              variant={recognition.verifiedByTeacher ? "filled" : "outlined"}
            />
          </Stack>

          <TextField
            size="small"
            fullWidth
            multiline
            minRows={1}
            maxRows={2}
            placeholder="Review notes"
            value={notes}
            disabled={disabled}
            onClick={(event) => event.stopPropagation()}
            onChange={(event) => onNotesChange(event.target.value)}
          />
        </Stack>
      </CardContent>

      <CardActions
        sx={{
          px: 2,
          pb: 2,
          pt: 0,
          flexWrap: "wrap",
          gap: 0.75,
        }}
      >
        <Tooltip title="Approve (A)">
          <span>
            <Button
              size="small"
              variant="outlined"
              color="success"
              disabled={disabled || actionLoading}
              onClick={(event) => {
                event.stopPropagation();
                onApprove();
              }}
            >
              Approve
            </Button>
          </span>
        </Tooltip>
        <Tooltip title="Reject (R)">
          <span>
            <Button
              size="small"
              variant="outlined"
              color="error"
              disabled={disabled || actionLoading}
              onClick={(event) => {
                event.stopPropagation();
                onReject();
              }}
            >
              Reject
            </Button>
          </span>
        </Tooltip>
        <Button
          size="small"
          variant="outlined"
          disabled={disabled || actionLoading}
          onClick={(event) => {
            event.stopPropagation();
            onAssign();
          }}
        >
          Assign student
        </Button>
        <Tooltip title="Ignore (I)">
          <span>
            <Button
              size="small"
              variant="outlined"
              disabled={disabled || actionLoading}
              onClick={(event) => {
                event.stopPropagation();
                onIgnore();
              }}
            >
              Ignore
            </Button>
          </span>
        </Tooltip>
      </CardActions>
    </Card>
  );
}
