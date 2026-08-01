import PhotoCameraOutlinedIcon from "@mui/icons-material/PhotoCameraOutlined";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  LinearProgress,
  Paper,
  Stack,
  Typography,
} from "@mui/material";
import { useCallback, useRef, useState, type ChangeEvent, type DragEvent, type ReactNode } from "react";
import {
  CLASSROOM_PHOTO_ACCEPT,
  CLASSROOM_PHOTO_DROP_TITLE,
  CLASSROOM_PHOTO_MAX_SIZE_LABEL,
  CLASSROOM_PHOTO_MIN_RESOLUTION_LABEL,
  CLASSROOM_PHOTO_SELECT_LABEL,
  CLASSROOM_PHOTO_SUPPORTED_FORMATS,
} from "../../constants/classroomPhotoUploadHints";
import { CLASSROOM_PHOTO_MAX_BYTES } from "../../constants/classroomPhotoConstraints";
import { validateMediaUploadFile } from "../media/mediaUploadValidation";

export type ClassroomPhotoDropZoneProps = {
  disabled?: boolean;
  busy?: boolean;
  busyLabel?: string | null;
  error?: string | null;
  multiple?: boolean;
  remainingSlots?: number;
  onSelectFile: (file: File) => void | Promise<void>;
  onSelectFiles?: (files: File[]) => void | Promise<void>;
};

type RequirementBlockProps = {
  title: string;
  children: ReactNode;
};

const RequirementBlock = ({ title, children }: RequirementBlockProps) => (
  <Stack spacing={0.5} sx={{ alignItems: "center" }}>
    <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600, letterSpacing: 0.2 }}>
      {title}
    </Typography>
    {children}
  </Stack>
);

