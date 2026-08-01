import {
  Avatar,
  Box,
  Card,
  CardActionArea,
  Checkbox,
  Chip,
  Stack,
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

export type RecognitionCardProps = {
  recognition: AttendanceRecognitionReviewDto;
  selected: boolean;
  focused: boolean;
  related?: boolean;
  batchSelected?: boolean;
  batchSelectionDisabled?: boolean;
  onSelect: () => void;
  onToggleBatchSelect?: () => void;
};

export const RecognitionCard = memo(function RecognitionCard({
  recognition,
  selected,
  focused,
  related = false,
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
        borderColor: focused
          ? "primary.main"
          : related
            ? "secondary.main"
            : selected
              ? "primary.light"
              : "divider",
        borderWidth: focused || related ? 2 : 1,
        boxShadow: focused ? 3 : related || selected ? 1 : 0,
        bgcolor: focused ? "action.selected" : related ? "action.hover" : "background.paper",
        transition: (theme) =>
          theme.transitions.create(["border-color", "box-shadow", "background-color", "transform"], {
            duration: theme.transitions.duration.shorter,
          }),
        transform: focused ? "translateX(2px)" : "none",
        "@media (prefers-reduced-motion: reduce)": {
          transition: "none",
          transform: "none",
        },
      }}
    >
      <CardActionArea
        onClick={onSelect}
        aria-label={`Face ${recognition.faceNumber}, ${recognition.studentName ?? "unassigned"}, image ${recognition.imageSequence ?? 1}`}
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
              bgcolor: "background.paper",
              borderRadius: 1,
              p: 0.25,
            }}
          />
        )}
        <Stack direction="row" spacing={1.5} sx={{ alignItems: "flex-start" }}>
          <Tooltip
            title={
              faceUrl ? (
                <Box
                  component="img"
                  src={faceUrl}
                  alt=""
                  sx={{ width: 120, height: 120, objectFit: "cover", display: "block" }}
                />
              ) : (
                `Face #${recognition.faceNumber}`
              )
            }
            placement="right"
            enterDelay={350}
          >
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
          </Tooltip>

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
              {(recognition.imageSequence ?? 1) > 1
                ? ` · Img ${recognition.imageSequence}`
                : ""}
            </Typography>
            <Box sx={{ mt: 1 }}>
              <EnterpriseConfidenceBadge confidence={recognition.confidence} compact />
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
