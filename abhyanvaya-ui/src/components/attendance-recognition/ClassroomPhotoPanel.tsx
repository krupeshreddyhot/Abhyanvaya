import { Box, Paper, Typography } from "@mui/material";
import { useMemo } from "react";
import type { AttendanceRecognitionReviewDto } from "../../services/attendanceRecognitionService";
import { mediaAssetUrl } from "../../utils/mediaAssetUrl";

type ClassroomPhotoPanelProps = {
  imageUrl: string | null;
  imageWidth: number | null;
  imageHeight: number | null;
  recognitions: AttendanceRecognitionReviewDto[];
  highlightedRecognitionId: string | null;
  onHighlightRecognition: (recognitionId: string | null) => void;
};

export function ClassroomPhotoPanel({
  imageUrl,
  imageWidth,
  imageHeight,
  recognitions,
  highlightedRecognitionId,
  onHighlightRecognition,
}: ClassroomPhotoPanelProps) {
  const resolvedUrl = useMemo(() => mediaAssetUrl(imageUrl), [imageUrl]);

  const aspectRatio =
    imageWidth && imageHeight && imageWidth > 0 && imageHeight > 0
      ? `${imageWidth} / ${imageHeight}`
      : "4 / 3";

  return (
    <Paper
      variant="outlined"
      sx={{
        p: 2,
        height: "100%",
        display: "flex",
        flexDirection: "column",
        gap: 1.5,
      }}
    >
      <Typography variant="h6">Classroom photo</Typography>

      <Box
        sx={{
          position: "relative",
          width: "100%",
          aspectRatio,
          bgcolor: "grey.100",
          borderRadius: 1,
          overflow: "hidden",
          border: "1px solid",
          borderColor: "divider",
        }}
      >
        {resolvedUrl ? (
          <>
            <Box
              component="img"
              src={resolvedUrl}
              alt="Uploaded classroom attendance photo"
              sx={{
                width: "100%",
                height: "100%",
                objectFit: "contain",
                display: "block",
                bgcolor: "common.black",
              }}
            />
            {recognitions.map((recognition) => {
              const refWidth = imageWidth && imageWidth > 0 ? imageWidth : 1;
              const refHeight = imageHeight && imageHeight > 0 ? imageHeight : 1;
              const isHighlighted = highlightedRecognitionId === recognition.recognitionId;

              return (
                <Box
                  key={recognition.recognitionId}
                  role="button"
                  tabIndex={0}
                  aria-label={`Face ${recognition.faceNumber}`}
                  onClick={() => onHighlightRecognition(recognition.recognitionId)}
                  onKeyDown={(event) => {
                    if (event.key === "Enter" || event.key === " ") {
                      event.preventDefault();
                      onHighlightRecognition(recognition.recognitionId);
                    }
                  }}
                  sx={{
                    position: "absolute",
                    left: `${(recognition.boundingBoxX / refWidth) * 100}%`,
                    top: `${(recognition.boundingBoxY / refHeight) * 100}%`,
                    width: `${(recognition.boundingBoxWidth / refWidth) * 100}%`,
                    height: `${(recognition.boundingBoxHeight / refHeight) * 100}%`,
                    border: "2px solid",
                    borderColor: isHighlighted ? "primary.main" : "warning.main",
                    bgcolor: isHighlighted ? "rgba(25, 118, 210, 0.18)" : "rgba(255, 152, 0, 0.12)",
                    cursor: "pointer",
                    boxSizing: "border-box",
                  }}
                >
                  <Typography
                    variant="caption"
                    sx={{
                      position: "absolute",
                      top: 2,
                      left: 2,
                      px: 0.5,
                      bgcolor: "rgba(0,0,0,0.65)",
                      color: "common.white",
                      borderRadius: 0.5,
                      fontSize: "0.65rem",
                    }}
                  >
                    #{recognition.faceNumber}
                  </Typography>
                </Box>
              );
            })}
          </>
        ) : (
          <Box
            sx={{
              width: "100%",
              height: "100%",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              p: 2,
            }}
          >
            <Typography variant="body2" color="text.secondary" align="center">
              No classroom photo is available for this session yet.
            </Typography>
          </Box>
        )}
      </Box>
    </Paper>
  );
}