export const ClassroomPhotoDropZone = ({
  disabled = false,
  busy = false,
  busyLabel = null,
  error = null,
  multiple = false,
  remainingSlots,
  onSelectFile,
  onSelectFiles,
}: ClassroomPhotoDropZoneProps) => {
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragOver, setDragOver] = useState(false);
  const [validationError, setValidationError] = useState<string | null>(null);

  const isDisabled = disabled || busy;
  const combinedError = validationError ?? error;

  const processFiles = useCallback(
    async (fileList: FileList | File[]) => {
      setValidationError(null);
      const files = Array.from(fileList);
      if (files.length === 0) {
        return;
      }

      const limited =
        remainingSlots != null && remainingSlots >= 0 ? files.slice(0, remainingSlots) : files;

      if (limited.length === 0) {
        setValidationError("No remaining image slots in this session.");
        return;
      }

      for (const file of limited) {
        const validation = validateMediaUploadFile(file, CLASSROOM_PHOTO_MAX_BYTES);
        if (!validation.ok) {
          setValidationError(validation.error ?? "Invalid file.");
          return;
        }
      }

      try {
        if (multiple && onSelectFiles) {
          await onSelectFiles(limited);
          return;
        }
        await onSelectFile(limited[0]);
      } catch (err) {
        const message = err instanceof Error ? err.message : "Unable to upload image.";
        setValidationError(message);
      }
    },
    [multiple, onSelectFile, onSelectFiles, remainingSlots],
  );

  const onInputChange = (event: ChangeEvent<HTMLInputElement>) => {
    // Copy FileList BEFORE clearing the input. In Chromium, `files` is live —
    // resetting value="" empties the same FileList reference, so the picker
    // path becomes a no-op while drag-and-drop (dataTransfer) still works.
    const files = event.target.files ? Array.from(event.target.files) : [];
    event.target.value = "";
    if (files.length > 0) {
      void processFiles(files);
    }
  };

  const onDrop = (event: DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    setDragOver(false);
    if (isDisabled) {
      return;
    }

    const list = event.dataTransfer.files;
    if (list && list.length > 0) {
      void processFiles(list);
    }
  };

  const openFilePicker = (event?: { stopPropagation?: () => void }) => {
    event?.stopPropagation?.();
    if (isDisabled) {
      return;
    }
    inputRef.current?.click();
  };

  return (
    <Stack spacing={1.5}>
      <Typography variant="subtitle1" component="h3" sx={{ fontWeight: 600 }}>
        Classroom Photo
      </Typography>

      <Paper
        variant="outlined"
        onDragOver={(event) => {
          event.preventDefault();
          if (!isDisabled) {
            setDragOver(true);
          }
        }}
        onDragLeave={() => setDragOver(false)}
        onDrop={onDrop}
        role="group"
        aria-label={
          multiple
            ? "Upload classroom photos. Drag and drop or use the select button."
            : "Upload classroom photo. Drag and drop or use the select button."
        }
        aria-disabled={isDisabled}
        sx={{
          minHeight: 280,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          cursor: isDisabled ? "not-allowed" : "default",
          borderStyle: "dashed",
          borderWidth: 2,
          borderColor: dragOver ? "primary.main" : "divider",
          bgcolor: dragOver ? "action.hover" : "background.paper",
          opacity: isDisabled ? 0.6 : 1,
          transition: (theme) =>
            theme.transitions.create(["border-color", "background-color", "box-shadow"], {
              duration: theme.transitions.duration.short,
            }),
          px: { xs: 2.5, sm: 4 },
          py: { xs: 4, sm: 5 },
        }}
      >
        <Stack spacing={3} sx={{ alignItems: "center", textAlign: "center", maxWidth: 460, width: "100%" }}>
          <Box
            sx={{
              color: dragOver ? "primary.main" : "text.secondary",
              display: "flex",
            }}
            aria-hidden
          >
            <PhotoCameraOutlinedIcon sx={{ fontSize: { xs: 64, sm: 72 } }} />
          </Box>

          <Stack spacing={1.5} sx={{ alignItems: "center", width: "100%" }}>
            <Typography variant="h6" component="p" sx={{ fontWeight: 600 }}>
              {busy
                ? busyLabel || "Preparing classroom photo…"
                : multiple
                  ? "Drag & drop classroom photos"
                  : CLASSROOM_PHOTO_DROP_TITLE}
            </Typography>

            {busy ? (
              <Stack spacing={1} sx={{ alignItems: "center", width: "100%", maxWidth: 280 }}>
                <CircularProgress size={28} aria-label="Preparing upload" />
                <LinearProgress sx={{ width: "100%" }} />
                <Typography variant="body2" color="text.secondary">
                  Please wait — processing and uploading your photo.
                </Typography>
              </Stack>
            ) : (
              <>
                <Typography variant="body2" color="text.secondary" sx={{ letterSpacing: 1 }}>
                  OR
                </Typography>

                <Button
                  variant="contained"
                  size="large"
                  disabled={isDisabled}
                  onClick={(event) => openFilePicker(event)}
                  aria-label={CLASSROOM_PHOTO_SELECT_LABEL}
                  sx={{ px: 3 }}
                >
                  {CLASSROOM_PHOTO_SELECT_LABEL}
                </Button>
              </>
            )}
          </Stack>

          <Stack spacing={2} sx={{ pt: 1, width: "100%", maxWidth: 280 }}>
            <RequirementBlock title="Supported Formats">
              <Stack spacing={0.25}>
                {CLASSROOM_PHOTO_SUPPORTED_FORMATS.map((format) => (
                  <Typography key={format} variant="body2" color="text.secondary">
                    • {format}
                  </Typography>
                ))}
              </Stack>
            </RequirementBlock>

            <RequirementBlock title="Maximum File Size">
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {CLASSROOM_PHOTO_MAX_SIZE_LABEL}
              </Typography>
            </RequirementBlock>

            <RequirementBlock title="Minimum Resolution">
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {CLASSROOM_PHOTO_MIN_RESOLUTION_LABEL}
              </Typography>
            </RequirementBlock>
          </Stack>
        </Stack>
      </Paper>

      <input
        ref={inputRef}
        type="file"
        accept={CLASSROOM_PHOTO_ACCEPT}
        multiple={multiple}
        hidden
        onChange={onInputChange}
      />

      {combinedError && (
        <Alert severity="error" role="alert">
          {combinedError}
        </Alert>
      )}

      {disabled && !busy && (
        <Alert severity="warning" role="status">
          Upload is disabled until the class roster finishes loading.
        </Alert>
      )}
    </Stack>
  );
};

export default ClassroomPhotoDropZone;
