import { Box, Button, LinearProgress, Stack, Typography } from "@mui/material";
import { useRef, useState, type ChangeEvent } from "react";
import { CLASSROOM_PHOTO_ACCEPT } from "../../constants/classroomPhotoUploadHints";
import { PreviewStatus, UploadStatus, type UploadState } from "../../types/uploadState";
import type {
  CapturedFrame,
  ClassroomPhotoCaptureContext,
  PhotoAcquisitionMethod,
} from "../../types/photoAcquisition";
import { formatBytes } from "../../utils/uploadProgress";
import { processClassroomImageFile } from "../../utils/classroomImageProcessing";
import { tryGetCaptureLocation } from "../../utils/geolocationCapture";
import { getUploadRetryLabel } from "../../services/attendanceSessionService";
import { CameraCapturePanel } from "./CameraCapturePanel";
import { ClassroomPhotoDropZone } from "./ClassroomPhotoDropZone";
import { ClassroomPhotoPreviewPanel } from "./ClassroomPhotoPreviewPanel";
import { PhotoAcquisitionMethodTabs } from "./PhotoAcquisitionMethodTabs";

export type ClassroomPhotoUploadProps = {
  disabled?: boolean;
  uploadState: UploadState;
  onSelectFile: (
    file: File,
    captureContext?: ClassroomPhotoCaptureContext,
  ) => void | Promise<void>;
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
  const [method, setMethod] = useState<PhotoAcquisitionMethod>("upload");
  const [prepareError, setPrepareError] = useState<string | null>(null);
  const [preparing, setPreparing] = useState(false);

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

  const submitUploadFile = async (file: File) => {
    setPrepareError(null);
    setPreparing(true);
    try {
      const processed = await processClassroomImageFile(file, file.name);
      const geo = await tryGetCaptureLocation();
      await onSelectFile(processed.file, {
        acquisitionMethod: "Upload",
        captureTimestampUtc: new Date().toISOString(),
        blurScore: processed.blurScore ?? undefined,
        latitude: geo?.latitude,
        longitude: geo?.longitude,
        captureDevice: typeof navigator !== "undefined" ? navigator.userAgent.slice(0, 100) : undefined,
      });
    } catch (error) {
      setPrepareError(error instanceof Error ? error.message : "Unable to prepare image.");
    } finally {
      setPreparing(false);
    }
  };

  const submitCapturedFrame = async (
    frame: CapturedFrame,
    acquisitionMethod: "CameraCapture" | "CameraMultiCapture",
    location: { latitude: number; longitude: number } | null,
  ) => {
    setPrepareError(null);
    await onSelectFile(frame.file, {
      acquisitionMethod,
      captureTimestampUtc: frame.capturedAt.toISOString(),
      blurScore: frame.blurScore ?? undefined,
      latitude: location?.latitude,
      longitude: location?.longitude,
      captureDevice: typeof navigator !== "undefined" ? navigator.userAgent.slice(0, 100) : undefined,
      orientation: 1,
    });
  };

  const onReplaceInputChange = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (file) {
      void submitUploadFile(file);
    }
  };

  const panelBusy = isUploading || preparing;

  return (
    <Stack spacing={2}>
      {!hasPreview && (
        <PhotoAcquisitionMethodTabs
          value={method}
          onChange={(next) => {
            setPrepareError(null);
            setMethod(next);
          }}
          disabled={panelBusy || disabled}
        />
      )}

      {hasPreview ? (
        <ClassroomPhotoPreviewPanel
          uploadState={uploadState}
          disabled={disabled}
          busy={panelBusy}
          onReplace={openReplacePicker}
          onDelete={onReset}
        />
      ) : method === "upload" ? (
        <ClassroomPhotoDropZone
          disabled={disabled}
          busy={panelBusy}
          error={prepareError ?? uploadState.errorMessage}
          onSelectFile={(file) => void submitUploadFile(file)}
        />
      ) : (
        <Stack spacing={1.5}>
          <Typography variant="subtitle1" component="h3" sx={{ fontWeight: 600 }}>
            Classroom Photo
          </Typography>
          <CameraCapturePanel
            mode={method}
            disabled={disabled}
            busy={panelBusy}
            onConfirmSingle={(frame, location) =>
              void submitCapturedFrame(frame, "CameraCapture", location)
            }
            onConfirmMultiSelection={(frame, location) =>
              void submitCapturedFrame(frame, "CameraMultiCapture", location)
            }
          />
          {(prepareError || uploadState.errorMessage) && (
            <Typography variant="body2" color="error" role="alert">
              {prepareError ?? uploadState.errorMessage}
            </Typography>
          )}
        </Stack>
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
