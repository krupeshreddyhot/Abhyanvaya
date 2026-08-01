import DeleteIcon from "@mui/icons-material/Delete";
import CloudUploadIcon from "@mui/icons-material/CloudUpload";
import DragIndicatorIcon from "@mui/icons-material/DragIndicator";
import ReplayIcon from "@mui/icons-material/Replay";
import SortIcon from "@mui/icons-material/Sort";
import {
  Box,
  Button,
  Chip,
  FormControl,
  InputLabel,
  LinearProgress,
  MenuItem,
  Popover,
  Select,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import { useMemo, useRef, useState, type DragEvent } from "react";
import { CLASSROOM_PHOTO_ACCEPT } from "../../constants/classroomPhotoUploadHints";
import {
  formatFileSizeLabel,
  formatResolution,
  formatUploadedTimestamp,
} from "../../utils/fileDisplay";
import { mediaAssetUrl } from "../../utils/mediaAssetUrl";
import {
  estimateFacesFromResolution,
  formatCaptureTime,
  getImageQualityIndicator,
} from "../../utils/imageQuality";
import { getEnterpriseImageStatus } from "../../utils/recognitionReadiness";
import {
  loadImageLabels,
  saveImageLabel,
  SUGGESTED_IMAGE_LABELS,
} from "../../utils/sessionImageLabels";
import {
  MAX_CLASSROOM_IMAGES_PER_SESSION,
  SESSION_IMAGE_STATUS,
  type AttendanceSessionImage,
} from "../../types/sessionImage";
import { ClassroomImageDetailsDialog } from "./ClassroomImageDetailsDialog";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";

export type ClassroomPhotoCollectionPanelProps = {
  images: AttendanceSessionImage[];
  sessionId?: string;
  disabled?: boolean;
  busy?: boolean;
  uploadProgress?: number | null;
  canAddMore: boolean;
  detectedFaces?: number;
  onAddMore: () => void;
  onDelete: (imageId: string) => void | Promise<void>;
  onReplace: (imageId: string, file: File) => void | Promise<void>;
  onReorder: (orderedIds: string[]) => void | Promise<void>;
  onRetryRecognition?: () => void | Promise<void>;
  onRetryImageRecognition?: (imageId: string) => void | Promise<void>;
  onRetryFailedUpload?: () => void | Promise<void>;
  onDeleteAll?: () => void | Promise<void>;
  onReplaceAll?: () => void;
  showRetryUpload?: boolean;
  onNotify?: (message: string, severity?: "success" | "info" | "warning" | "error") => void;
};

type SortMode = "sequence" | "captureTime" | "quality" | "status";

export const ClassroomPhotoCollectionPanel = ({
  images,
  sessionId,
  disabled = false,
  busy = false,
  uploadProgress = null,
  canAddMore,
  detectedFaces = 0,
  onAddMore,
  onDelete,
  onReplace,
  onReorder,
  onRetryRecognition,
  onRetryImageRecognition,
  onRetryFailedUpload,
  onDeleteAll,
  onReplaceAll,
  showRetryUpload = false,
  onNotify,
}: ClassroomPhotoCollectionPanelProps) => {
  const replaceInputRef = useRef<HTMLInputElement>(null);
  const [replaceTargetId, setReplaceTargetId] = useState<string | null>(null);
  const [dragId, setDragId] = useState<string | null>(null);
  const [dropTargetId, setDropTargetId] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(images[0]?.id ?? null);
  const [sortMode, setSortMode] = useState<SortMode>("sequence");
  const [labels, setLabels] = useState<Record<string, string>>(() => loadImageLabels(sessionId));
  const [detailsImage, setDetailsImage] = useState<AttendanceSessionImage | null>(null);
  const [hoverAnchor, setHoverAnchor] = useState<{
    el: HTMLElement;
    image: AttendanceSessionImage;
  } | null>(null);
  const isDisabled = disabled || busy;

  const displayImages = useMemo(() => {
    const copy = [...images];
    switch (sortMode) {
      case "captureTime":
        return copy.sort(
          (a, b) =>
            new Date(b.captureTimestamp ?? b.uploadedUtc ?? 0).getTime() -
            new Date(a.captureTimestamp ?? a.uploadedUtc ?? 0).getTime(),
        );
      case "quality":
        return copy.sort(
          (a, b) =>
            getImageQualityIndicator(b.blurScore).rank -
            getImageQualityIndicator(a.blurScore).rank,
        );
      case "status":
        return copy.sort((a, b) => Number(a.status) - Number(b.status));
      default:
        return copy.sort((a, b) => a.imageSequence - b.imageSequence);
    }
  }, [images, sortMode]);

  const openReplace = (imageId: string) => {
    if (isDisabled) {
      return;
    }
    setReplaceTargetId(imageId);
    replaceInputRef.current?.click();
  };

  const onReplaceChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    const targetId = replaceTargetId;
    setReplaceTargetId(null);
    if (file && targetId) {
      void onReplace(targetId, file);
      onNotify?.("Photo replaced", "info");
    }
  };

  const onDragStart = (imageId: string) => (event: DragEvent) => {
    if (sortMode !== "sequence") {
      event.preventDefault();
      onNotify?.("Switch sort to Sequence to reorder", "info");
      return;
    }
    setDragId(imageId);
    event.dataTransfer.effectAllowed = "move";
    event.dataTransfer.setData("text/plain", imageId);
  };

  const onDropOn = (targetId: string) => (event: DragEvent) => {
    event.preventDefault();
    const sourceId = dragId ?? event.dataTransfer.getData("text/plain");
    setDragId(null);
    setDropTargetId(null);
    if (!sourceId || sourceId === targetId || isDisabled || sortMode !== "sequence") {
      return;
    }

    const ids = images
      .slice()
      .sort((a, b) => a.imageSequence - b.imageSequence)
      .map((image) => image.id);
    const from = ids.indexOf(sourceId);
    const to = ids.indexOf(targetId);
    if (from < 0 || to < 0) {
      return;
    }

    const next = [...ids];
    next.splice(from, 1);
    next.splice(to, 0, sourceId);
    void onReorder(next);
    onNotify?.("Photos reordered", "info");
  };

  const updateLabel = (imageId: string, value: string) => {
    setLabels((current) => {
      const next = { ...current, [imageId]: value };
      saveImageLabel(sessionId, imageId, value);
      return next;
    });
  };

  const hasFailed = images.some((image) => image.status === SESSION_IMAGE_STATUS.Failed);

  return (
    <Stack spacing={1.5} aria-label="Classroom photo collection">
      <Stack
        direction={{ xs: "column", sm: "row" }}
        sx={{ justifyContent: "space-between", alignItems: { sm: "center" }, gap: 1 }}
      >
        <Typography variant="subtitle1" component="h3" sx={{ fontWeight: 600 }}>
          Classroom Photos ({images.length}/{MAX_CLASSROOM_IMAGES_PER_SESSION})
        </Typography>
        <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap" }}>
          <FormControl size="small" sx={{ minWidth: 150 }}>
            <InputLabel id="photo-sort-label">Sort</InputLabel>
            <Select
              labelId="photo-sort-label"
              label="Sort"
              value={sortMode}
              onChange={(event) => setSortMode(event.target.value as SortMode)}
              startAdornment={<SortIcon fontSize="small" sx={{ mr: 0.5, color: "text.secondary" }} />}
            >
              <MenuItem value="sequence">Sequence</MenuItem>
              <MenuItem value="captureTime">Capture Time</MenuItem>
              <MenuItem value="quality">Quality</MenuItem>
              <MenuItem value="status">Status</MenuItem>
            </Select>
          </FormControl>
          {canAddMore && (
            <Button size="small" variant="outlined" onClick={onAddMore} disabled={isDisabled}>
              Add photos
            </Button>
          )}
          {onReplaceAll && (
            <Button size="small" variant="outlined" onClick={onReplaceAll} disabled={isDisabled}>
              Replace All
            </Button>
          )}
          {onDeleteAll && (
            <Button
              size="small"
              variant="outlined"
              color="error"
              onClick={() => void onDeleteAll()}
              disabled={isDisabled || images.length === 0}
            >
              Delete All
            </Button>
          )}
          {(hasFailed || showRetryUpload) && onRetryFailedUpload && (
            <Button
              size="small"
              variant="outlined"
              color="warning"
              startIcon={<ReplayIcon />}
              onClick={() => void onRetryFailedUpload()}
              disabled={isDisabled}
            >
              Retry upload
            </Button>
          )}
          {onRetryRecognition && (
            <Button
              size="small"
              variant="outlined"
              startIcon={<ReplayIcon />}
              onClick={() => void onRetryRecognition()}
              disabled={isDisabled || images.length === 0}
            >
              Retry Recognition
            </Button>
          )}
        </Stack>
      </Stack>

      {uploadProgress != null && (
        <Box aria-label="Upload progress">
          <LinearProgress variant="determinate" value={uploadProgress} />
          <Typography variant="caption" color="text.secondary">
            {uploadProgress}% uploaded
          </Typography>
        </Box>
      )}

      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: {
            xs: "1fr",
            sm: "repeat(2, minmax(0, 1fr))",
            md: "repeat(3, minmax(0, 1fr))",
            lg: "repeat(4, minmax(0, 1fr))",
          },
          gap: 1.5,
        }}
        role="list"
        aria-label="Classroom photo gallery"
      >
        {displayImages.map((image) => {
          const status = getEnterpriseImageStatus(image.status);
          const quality = getImageQualityIndicator(image.blurScore);
          const selected = selectedId === image.id;
          const facesEstimate =
            detectedFaces > 0 && images.length > 0
              ? `~${Math.max(1, Math.round(detectedFaces / images.length))}`
              : estimateFacesFromResolution(image.width, image.height);
          const label = labels[image.id] ?? "";

          return (
            <Box
              key={image.id}
              role="listitem"
              draggable={!isDisabled && sortMode === "sequence"}
              onDragStart={onDragStart(image.id)}
              onDragOver={(event) => {
                event.preventDefault();
                setDropTargetId(image.id);
              }}
              onDragLeave={() => setDropTargetId((current) => (current === image.id ? null : current))}
              onDrop={onDropOn(image.id)}
              onClick={() => setSelectedId(image.id)}
              onKeyDown={(event) => {
                if (event.key === "Enter" || event.key === " ") {
                  event.preventDefault();
                  setSelectedId(image.id);
                }
              }}
              tabIndex={0}
              aria-selected={selected}
              aria-label={`Image ${image.imageSequence}${label ? `, ${label}` : ""}, ${status.label}`}
              sx={{
                border: 2,
                borderColor: dropTargetId === image.id
                  ? "primary.main"
                  : selected
                    ? "primary.main"
                    : "divider",
                borderRadius: 1.5,
                overflow: "hidden",
                bgcolor: "background.paper",
                boxShadow: dragId === image.id ? 4 : selected ? 2 : 0,
                transform: dragId === image.id ? "scale(1.02)" : "none",
                transition: (theme) =>
                  theme.transitions.create(["box-shadow", "transform", "border-color"], {
                    duration: theme.transitions.duration.short,
                  }),
                outlineOffset: 2,
                "&:focus-visible": {
                  outline: (theme) => `2px solid ${theme.palette.primary.main}`,
                },
              }}
            >
              <Box
                sx={{ position: "relative" }}
                onMouseEnter={(event) =>
                  setHoverAnchor({ el: event.currentTarget, image })
                }
                onMouseLeave={() => setHoverAnchor(null)}
              >
                <Box
                  component="img"
                  src={mediaAssetUrl(image.imageUrl) ?? undefined}
                  alt={label || image.originalFileName || `Classroom image ${image.imageSequence}`}
                  loading="lazy"
                  decoding="async"
                  sx={{
                    width: "100%",
                    height: 140,
                    objectFit: "cover",
                    display: "block",
                    bgcolor: "action.hover",
                  }}
                />
                <Stack
                  direction="row"
                  spacing={0.5}
                  sx={{
                    position: "absolute",
                    left: 6,
                    top: 6,
                    right: 6,
                    justifyContent: "space-between",
                    pointerEvents: "none",
                  }}
                >
                  <Chip size="small" label={`#${image.imageSequence}`} sx={{ bgcolor: "rgba(0,0,0,0.65)", color: "common.white" }} />
                  <Chip
                    size="small"
                    label={status.label}
                    color={status.color === "default" ? "default" : status.color === "primary" ? "primary" : status.color}
                    sx={{ opacity: 0.95 }}
                  />
                </Stack>
                <Stack
                  direction="row"
                  spacing={0.5}
                  sx={{
                    position: "absolute",
                    left: 6,
                    bottom: 6,
                    right: 6,
                    justifyContent: "space-between",
                    pointerEvents: "none",
                  }}
                >
                  <Chip
                    size="small"
                    label={quality.shortLabel}
                    sx={{ bgcolor: "rgba(0,0,0,0.65)", color: "common.white" }}
                  />
                  <Chip
                    size="small"
                    label={image.acquisitionMethod ?? "Upload"}
                    sx={{ bgcolor: "rgba(0,0,0,0.65)", color: "common.white" }}
                  />
                </Stack>
                {sortMode === "sequence" && (
                  <DragIndicatorIcon
                    fontSize="small"
                    sx={{
                      position: "absolute",
                      right: 6,
                      top: "50%",
                      transform: "translateY(-50%)",
                      color: "common.white",
                      bgcolor: "rgba(0,0,0,0.45)",
                      borderRadius: 0.5,
                      cursor: isDisabled ? "default" : "grab",
                    }}
                    aria-hidden
                  />
                )}
              </Box>

              <Stack spacing={0.75} sx={{ p: 1.25 }}>
                <Typography variant="body2" sx={{ fontWeight: 700 }} noWrap>
                  {label || image.originalFileName || `Image ${image.imageSequence}`}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  {formatResolution(image.width ?? undefined, image.height ?? undefined)} ·{" "}
                  {formatFileSizeLabel(image.fileSize ?? undefined)}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  {quality.stars} {quality.label} · Faces {facesEstimate}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  {formatCaptureTime(image.captureTimestamp ?? image.uploadedUtc)}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  Faces: {image.detectedFaceCount && image.detectedFaceCount > 0
                    ? image.detectedFaceCount
                    : facesEstimate}{" "}
                  · Batch: {image.batchStatus ?? status.label}
                </Typography>

                <TextField
                  size="small"
                  label="Label"
                  placeholder="Optional (e.g. Front Left)"
                  value={label}
                  disabled={isDisabled}
                  onChange={(event) => updateLabel(image.id, event.target.value)}
                  onClick={(event) => event.stopPropagation()}
                  slotProps={{
                    htmlInput: {
                      list: `image-label-suggestions-${image.id}`,
                      maxLength: 64,
                      "aria-label": `Label for image ${image.imageSequence}`,
                    },
                  }}
                />
                <datalist id={`image-label-suggestions-${image.id}`}>
                  {SUGGESTED_IMAGE_LABELS.map((suggestion) => (
                    <option key={suggestion} value={suggestion} />
                  ))}
                </datalist>

                <Stack direction="row" spacing={0.75}>
                  <Button
                    size="small"
                    variant="outlined"
                    startIcon={<InfoOutlinedIcon />}
                    disabled={isDisabled}
                    onClick={(event) => {
                      event.stopPropagation();
                      setDetailsImage(image);
                    }}
                    fullWidth
                  >
                    Details
                  </Button>
                  <Button
                    size="small"
                    variant="contained"
                    startIcon={<CloudUploadIcon />}
                    disabled={isDisabled}
                    onClick={(event) => {
                      event.stopPropagation();
                      openReplace(image.id);
                    }}
                    fullWidth
                  >
                    Replace
                  </Button>
                </Stack>
                <Stack direction="row" spacing={0.75}>
                  <Button
                    size="small"
                    variant="outlined"
                    color="error"
                    startIcon={<DeleteIcon />}
                    disabled={isDisabled}
                    onClick={(event) => {
                      event.stopPropagation();
                      void onDelete(image.id);
                      onNotify?.("Photo Deleted", "info");
                    }}
                    fullWidth
                  >
                    Delete
                  </Button>
                  {(image.status === SESSION_IMAGE_STATUS.Failed ||
                    image.status === SESSION_IMAGE_STATUS.Uploaded) &&
                    onRetryImageRecognition && (
                      <Button
                        size="small"
                        variant="outlined"
                        color="warning"
                        startIcon={<ReplayIcon />}
                        disabled={isDisabled}
                        onClick={(event) => {
                          event.stopPropagation();
                          void onRetryImageRecognition(image.id);
                          onNotify?.("Recognition Started", "info");
                        }}
                        fullWidth
                      >
                        Retry
                      </Button>
                    )}
                </Stack>
              </Stack>
            </Box>
          );
        })}
      </Box>

      <Typography variant="caption" color="text.secondary">
        Drag to reorder (Sequence sort). Replace or Retry restarts recognition for that image only —
        successfully Processed images are left unchanged.
      </Typography>

      <ClassroomImageDetailsDialog
        open={Boolean(detailsImage)}
        image={detailsImage}
        onClose={() => setDetailsImage(null)}
      />

      <Popover
        open={Boolean(hoverAnchor)}
        anchorEl={hoverAnchor?.el}
        onClose={() => setHoverAnchor(null)}
        disableRestoreFocus
        sx={{ pointerEvents: "none" }}
        anchorOrigin={{ vertical: "center", horizontal: "right" }}
        transformOrigin={{ vertical: "center", horizontal: "left" }}
        slotProps={{ paper: { sx: { p: 1.5, maxWidth: 320 } } }}
      >
        {hoverAnchor && (
          <Stack spacing={1}>
            <Box
              component="img"
              src={mediaAssetUrl(hoverAnchor.image.imageUrl) ?? undefined}
              alt="Hover preview"
              sx={{ width: "100%", maxHeight: 200, objectFit: "contain", borderRadius: 1 }}
            />
            <Typography variant="body2" sx={{ fontWeight: 700 }}>
              #{hoverAnchor.image.imageSequence}{" "}
              {labels[hoverAnchor.image.id] || hoverAnchor.image.originalFileName}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {formatResolution(
                hoverAnchor.image.width ?? undefined,
                hoverAnchor.image.height ?? undefined,
              )}{" "}
              · {hoverAnchor.image.acquisitionMethod ?? "Upload"}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {getImageQualityIndicator(hoverAnchor.image.blurScore).stars}{" "}
              {getImageQualityIndicator(hoverAnchor.image.blurScore).label}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {formatUploadedTimestamp(
                hoverAnchor.image.uploadedUtc
                  ? new Date(hoverAnchor.image.uploadedUtc)
                  : undefined,
              )}
            </Typography>
            <Tooltip title="Recognition status">
              <Chip
                size="small"
                label={getEnterpriseImageStatus(hoverAnchor.image.status).label}
                color={
                  getEnterpriseImageStatus(hoverAnchor.image.status).color === "default"
                    ? "default"
                    : (getEnterpriseImageStatus(hoverAnchor.image.status).color as
                        | "info"
                        | "warning"
                        | "success"
                        | "error"
                        | "primary")
                }
              />
            </Tooltip>
          </Stack>
        )}
      </Popover>

      <input
        ref={replaceInputRef}
        type="file"
        accept={CLASSROOM_PHOTO_ACCEPT}
        hidden
        onChange={onReplaceChange}
        aria-hidden
        tabIndex={-1}
      />
    </Stack>
  );
};

export default ClassroomPhotoCollectionPanel;
