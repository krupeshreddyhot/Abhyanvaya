import PhotoCameraOutlinedIcon from "@mui/icons-material/PhotoCameraOutlined";
import { Alert, Box, Button, Paper, Stack, Typography } from "@mui/material";
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
  error?: string | null;
  onSelectFile: (file: File) => void | Promise<void>;
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
  error = null,
  onSelectFile,
}: ClassroomPhotoDropZoneProps) => {
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragOver, setDragOver] = useState(false);
  const [validationError, setValidationError] = useState<string | null>(null);

  const isDisabled = disabled || busy;
  const combinedError = validationError ?? error;

  const processFile = useCallback(
    async (file: File) => {
      setValidationError(null);
      const validation = validateMediaUploadFile(file, CLASSROOM_PHOTO_MAX_BYTES);
      if (!validation.ok) {
        setValidationError(validation.error ?? "Invalid file.");
        return;
      }

      await onSelectFile(file);
    },
    [onSelectFile],
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
    if (isDisabled) {
      return;
    }

    const file = event.dataTransfer.files?.[0];
    if (file) {
      void processFile(file);
    }
  };

  const openFilePicker = () => {
    if (!isDisabled) {
      inputRef.current?.click();
    }
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
        onClick={openFilePicker}
        onKeyDown={(event) => {
          if ((event.key === "Enter" || event.key === " ") && !isDisabled) {
            event.preventDefault();
            openFilePicker();
          }
        }}
        role="button"
        tabIndex={isDisabled ? -1 : 0}
        aria-label="Upload classroom photo. Drag and drop or press Enter to select a file."
        aria-disabled={isDisabled}
        sx={{
          minHeight: 280,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          cursor: isDisabled ? "not-allowed" : "pointer",
          borderStyle: "dashed",
          borderWidth: 2,
          borderColor: dragOver ? "primary.main" : "divider",
          bgcolor: dragOver ? "action.hover" : "background.paper",
          opacity: isDisabled ? 0.6 : 1,
          transition: (theme) =>
            theme.transitions.create(["border-color", "background-color", "box-shadow"], {
              duration: theme.transitions.duration.short,
            }),
          "&:hover": isDisabled
            ? undefined
            : {
                borderColor: "primary.main",
                bgcolor: "action.hover",
                boxShadow: 1,
              },
          "&:focus-visible": {
            outline: (theme) => `2px solid ${theme.palette.primary.main}`,
            outlineOffset: 2,
          },
          px: { xs: 2.5, sm: 4 },
          py: { xs: 4, sm: 5 },
        }}
      >
        <Stack spacing={3} sx={{ alignItems: "center", textAlign: "center", maxWidth: 460, width: "100%" }}>
          <Box
            sx={{
              color: dragOver ? "primary.main" : "text.secondary",
              display: "flex",
              transition: (theme) => theme.transitions.create("color"),
            }}
            aria-hidden
          >
            <PhotoCameraOutlinedIcon sx={{ fontSize: { xs: 64, sm: 72 } }} />
          </Box>

          <Stack spacing={1.5} sx={{ alignItems: "center", width: "100%" }}>
            <Typography variant="h6" component="p" sx={{ fontWeight: 600 }}>
              {CLASSROOM_PHOTO_DROP_TITLE}
            </Typography>

            <Typography variant="body2" color="text.secondary" sx={{ letterSpacing: 1 }}>
              OR
            </Typography>

            <Button
              variant="contained"
              size="large"
              disabled={isDisabled}
              onClick={(event) => {
                event.stopPropagation();
                openFilePicker();
              }}
              aria-label={CLASSROOM_PHOTO_SELECT_LABEL}
              sx={{ px: 3 }}
            >
              {CLASSROOM_PHOTO_SELECT_LABEL}
            </Button>
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
        hidden
        onChange={onInputChange}
        aria-hidden
        tabIndex={-1}
      />

      {combinedError && (
        <Alert severity="error" role="alert">
          {combinedError}
        </Alert>
      )}
    </Stack>
  );
};

export default ClassroomPhotoDropZone;
