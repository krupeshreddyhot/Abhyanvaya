import CloudUploadIcon from "@mui/icons-material/CloudUpload";
import DeleteIcon from "@mui/icons-material/Delete";
import ImageOutlinedIcon from "@mui/icons-material/ImageOutlined";
import {
  Alert,
  Box,
  Button,
  LinearProgress,
  Stack,
  Typography,
} from "@mui/material";
import { useCallback, useEffect, useRef, useState, type ChangeEvent, type DragEvent } from "react";
import {
  MEDIA_UPLOAD_ACCEPT,
  MEDIA_UPLOAD_MAX_BYTES,
  validateMediaUploadFile,
} from "./mediaUploadValidation";

export type MediaUploadProps = {
  /** Field label shown above the control. */
  label?: string;
  /** Helper text below the label. */
  helperText?: string;
  /** Remote preview URL (e.g. after successful upload). */
  previewUrl?: string | null;
  /** Alt text for preview image. */
  previewAlt?: string;
  /** Whether an upload is in progress. */
  uploading?: boolean;
  /** Upload progress 0–100; shown when uploading. */
  uploadProgress?: number | null;
  /** Whether delete is in progress. */
  deleting?: boolean;
  /** External error message (e.g. API failure). */
  error?: string | null;
  disabled?: boolean;
  /** Show delete control when a preview exists and onDelete is provided. */
  showDelete?: boolean;
  /** When false, hides upload/replace button and disables drag-and-drop. */
  allowUpload?: boolean;
  /** When false, hides the inline preview (use when an external preview is shown). */
  showPreview?: boolean;
  maxSizeBytes?: number;
  accept?: string;
  onUpload: (file: File) => void | Promise<void>;
  onDelete?: () => void | Promise<void>;
};

const MediaUpload = ({
  label = "Image",
  helperText = "JPG, JPEG, PNG, or WebP. Maximum 5 MB.",
  previewUrl,
  previewAlt = "Upload preview",
  uploading = false,
  uploadProgress = null,
  deleting = false,
  error = null,
  disabled = false,
  showDelete = true,
  allowUpload = true,
  showPreview = true,
  maxSizeBytes = MEDIA_UPLOAD_MAX_BYTES,
  accept = MEDIA_UPLOAD_ACCEPT,
  onUpload,
  onDelete,
}: MediaUploadProps) => {
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragOver, setDragOver] = useState(false);
  const [localPreviewUrl, setLocalPreviewUrl] = useState<string | null>(null);
  const [validationError, setValidationError] = useState<string | null>(null);

  const displayPreview = localPreviewUrl ?? previewUrl ?? null;
  const busy = uploading || deleting;
  const hasPreview = Boolean(displayPreview);
  const combinedError = validationError ?? error;

  useEffect(() => {
    return () => {
      if (localPreviewUrl) {
        URL.revokeObjectURL(localPreviewUrl);
      }
    };
  }, [localPreviewUrl]);

  const processFile = useCallback(
    async (file: File) => {
      setValidationError(null);
      const validation = validateMediaUploadFile(file, maxSizeBytes);
      if (!validation.ok) {
        setValidationError(validation.error ?? "Invalid file.");
        return;
      }

      if (localPreviewUrl) {
        URL.revokeObjectURL(localPreviewUrl);
      }
      setLocalPreviewUrl(URL.createObjectURL(file));
      await onUpload(file);
    },
    [localPreviewUrl, maxSizeBytes, onUpload]
  );

  const onInputChange = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (file) {
      void processFile(file);
    }
  };

  const onDrop = (event: DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    setDragOver(false);
    if (disabled || busy || !allowUpload) return;

    const file = event.dataTransfer.files?.[0];
    if (file) {
      void processFile(file);
    }
  };

  const openFilePicker = () => {
    if (!disabled && !busy && allowUpload) {
      inputRef.current?.click();
    }
  };

  return (
    <Stack spacing={1.5}>
      {label && (
        <Typography variant="subtitle2" component="div">
          {label}
        </Typography>
      )}
      {helperText && (
        <Typography variant="body2" color="text.secondary">
          {helperText}
        </Typography>
      )}

      <Box
        onDragOver={(e) => {
          e.preventDefault();
          if (!disabled && !busy && allowUpload) setDragOver(true);
        }}
        onDragLeave={() => setDragOver(false)}
        onDrop={onDrop}
        sx={{
          border: 1,
          borderStyle: "dashed",
          borderColor: dragOver ? "primary.main" : "divider",
          borderRadius: 1,
          bgcolor: dragOver ? "action.hover" : "background.paper",
          p: 2,
          transition: "border-color 0.2s, background-color 0.2s",
        }}
      >
        <Stack spacing={2} sx={{ alignItems: "center" }}>
          {showPreview && (
            hasPreview ? (
              <Box
                component="img"
                src={displayPreview ?? undefined}
                alt={previewAlt}
                sx={{
                  maxHeight: 160,
                  maxWidth: "100%",
                  objectFit: "contain",
                  borderRadius: 1,
                  border: 1,
                  borderColor: "divider",
                  bgcolor: "background.default",
                }}
              />
            ) : (
              <ImageOutlinedIcon sx={{ fontSize: 48, color: "text.disabled" }} />
            )
          )}

          <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", justifyContent: "center" }}>
            {allowUpload && (
              <Button
                variant="contained"
                startIcon={<CloudUploadIcon />}
                onClick={openFilePicker}
                disabled={disabled || busy}
              >
                {hasPreview ? "Replace" : "Upload"}
              </Button>
            )}
            {hasPreview && showDelete && onDelete && (
              <Button
                variant="outlined"
                color="error"
                startIcon={<DeleteIcon />}
                onClick={() => void onDelete()}
                disabled={disabled || busy}
              >
                Delete
              </Button>
            )}
          </Stack>

          {allowUpload && (
            <Typography variant="caption" color="text.secondary">
              or drag and drop a file here
            </Typography>
          )}
        </Stack>
      </Box>

      <input
        ref={inputRef}
        type="file"
        accept={accept}
        hidden
        onChange={onInputChange}
      />

      {uploading && (
        <LinearProgress
          variant={uploadProgress == null ? "indeterminate" : "determinate"}
          value={uploadProgress ?? 0}
          aria-label="Upload progress"
        />
      )}

      {combinedError && <Alert severity="error">{combinedError}</Alert>}
    </Stack>
  );
};

export default MediaUpload;
