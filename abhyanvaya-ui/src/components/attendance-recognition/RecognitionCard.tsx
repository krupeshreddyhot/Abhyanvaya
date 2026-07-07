import { Avatar, Box, Card, CardActionArea, Checkbox, Chip, Stack, Typography } from "@mui/material";
import { memo } from "react";
import type { AttendanceRecognitionReviewDto } from "../../services/attendanceRecognitionService";
import { mediaAssetUrl } from "../../utils/mediaAssetUrl";
import {
  recognitionStatusColor,
  recognitionStatusLabel,
} from "../../utils/recognitionStatus";
import { ConfidenceBar } from "./ConfidenceBar";

export type RecognitionCardProps = {
  recognition: AttendanceRecognitionReviewDto;
  selected: boolean;
  focused: boolean;
  batchSelected?: boolean;
  batchSelectionDisabled?: boolean;
  onSelect: () => void;
  onToggleBatchSelect?: () => void;
};

export const RecognitionCard = memo(function RecognitionCard({
  recognition,
  selected,
  focused,
  batchSelected = false,
  batchSelectionDisabled = false,
  onSelect,
  onToggleBatchSelect,
}: RecognitionCardProps) {
  const faceUrl = mediaAssetUrl(recognition.faceThumbnailUrl);
  const studentUrl = mediaAssetUrl(recognition.studentPhotoUrl);

  return (
    <Card
      variant="outlined"
      sx={{
        borderColor: focused ? "primary.main" : selected ? "primary.light" : "divider",
        borderWidth: focused ? 2 : 1,
        boxShadow: focused ? 3 : selected ? 1 : 0,
        transition: (theme) =>
          theme.transitions.create(["border-color", "box-shadow"], {
            duration: theme.transitions.duration.shorter,
          }),
        "@media (prefers-reduced-motion: reduce)": {
          transition: "none",
        },
      }}
    >
      <CardActionArea
        onClick={onSelect}
        aria-label={`Face ${recognition.faceNumber}, ${recognition.studentName ?? "unassigned"}`}
        aria-pressed={focused}
        sx={{ p: 1.5, display: "block", textAlign: "left", position: "relative" }}
      >
        {onToggleBatchSelect && (
          <Checkbox
            checked={batchSelected}
            disabled={batchSelectionDisabled}
            onClick={(event) => event.stopPropagation()}
            onChange={(event) => {
              event.stopPropagation();
              onToggleBatchSelect();
            }}
            slotProps={{
              input: { "aria-label": `Select face ${recognition.faceNumber} for batch review` },
            }}
            sx={{
              position: "absolute",
              top: 4,
              right: 4,
              zIndex: 1,
              bgcolor: "rgba(255,255,255,0.9)",
              borderRadius: 1,
              p: 0.25,
            }}
          />
        )}
        <Stack direction="row" spacing={1.5} sx={{ alignItems: "flex-start" }}>
          <Box sx={{ position: "relative", flexShrink: 0 }}>
            <Avatar
              variant="rounded"
              src={faceUrl ?? undefined}
              alt=""
              sx={{ width: 56, height: 56, bgcolor: "grey.300" }}
            >
              #{recognition.faceNumber}
            </Avatar>
            <Chip
              size="small"
              label={`#${recognition.faceNumber}`}
              sx={{
                position: "absolute",
                bottom: -6,
                left: "50%",
                transform: "translateX(-50%)",
                height: 18,
                fontSize: "0.65rem",
              }}
            />
          </Box>

          <Avatar
            variant="rounded"
            src={studentUrl ?? undefined}
            alt=""
            sx={{ width: 44, height: 44, bgcolor: "grey.200", flexShrink: 0 }}
          >
            {recognition.studentName?.charAt(0) ?? "?"}
          </Avatar>

          <Box sx={{ flex: 1, minWidth: 0 }}>
            <Typography variant="subtitle2" noWrap sx={{ fontWeight: 700 }}>
              {recognition.studentName ?? "Unassigned"}
            </Typography>
            <Typography variant="caption" color="text.secondary" noWrap sx={{ display: "block" }}>
              {recognition.studentNumber ?? "No student number"}
            </Typography>
            <Box sx={{ mt: 1 }}>
              <ConfidenceBar score={recognition.confidence} compact />
            </Box>
            <Chip
              size="small"
              label={recognitionStatusLabel(recognition.status)}
              color={recognitionStatusColor(recognition.status)}
              sx={{ mt: 1 }}
            />
          </Box>
        </Stack>
      </CardActionArea>
    </Card>
  );
});
