import CameraAltIcon from "@mui/icons-material/CameraAlt";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import PersonIcon from "@mui/icons-material/Person";
import SyncIcon from "@mui/icons-material/Sync";
import VerifiedIcon from "@mui/icons-material/Verified";
import {
  Alert,
  Avatar,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  Fade,
  Grow,
  LinearProgress,
  Stack,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import { useCallback, useEffect, useRef, useState, type ChangeEvent, type DragEvent, type KeyboardEvent } from "react";
import {
  MEDIA_UPLOAD_ACCEPT,
  MEDIA_UPLOAD_MAX_BYTES,
  validateMediaUploadFile,
} from "./mediaUploadValidation";

export type PhotoCardProps = {
  /** Card heading, e.g. "Student Photo", "Staff Photo". */
  title?: string;
  photoUrl?: string | null;
  verified?: boolean;
  uploadedUtc?: string | null;
  uploading?: boolean;
  /** Upload progress 0–100. */
  uploadProgress?: number | null;
  deleting?: boolean;
  error?: string | null;
  disabled?: boolean;
  previewAlt?: string;
  helperText?: string;
  onUpload: (file: File) => void | Promise<void>;
  onDelete?: () => void | Promise<void>;
  onReplace?: (file: File) => void | Promise<void>;
  allowDelete?: boolean;
  allowReplace?: boolean;
  maxSizeBytes?: number;
  accept?: string;
  deleteDialogTitle?: string;
  deleteDialogMessage?: string;
  replaceDialogTitle?: string;
  replaceDialogMessage?: string;
};

const photoActionButtonSx = {
  flex: 1,
  minWidth: 0,
} as const;

const photoPrimaryButtonSx = {
  minWidth: { xs: 0, sm: 220 },
  width: { xs: "100%", sm: "auto" },
} as const;

const photoFocusVisibleSx = {
  "& .MuiButton-root:focus-visible": {
    outline: "2px solid",
    outlineColor: "primary.main",
    outlineOffset: 2,
  },
} as const;

const PHOTO_DELETE_DIALOG_TITLE_ID = "photo-delete-dialog-title";
const PHOTO_DELETE_DIALOG_DESC_ID = "photo-delete-dialog-description";
const PHOTO_REPLACE_DIALOG_TITLE_ID = "photo-replace-dialog-title";
const PHOTO_REPLACE_DIALOG_DESC_ID = "photo-replace-dialog-description";

const formatUploadedUtc = (value?: string | null): string | null => {
  if (!value) return null;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return null;
  return date.toLocaleString();
};

type PhotoStatusChipsProps = {
  hasPhoto: boolean;
  verified: boolean;
  uploadedUtc?: string | null;
};

const PhotoStatusChips = ({ hasPhoto, verified, uploadedUtc }: PhotoStatusChipsProps) => {
  const uploadedLabel = formatUploadedUtc(uploadedUtc);

  return (
    <Stack spacing={0.75}>
      <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", gap: 1 }}>
        {!hasPhoto ? (
          <Chip
            label="No Photo"
            size="small"
            variant="outlined"
            role="status"
            aria-label="Photo availability: No Photo"
            sx={{
              color: "text.secondary",
              borderColor: "divider",
              bgcolor: "action.hover",
            }}
          />
        ) : (
          <>
            <Chip
              label="Photo Available"
              size="small"
              color="info"
              variant="outlined"
              role="status"
              aria-label="Photo availability: Photo Available"
            />
            {verified ? (
              <Chip
                icon={<VerifiedIcon aria-hidden />}
                label="Verified"
                size="small"
                color="success"
                variant="filled"
                role="status"
                aria-label="Photo verification status: Verified"
              />
            ) : (
              <Chip
                label="Pending Verification"
                size="small"
                color="warning"
                variant="filled"
                role="status"
                aria-label="Photo verification status: Pending Verification"
              />
            )}
          </>
        )}
      </Stack>
      {hasPhoto && uploadedLabel && (
        <Typography variant="caption" color="text.secondary">
          Uploaded: {uploadedLabel}
        </Typography>
      )}
    </Stack>
  );
};

const PhotoCard = ({
  title = "Photo",
  photoUrl,
  verified = false,
  uploadedUtc,
  uploading = false,
  uploadProgress = null,
  deleting = false,
  error = null,
  disabled = false,
  previewAlt = "Photo preview",
  helperText,
  onUpload,
  onDelete,
  onReplace,
  allowDelete = true,
  allowReplace = true,
  maxSizeBytes = MEDIA_UPLOAD_MAX_BYTES,
  accept = MEDIA_UPLOAD_ACCEPT,
  deleteDialogTitle = "Delete Photo",
  deleteDialogMessage = "Are you sure you want to permanently remove this photo?",
  replaceDialogTitle = "Replace Photo?",
  replaceDialogMessage = "Existing photo will be overwritten.",
}: PhotoCardProps) => {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));

  const inputRef = useRef<HTMLInputElement>(null);
  const [dragOver, setDragOver] = useState(false);
  const [localPreviewUrl, setLocalPreviewUrl] = useState<string | null>(null);
  const [validationError, setValidationError] = useState<string | null>(null);
  const [showPreview, setShowPreview] = useState(false);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [replaceDialogOpen, setReplaceDialogOpen] = useState(false);
  const [pendingReplaceFile, setPendingReplaceFile] = useState<File | null>(null);

  const busy = uploading || deleting;
  const displayUrl = localPreviewUrl ?? photoUrl ?? null;
  const hasPhoto = Boolean(displayUrl);
  const hasPersistedPhoto = Boolean(photoUrl);
  const canReplace = allowReplace !== false;
  const canDelete = allowDelete !== false && Boolean(onDelete);
  const canPickFile = !disabled && !busy && (!hasPhoto || canReplace);
  const combinedError = validationError ?? error;
  const maxSizeMb = Math.round(maxSizeBytes / (1024 * 1024));

  useEffect(() => {
    if (hasPhoto) {
      const timer = window.setTimeout(() => setShowPreview(true), 20);
      return () => window.clearTimeout(timer);
    }
    setShowPreview(false);
    return undefined;
  }, [hasPhoto]);

  useEffect(() => {
    return () => {
      if (localPreviewUrl) {
        URL.revokeObjectURL(localPreviewUrl);
      }
    };
  }, [localPreviewUrl]);

  useEffect(() => {
    if (!uploading && photoUrl && localPreviewUrl) {
      URL.revokeObjectURL(localPreviewUrl);
      setLocalPreviewUrl(null);
    }
  }, [uploading, photoUrl, localPreviewUrl]);

  const handleFile = useCallback(
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

      if (hasPersistedPhoto && onReplace) {
        await onReplace(file);
        return;
      }
      await onUpload(file);
    },
    [hasPersistedPhoto, localPreviewUrl, maxSizeBytes, onReplace, onUpload]
  );

  const queueFileSelection = useCallback(
    (file: File) => {
      setValidationError(null);
      const validation = validateMediaUploadFile(file, maxSizeBytes);
      if (!validation.ok) {
        setValidationError(validation.error ?? "Invalid file.");
        return;
      }

      if (hasPersistedPhoto) {
        setPendingReplaceFile(file);
        setReplaceDialogOpen(true);
        return;
      }

      void handleFile(file);
    },
    [handleFile, hasPersistedPhoto, maxSizeBytes]
  );

  const onInputChange = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (file) {
      queueFileSelection(file);
    }
  };

  const onDrop = (event: DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    setDragOver(false);
    if (!canPickFile) return;

    const file = event.dataTransfer.files?.[0];
    if (file) {
      queueFileSelection(file);
    }
  };

  const openFilePicker = () => {
    if (canPickFile) {
      inputRef.current?.click();
    }
  };

  const handleDropZoneKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if ((event.key === "Enter" || event.key === " ") && canPickFile) {
      event.preventDefault();
      openFilePicker();
    }
  };

  const uploadFormatsHint = `Supported formats JPG, PNG, JPEG, WebP. Maximum size ${maxSizeMb} megabytes.`;

  const handleConfirmDelete = () => {
    setDeleteDialogOpen(false);
    void onDelete?.();
  };

  const handleConfirmReplace = () => {
    if (!pendingReplaceFile) return;
    const file = pendingReplaceFile;
    setPendingReplaceFile(null);
    setReplaceDialogOpen(false);
    void handleFile(file);
  };

  const handleCancelReplace = () => {
    setPendingReplaceFile(null);
    setReplaceDialogOpen(false);
  };

  const replaceActionIcon = uploading ? (
    <CircularProgress size={18} color="inherit" aria-label="Uploading photo" />
  ) : (
    <SyncIcon aria-hidden />
  );

  const uploadActionIcon = uploading ? (
    <CircularProgress size={18} color="inherit" aria-label="Uploading photo" />
  ) : (
    <CameraAltIcon aria-hidden />
  );

  return (
    <Card variant="outlined" sx={{ overflow: "hidden", minWidth: 0, ...photoFocusVisibleSx }}>
      <CardContent sx={{ p: { xs: 1.5, sm: 2, md: 2.5 } }}>
        <Stack spacing={1.5}>
          <Typography variant="h6" component="h3" id="photo-card-title">
            {title}
          </Typography>

          <PhotoStatusChips hasPhoto={hasPhoto} verified={verified} uploadedUtc={uploadedUtc} />

          <Box
            role="group"
            aria-label={`${title} upload area. ${uploadFormatsHint}`}
            aria-describedby="photo-card-upload-hint"
            tabIndex={canPickFile ? 0 : -1}
            onKeyDown={handleDropZoneKeyDown}
            onDragOver={(event) => {
              event.preventDefault();
              if (canPickFile) setDragOver(true);
            }}
            onDragLeave={() => setDragOver(false)}
            onDrop={onDrop}
            sx={{
              position: "relative",
              border: 1,
              borderStyle: "dashed",
              borderColor: dragOver ? "primary.main" : "divider",
              borderRadius: 2,
              bgcolor: dragOver ? "action.hover" : "background.default",
              minHeight: { xs: 240, sm: 280 },
              transition: "border-color 0.25s ease, background-color 0.25s ease, box-shadow 0.25s ease",
              boxShadow: dragOver ? 2 : 0,
              overflow: "hidden",
            }}
          >
            {uploading && (
              <LinearProgress
                variant={uploadProgress == null ? "indeterminate" : "determinate"}
                value={uploadProgress ?? 0}
                aria-label="Upload progress"
                sx={{
                  position: "absolute",
                  top: 0,
                  left: 0,
                  right: 0,
                  zIndex: 2,
                  borderRadius: 0,
                }}
              />
            )}

            <Box sx={{ position: "relative", minHeight: 280 }}>
              {!hasPhoto && (
                <Fade in={!hasPhoto} timeout={{ enter: 350, exit: 250 }}>
                  <Stack
                    spacing={1.5}
                    sx={{
                      alignItems: "center",
                      justifyContent: "center",
                      textAlign: "center",
                      px: 3,
                      py: 4,
                      minHeight: { xs: 240, sm: 280 },
                    }}
                  >
                    <Avatar
                      aria-hidden
                      sx={{
                        width: 120,
                        height: 120,
                        bgcolor: dragOver ? "primary.light" : "action.selected",
                        color: dragOver ? "primary.main" : "text.secondary",
                        border: 2,
                        borderColor: dragOver ? "primary.main" : "divider",
                        transition: "transform 0.25s ease, background-color 0.25s ease, border-color 0.25s ease",
                        transform: dragOver ? "scale(1.04)" : "scale(1)",
                      }}
                    >
                      <PersonIcon sx={{ fontSize: 56 }} />
                    </Avatar>

                    <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
                      No Photo Available
                    </Typography>

                    <Button
                      variant="contained"
                      startIcon={uploadActionIcon}
                      onClick={openFilePicker}
                      disabled={disabled || busy}
                      fullWidth={isMobile}
                      sx={photoPrimaryButtonSx}
                      aria-label={`Upload new photo. ${uploadFormatsHint}`}
                    >
                      Upload New Photo
                    </Button>

                    <Stack id="photo-card-upload-hint" spacing={0.25}>
                      <Typography variant="caption" color="text.secondary">
                        Supported formats: JPG, PNG, JPEG, WebP
                      </Typography>
                      <Typography variant="caption" color="text.secondary">
                        Maximum size: {maxSizeMb} MB
                      </Typography>
                      <Typography variant="caption" color="text.secondary" sx={{ pt: 0.5 }}>
                        or drag and drop a photo here
                      </Typography>
                    </Stack>
                  </Stack>
                </Fade>
              )}

              {hasPhoto && (
                <Grow in={showPreview} timeout={{ enter: 450, exit: 250 }}>
                  <Stack spacing={1.5} sx={{ p: 2, alignItems: "center", minHeight: 280 }}>
                    <Box
                      component="img"
                      src={displayUrl ?? undefined}
                      alt={previewAlt}
                      sx={{
                        maxHeight: 280,
                        maxWidth: "100%",
                        width: "auto",
                        objectFit: "contain",
                        borderRadius: 1.5,
                        opacity: busy ? 0.7 : 1,
                        transition: "opacity 0.25s ease, transform 0.35s ease",
                        transform: busy ? "scale(0.99)" : "scale(1)",
                        boxShadow: 1,
                      }}
                    />
                    <Stack
                      direction={isMobile ? "column" : "row"}
                      spacing={1.5}
                      sx={{ width: "100%", maxWidth: isMobile ? "100%" : 480, alignSelf: "stretch", mx: "auto" }}
                    >
                      {canReplace && (
                        <Button
                          variant="contained"
                          startIcon={replaceActionIcon}
                          onClick={openFilePicker}
                          disabled={disabled || busy}
                          fullWidth={isMobile}
                          sx={isMobile ? photoPrimaryButtonSx : photoActionButtonSx}
                          aria-label={`Replace photo. ${uploadFormatsHint}`}
                        >
                          Replace Photo
                        </Button>
                      )}
                      {canDelete && (
                        <Button
                          variant="outlined"
                          color="error"
                          startIcon={
                            deleting ? (
                              <CircularProgress size={18} color="inherit" aria-label="Deleting photo" />
                            ) : (
                              <DeleteOutlinedIcon aria-hidden />
                            )
                          }
                          onClick={() => setDeleteDialogOpen(true)}
                          disabled={disabled || busy}
                          fullWidth={isMobile}
                          sx={isMobile ? photoPrimaryButtonSx : photoActionButtonSx}
                          aria-label="Delete photo. Opens confirmation dialog."
                        >
                          Delete Photo
                        </Button>
                      )}
                    </Stack>
                    {canReplace && (
                      <Typography variant="caption" color="text.secondary">
                        or drag and drop a new photo here
                      </Typography>
                    )}
                  </Stack>
                </Grow>
              )}
            </Box>
          </Box>

          {helperText && (
            <Typography variant="body2" color="text.secondary">
              {helperText}
            </Typography>
          )}

          {combinedError && <Alert severity="error">{combinedError}</Alert>}

          <input
            ref={inputRef}
            type="file"
            accept={accept}
            hidden
            tabIndex={-1}
            aria-hidden
            onChange={onInputChange}
          />
        </Stack>
      </CardContent>

      <Dialog
        open={deleteDialogOpen}
        onClose={() => {
          if (deleting) return;
          setDeleteDialogOpen(false);
        }}
        maxWidth="xs"
        fullWidth
        aria-labelledby={PHOTO_DELETE_DIALOG_TITLE_ID}
        aria-describedby={PHOTO_DELETE_DIALOG_DESC_ID}
        slotProps={{
          paper: {
            role: "alertdialog",
            sx: photoFocusVisibleSx,
          },
        }}
      >
        <DialogTitle id={PHOTO_DELETE_DIALOG_TITLE_ID}>{deleteDialogTitle}</DialogTitle>
        <DialogContent>
          <DialogContentText id={PHOTO_DELETE_DIALOG_DESC_ID}>{deleteDialogMessage}</DialogContentText>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2, ...photoFocusVisibleSx }}>
          <Button autoFocus onClick={() => setDeleteDialogOpen(false)} disabled={deleting}>
            Cancel
          </Button>
          <Button
            variant="contained"
            color="error"
            onClick={handleConfirmDelete}
            disabled={deleting}
            aria-label="Confirm delete photo"
            startIcon={
              deleting ? <CircularProgress size={18} color="inherit" aria-label="Deleting photo" /> : undefined
            }
          >
            Delete
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog
        open={replaceDialogOpen}
        onClose={() => {
          if (uploading) return;
          handleCancelReplace();
        }}
        maxWidth="xs"
        fullWidth
        aria-labelledby={PHOTO_REPLACE_DIALOG_TITLE_ID}
        aria-describedby={PHOTO_REPLACE_DIALOG_DESC_ID}
        slotProps={{
          paper: {
            role: "alertdialog",
            sx: photoFocusVisibleSx,
          },
        }}
      >
        <DialogTitle id={PHOTO_REPLACE_DIALOG_TITLE_ID}>{replaceDialogTitle}</DialogTitle>
        <DialogContent>
          <DialogContentText id={PHOTO_REPLACE_DIALOG_DESC_ID}>{replaceDialogMessage}</DialogContentText>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2, ...photoFocusVisibleSx }}>
          <Button autoFocus onClick={handleCancelReplace} disabled={uploading}>
            Cancel
          </Button>
          <Button
            variant="contained"
            onClick={handleConfirmReplace}
            disabled={uploading || !pendingReplaceFile}
            aria-label="Confirm replace photo"
            startIcon={
              uploading ? <CircularProgress size={18} color="inherit" aria-label="Uploading photo" /> : undefined
            }
          >
            Replace
          </Button>
        </DialogActions>
      </Dialog>
    </Card>
  );
};

export default PhotoCard;
