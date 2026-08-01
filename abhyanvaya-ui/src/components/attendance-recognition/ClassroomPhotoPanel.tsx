import FitScreenIcon from "@mui/icons-material/FitScreen";
import HeightIcon from "@mui/icons-material/Height";
import RestartAltIcon from "@mui/icons-material/RestartAlt";
import WidthFullIcon from "@mui/icons-material/WidthFull";
import ZoomInIcon from "@mui/icons-material/ZoomIn";
import ZoomOutIcon from "@mui/icons-material/ZoomOut";
import {
  Box,
  Button,
  ButtonGroup,
  Chip,
  IconButton,
  Paper,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import { memo, useEffect, useMemo, useRef } from "react";
import { useEnterpriseImageViewer } from "../../hooks/useEnterpriseImageViewer";
import type { AttendanceRecognitionReviewDto } from "../../services/attendanceRecognitionService";
import type { AttendanceSessionImage } from "../../types/sessionImage";
import { resolveSwipe } from "../../theme";
import { getEnterpriseConfidence } from "../../utils/enterpriseConfidence";
import { getHeatMapTone, HEAT_MAP_COLORS } from "../../utils/confidenceHeatMap";
import { mediaAssetUrl } from "../../utils/mediaAssetUrl";
import { FilmstripNavigator } from "./FilmstripNavigator";
import { ViewerMiniMap } from "./ViewerMiniMap";

export type ClassroomPhotoPanelProps = {
  imageUrl: string | null;
  imageWidth: number | null;
  imageHeight: number | null;
  recognitions: AttendanceRecognitionReviewDto[];
  /** Faces to emphasize across the active image (same student). */
  relatedRecognitionIds?: Set<string>;
  highlightedRecognitionId: string | null;
  onHighlightRecognition: (recognitionId: string | null) => void;
  sessionImages?: AttendanceSessionImage[];
  activeImageSequence?: number;
  onActiveImageSequenceChange?: (sequence: number) => void;
  onReorderImages?: (orderedIds: string[]) => void;
  hideHighConfidence?: boolean;
  heatMapEnabled?: boolean;
  heatMapOpacity?: number;
  miniMapVisible?: boolean;
  filmstripHeight?: number;
};

function ClassroomPhotoPanelInner({
  imageUrl,
  imageWidth,
  imageHeight,
  recognitions,
  relatedRecognitionIds,
  highlightedRecognitionId,
  onHighlightRecognition,
  sessionImages = [],
  activeImageSequence = 1,
  onActiveImageSequenceChange,
  onReorderImages,
  hideHighConfidence = false,
  heatMapEnabled = false,
  heatMapOpacity = 0.35,
  miniMapVisible = true,
  filmstripHeight = 96,
}: ClassroomPhotoPanelProps) {
  const resolvedUrl = useMemo(() => mediaAssetUrl(imageUrl), [imageUrl]);
  const viewer = useEnterpriseImageViewer();
  const swipeOrigin = useRef<{ x: number; y: number } | null>(null);

  const aspectRatio =
    imageWidth && imageHeight && imageWidth > 0 && imageHeight > 0
      ? `${imageWidth} / ${imageHeight}`
      : "4 / 3";

  const visibleRecognitions = useMemo(() => {
    const forImage = recognitions.filter(
      (r) => (r.imageSequence ?? 1) === activeImageSequence,
    );
    if (!hideHighConfidence) {
      return forImage;
    }
    return forImage.filter((r) => {
      const band = getEnterpriseConfidence(r.confidence).band;
      return band !== "excellent" && band !== "high";
    });
  }, [recognitions, activeImageSequence, hideHighConfidence]);

  const sequencesWithFaces = useMemo(() => {
    const set = new Set(recognitions.map((r) => r.imageSequence ?? 1));
    return set;
  }, [recognitions]);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (
        event.target instanceof HTMLElement &&
        (event.target.tagName === "INPUT" ||
          event.target.tagName === "TEXTAREA" ||
          event.target.isContentEditable)
      ) {
        return;
      }

      if (event.key === "+" || event.key === "=") {
        event.preventDefault();
        viewer.zoomIn();
      } else if (event.key === "-" || event.key === "_") {
        event.preventDefault();
        viewer.zoomOut();
      } else if (event.key === "0") {
        event.preventDefault();
        viewer.resetView();
      } else if (event.key === "ArrowLeft") {
        event.preventDefault();
        viewer.panByKeys(40, 0);
      } else if (event.key === "ArrowRight") {
        event.preventDefault();
        viewer.panByKeys(-40, 0);
      } else if (event.key === "ArrowUp") {
        event.preventDefault();
        viewer.panByKeys(0, 40);
      } else if (event.key === "ArrowDown") {
        event.preventDefault();
        viewer.panByKeys(0, -40);
      }
    };

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [viewer]);

  return (
    <Paper
      variant="outlined"
      sx={{
        p: { xs: 1.25, md: 2 },
        height: "100%",
        display: "flex",
        flexDirection: "column",
        gap: 1.25,
        minHeight: 0,
        transition: (theme) => theme.transitions.create(["box-shadow"], { duration: theme.transitions.duration.shorter }),
      }}
      aria-label="Enterprise classroom image viewer"
    >
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center" }}>
        <Typography variant="h6" component="h3">
          Classroom photo
        </Typography>
        <Chip size="small" label={`${viewer.percent}%`} aria-live="polite" />
      </Stack>

      {sessionImages.length > 0 && (
        <FilmstripNavigator
          images={sessionImages}
          activeImageSequence={activeImageSequence}
          sequencesWithFaces={sequencesWithFaces}
          onSelectSequence={(sequence) => onActiveImageSequenceChange?.(sequence)}
          onReorder={onReorderImages}
          thumbnailHeight={filmstripHeight}
        />
      )}

      <Stack direction="row" spacing={0.5} sx={{ flexWrap: "wrap", gap: 0.5 }} role="toolbar" aria-label="Image zoom toolbar">
        <Tooltip title="Zoom in (+)">
          <IconButton size="small" onClick={viewer.zoomIn} aria-label="Zoom in">
            <ZoomInIcon fontSize="small" />
          </IconButton>
        </Tooltip>
        <Tooltip title="Zoom out (-)">
          <IconButton size="small" onClick={viewer.zoomOut} aria-label="Zoom out">
            <ZoomOutIcon fontSize="small" />
          </IconButton>
        </Tooltip>
        <Tooltip title="Reset (0)">
          <IconButton size="small" onClick={viewer.resetView} aria-label="Reset view">
            <RestartAltIcon fontSize="small" />
          </IconButton>
        </Tooltip>
        <ButtonGroup size="small" variant="outlined">
          <Button startIcon={<WidthFullIcon />} onClick={() => viewer.fit("width")} aria-label="Fit width">
            Width
          </Button>
          <Button startIcon={<HeightIcon />} onClick={() => viewer.fit("height")} aria-label="Fit height">
            Height
          </Button>
          <Button startIcon={<FitScreenIcon />} onClick={() => viewer.fit("screen")} aria-label="Fit screen">
            Screen
          </Button>
        </ButtonGroup>
        <ButtonGroup size="small" variant="outlined">
          <Button onClick={() => viewer.setZoomPercent(100)} aria-label="Zoom 100 percent">
            100%
          </Button>
          <Button onClick={() => viewer.setZoomPercent(200)} aria-label="Zoom 200 percent">
            200%
          </Button>
          <Button onClick={() => viewer.setZoomPercent(400)} aria-label="Zoom 400 percent">
            400%
          </Button>
        </ButtonGroup>
      </Stack>

      <Box
        data-pan-surface
        className="enterprise-media"
        onWheel={viewer.onWheel}
        onPointerDown={(event) => {
          // AI22.7B 5.4 — track swipe origin for image navigation (pen/finger).
          if (event.pointerType === "touch" || event.pointerType === "pen") {
            swipeOrigin.current = { x: event.clientX, y: event.clientY };
          }
          viewer.onPointerDown(event);
        }}
        onPointerMove={viewer.onPointerMove}
        onPointerUp={(event) => {
          viewer.onPointerUp(event);
          if (swipeOrigin.current && !viewer.panning && onActiveImageSequenceChange) {
            const direction = resolveSwipe(
              swipeOrigin.current.x,
              swipeOrigin.current.y,
              event.clientX,
              event.clientY,
            );
            const ordered = [...sessionImages].sort((a, b) => a.imageSequence - b.imageSequence);
            const index = ordered.findIndex((image) => image.imageSequence === activeImageSequence);
            if (direction === "left" && index >= 0 && index < ordered.length - 1) {
              onActiveImageSequenceChange(ordered[index + 1].imageSequence);
            } else if (direction === "right" && index > 0) {
              onActiveImageSequenceChange(ordered[index - 1].imageSequence);
            }
          }
          swipeOrigin.current = null;
        }}
        onPointerCancel={viewer.onPointerUp}
        onTouchStart={viewer.onTouchStart}
        onTouchMove={viewer.onTouchMove}
        onTouchEnd={viewer.onTouchEnd}
        sx={{
          position: "relative",
          width: "100%",
          aspectRatio,
          bgcolor: "grey.900",
          borderRadius: 1,
          overflow: "hidden",
          border: "1px solid",
          borderColor: "divider",
          cursor: viewer.panning ? "grabbing" : "grab",
          touchAction: "none",
          userSelect: "none",
        }}
        aria-label="Zoomable classroom photo canvas. Pinch to zoom, swipe to change image, Alt or middle-drag to pan."
      >
        {resolvedUrl ? (
          <Box
            sx={{
              position: "absolute",
              inset: 0,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              ...viewer.transformStyle,
            }}
          >
            <Box sx={{ position: "relative", width: "100%", height: "100%" }}>
              <Box
                component="img"
                src={resolvedUrl}
                alt={`Classroom attendance photo image ${activeImageSequence}`}
                data-enterprise-media="true"
                draggable={false}
                sx={{
                  width: "100%",
                  height: "100%",
                  objectFit: "contain",
                  display: "block",
                  pointerEvents: "none",
                  // Images must remain visually unchanged across themes (AI22.7B 5.2).
                  filter: "none",
                }}
              />
              {visibleRecognitions.map((recognition) => {
                const refWidth = imageWidth && imageWidth > 0 ? imageWidth : 1;
                const refHeight = imageHeight && imageHeight > 0 ? imageHeight : 1;
                const isHighlighted = highlightedRecognitionId === recognition.recognitionId;
                const isRelated = relatedRecognitionIds?.has(recognition.recognitionId) ?? false;
                const confidence = getEnterpriseConfidence(recognition.confidence);
                const borderColor = isHighlighted || isRelated ? confidence.bboxColor : `${confidence.bboxColor}99`;
                const heatTone = getHeatMapTone(recognition.confidence);
                const heatFill = HEAT_MAP_COLORS[heatTone];

                return (
                  <Box
                    key={recognition.recognitionId}
                    role="button"
                    tabIndex={0}
                    aria-label={`Face ${recognition.faceNumber}, confidence ${confidence.percentLabel}`}
                    aria-pressed={isHighlighted}
                    onClick={(event) => {
                      event.stopPropagation();
                      onHighlightRecognition(recognition.recognitionId);
                    }}
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
                      border: isHighlighted || isRelated ? "3px solid" : "2px solid",
                      borderColor,
                      bgcolor: heatMapEnabled
                        ? heatFill
                        : isHighlighted
                          ? `${confidence.bboxColor}33`
                          : isRelated
                            ? `${confidence.bboxColor}22`
                            : "transparent",
                      opacity: heatMapEnabled ? heatMapOpacity : 1,
                      cursor: "pointer",
                      boxSizing: "border-box",
                      // GPU-friendly fill only — no canvas redraw (Phase 5.5).
                      willChange: heatMapEnabled ? "opacity, background-color" : "auto",
                      transition: (theme) =>
                        theme.transitions.create(["border-color", "background-color", "box-shadow", "opacity"], {
                          duration: theme.transitions.duration.shortest,
                        }),
                      boxShadow: isHighlighted ? `0 0 0 2px ${confidence.bboxColor}` : "none",
                      outlineOffset: 2,
                      "&:focus-visible": {
                        outline: (theme) => `2px solid ${theme.palette.common.white}`,
                      },
                      "@media (prefers-reduced-motion: reduce)": { transition: "none" },
                    }}
                  >
                    <Typography
                      variant="caption"
                      sx={{
                        position: "absolute",
                        top: 2,
                        left: 2,
                        px: 0.5,
                        bgcolor: "rgba(0,0,0,0.7)",
                        color: "common.white",
                        borderRadius: 0.5,
                        fontSize: "0.65rem",
                        whiteSpace: "nowrap",
                        opacity: 1,
                      }}
                    >
                      #{recognition.faceNumber} · {confidence.percentLabel}
                    </Typography>
                  </Box>
                );
              })}
            </Box>
          </Box>
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
            <Typography variant="body2" color="grey.300" align="center">
              No classroom photo is available for this session yet.
            </Typography>
          </Box>
        )}

        <ViewerMiniMap
          imageUrl={imageUrl}
          scale={viewer.scale}
          offsetX={viewer.offsetX}
          offsetY={viewer.offsetY}
          onOffsetChange={viewer.setOffset}
          visible={miniMapVisible && Boolean(resolvedUrl)}
        />
      </Box>

      <Typography variant="caption" color="text.secondary">
        Scroll to zoom · Alt+drag or middle-drag to pan · Pinch on touch · + / − / 0 / arrow pan
      </Typography>
    </Paper>
  );
}

export const ClassroomPhotoPanel = memo(ClassroomPhotoPanelInner);
export default ClassroomPhotoPanel;
