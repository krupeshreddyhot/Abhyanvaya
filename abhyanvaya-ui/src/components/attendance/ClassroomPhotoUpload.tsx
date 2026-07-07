import { Box, Button, LinearProgress, Stack, Typography } from "@mui/material";
import { useRef, type ChangeEvent } from "react";
import { CLASSROOM_PHOTO_ACCEPT } from "../../constants/classroomPhotoUploadHints";
import { PreviewStatus, UploadStatus, type UploadState } from "../../types/uploadState";
import { formatBytes } from "../../utils/uploadProgress";
import { getUploadRetryLabel } from "../../services/attendanceSessionService";
import { ClassroomPhotoDropZone } from "./ClassroomPhotoDropZone";
import { ClassroomPhotoPreviewPanel } from "./ClassroomPhotoPreviewPanel";

export type ClassroomPhotoUploadProps = {
  disabled?: boolean;
  uploadState: UploadState;
  onSelectFile: (file: File) => void | Promise<void>;
  onReset: () => void;
  onRetry?: () => void | Promise<void>;
  isUploading?: boolean;
};

export const ClassroomPhotoUpload = ({
  disabled = false,
  uploadState,
  onSelectFile,
  onReset,
  onRetry,
  isUploading = false,
}: ClassroomPhotoUploadProps) => {
  const replaceInputRef = useRef<HTMLInputElement>(null);

  const bytesUploaded = uploadState.bytesUploaded ?? 0;
  const bytesTotal = uploadState.bytesTotal ?? uploadState.fileSize ?? 0;
  const bytesRemaining = Math.max(0, bytesTotal - bytesUploaded);
  const showProgress =
    uploadState.uploadStatus === UploadStatus.Uploading ||
    uploadState.uploadStatus === UploadStatus.Retrying ||
    uploadState.uploadStatus === UploadStatus.Completed;
  const retryLabel =
    uploadState.uploadStatus === UploadStatus.Retrying && uploadState.retryAttempt
      ? getUploadRetryLabel(uploadState.retryAttempt - 1)
      : null;

  const hasPreview =
    Boolean(uploadState.previewUrl) && uploadState.previewStatus !== PreviewStatus.None;

  const openReplacePicker = () => {
    if (!disabled && !isUploading) {
      replaceInputRef.current?.click();
    }
  };

  const onReplaceInputChange = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (file) {
      void onSelectFile(file);
    }
  };

  return (
    <Stack spacing={2}>
      {hasPreview ? (
        <ClassroomPhotoPreviewPanel
          uploadState={uploadState}
          disabled={disabled}
          busy={isUploading}
          onReplace={openReplacePicker}
          onDelete={onReset}
        />
      ) : (
        <ClassroomPhotoDropZone
          disabled={disabled}
          busy={isUploading}
          error={uploadState.errorMessage}
          onSelectFile={onSelectFile}
        />
      )}

      <input
        ref={replaceInputRef}
        type="file"
        accept={CLASSROOM_PHOTO_ACCEPT}
        hidden
        onChange={onReplaceInputChange}
        aria-hidden
        tabIndex={-1}
      />

      {retryLabel && (
        <Typography variant="body2" color="warning.main" role="status">
          {retryLabel}
        </Typography>
      )}

      {showProgress && (
        <Box aria-label="Upload progress">
          <Stack direction="row" sx={{ justifyContent: "space-between", mb: 0.5 }}>
            <Typography variant="caption" color="text.secondary">
              {uploadState.progress}% uploaded
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {formatBytes(bytesUploaded)} / {formatBytes(bytesTotal)}
            </Typography>
          </Stack>
          <LinearProgress variant="determinate" value={uploadState.progress} aria-label="Upload progress bar" />
          <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 0.5 }}>
            Remaining: {formatBytes(bytesRemaining)}
          </Typography>
        </Box>
      )}

      {uploadState.uploadStatus === UploadStatus.Failed && onRetry && (
        <Button variant="outlined" onClick={() => void onRetry()} disabled={disabled} aria-label="Retry upload">
          Retry
        </Button>
      )}
    </Stack>
  );
};

export default ClassroomPhotoUpload;
