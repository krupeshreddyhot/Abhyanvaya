import { Box, Button, LinearProgress, Stack, Typography } from "@mui/material";
import { useState } from "react";
import { CLASSROOM_PHOTO_MAX_IMAGES_PER_SESSION } from "../../constants/classroomPhotoConstraints";
import { PreviewStatus, UploadStatus, type UploadState } from "../../types/uploadState";
import type {
  CapturedFrame,
  ClassroomPhotoCaptureContext,
  PhotoAcquisitionMethod,
} from "../../types/photoAcquisition";
import type { AttendanceSessionImage } from "../../types/sessionImage";
import { formatBytes } from "../../utils/uploadProgress";
import { processClassroomImageFile } from "../../utils/classroomImageProcessing";
import { prepareClassroomUploadFile } from "../../utils/prepareClassroomUploadFile";
import { getUploadRetryLabel } from "../../services/attendanceSessionService";
import { CameraCapturePanel } from "./CameraCapturePanel";
import { ClassroomPhotoCollectionPanel } from "./ClassroomPhotoCollectionPanel";
import { ClassroomPhotoDropZone } from "./ClassroomPhotoDropZone";
import { PhotoAcquisitionMethodTabs } from "./PhotoAcquisitionMethodTabs";

export type ClassroomPhotoUploadProps = {
  disabled?: boolean;
  uploadState: UploadState;
  images: AttendanceSessionImage[];
  canAddMore: boolean;
  collectionError?: string | null;
  sessionId?: string;
  detectedFaces?: number;
  onSelectFile: (
    file: File,
    captureContext?: ClassroomPhotoCaptureContext,
  ) => void | Promise<void>;
  onSelectFiles: (
    files: File[],
    captureContext?: ClassroomPhotoCaptureContext,
  ) => void | Promise<void>;
  onDeleteImage: (imageId: string) => void | Promise<void>;
  onDeleteAllImages?: () => void | Promise<void>;
  onReplaceImage: (
    imageId: string,
    file: File,
    captureContext?: ClassroomPhotoCaptureContext,
  ) => void | Promise<void>;
  onReorderImages: (orderedIds: string[]) => void | Promise<void>;
  onRetryRecognition?: () => void | Promise<void>;
  onRetryImageRecognition?: (imageId: string) => void | Promise<void>;
  onReset: () => void;
  onRetry?: () => void | Promise<void>;
  onNotify?: (message: string, severity?: "success" | "info" | "warning" | "error") => void;
  isUploading?: boolean;
};

export const ClassroomPhotoUpload = ({
  disabled = false,
  uploadState,
  images,
  canAddMore,
  collectionError = null,
  sessionId,
  detectedFaces = 0,
  onSelectFile,
  onSelectFiles,
  onDeleteImage,
  onDeleteAllImages,
  onReplaceImage,
  onReorderImages,
  onRetryRecognition,
  onRetryImageRecognition,
  onRetry,
  onNotify,
  isUploading = false,
}: ClassroomPhotoUploadProps) => {
  const [method, setMethod] = useState<PhotoAcquisitionMethod>("upload");
  const [prepareError, setPrepareError] = useState<string | null>(null);
  const [preparing, setPreparing] = useState(false);
  const [showAcquisition, setShowAcquisition] = useState(images.length === 0);

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

  const panelBusy = isUploading || preparing;
  const hasCollection = images.length > 0;
  const showAddUi = !hasCollection || showAcquisition;

  const prepareAndUploadOne = async (
    file: File,
    captureContext: ClassroomPhotoCaptureContext,
  ) => {
    const processed = await processClassroomImageFile(file, file.name);
    await onSelectFile(processed.file, {
      ...captureContext,
      blurScore: processed.blurScore ?? captureContext.blurScore,
    });
  };

  const submitUploadFiles = async (files: File[]) => {
    setPrepareError(null);
    setPreparing(true);
    onNotify?.("Preparing classroom photo…", "info");
    try {
      const device =
        typeof navigator !== "undefined" ? navigator.userAgent.slice(0, 100) : undefined;
      const remaining = CLASSROOM_PHOTO_MAX_IMAGES_PER_SESSION - images.length;
      const batch = files.slice(0, Math.max(0, remaining));

      if (batch.length === 0) {
        throw new Error(
          `A session may contain at most ${CLASSROOM_PHOTO_MAX_IMAGES_PER_SESSION} classroom images.`,
        );
      }

      // Lightweight validate only — do not re-encode (canvas path was failing silently for some JPEGs).
      const prepared: File[] = [];
      for (const file of batch) {
        const ready = await prepareClassroomUploadFile(file);
        prepared.push(ready.file);
      }

      await onSelectFiles(prepared, {
        acquisitionMethod: "Upload",
        captureTimestampUtc: new Date().toISOString(),
        captureDevice: device,
      });
      setShowAcquisition(false);
      onNotify?.(`Uploaded ${prepared.length} classroom photo(s).`, "success");
    } catch (error) {
      const message = error instanceof Error ? error.message : "Unable to upload image.";
      setPrepareError(message);
      onNotify?.(message, "error");
      throw error instanceof Error ? error : new Error(message);
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
    setPreparing(true);
    try {
      await prepareAndUploadOne(frame.file, {
        acquisitionMethod,
        captureTimestampUtc: frame.capturedAt.toISOString(),
        blurScore: frame.blurScore ?? undefined,
        latitude: location?.latitude,
        longitude: location?.longitude,
        captureDevice: typeof navigator !== "undefined" ? navigator.userAgent.slice(0, 100) : undefined,
        orientation: 1,
      });
      setShowAcquisition(false);
    } catch (error) {
      setPrepareError(error instanceof Error ? error.message : "Unable to prepare capture.");
    } finally {
      setPreparing(false);
    }
  };

  const submitCapturedFrames = async (
    frames: CapturedFrame[],
    location: { latitude: number; longitude: number } | null,
  ) => {
    setPrepareError(null);
    setPreparing(true);
    try {
      const remaining = CLASSROOM_PHOTO_MAX_IMAGES_PER_SESSION - images.length;
      const batch = frames.slice(0, Math.max(0, remaining));
      for (const frame of batch) {
        await prepareAndUploadOne(frame.file, {
          acquisitionMethod: "CameraMultiCapture",
          captureTimestampUtc: frame.capturedAt.toISOString(),
          blurScore: frame.blurScore ?? undefined,
          latitude: location?.latitude,
          longitude: location?.longitude,
          captureDevice:
            typeof navigator !== "undefined" ? navigator.userAgent.slice(0, 100) : undefined,
          orientation: 1,
        });
      }
      setShowAcquisition(false);
    } catch (error) {
      setPrepareError(error instanceof Error ? error.message : "Unable to prepare captures.");
    } finally {
      setPreparing(false);
    }
  };

  const replaceWithProcessed = async (imageId: string, file: File) => {
    setPrepareError(null);
    setPreparing(true);
    try {
      const ready = await prepareClassroomUploadFile(file);
      await onReplaceImage(imageId, ready.file, {
        acquisitionMethod: "Upload",
        captureTimestampUtc: new Date().toISOString(),
        captureDevice: typeof navigator !== "undefined" ? navigator.userAgent.slice(0, 100) : undefined,
      });
    } catch (error) {
      setPrepareError(error instanceof Error ? error.message : "Unable to replace image.");
    } finally {
      setPreparing(false);
    }
  };

  return (
    <Stack spacing={2}>
      {hasCollection && (
        <ClassroomPhotoCollectionPanel
          images={images}
          sessionId={sessionId}
          detectedFaces={detectedFaces}
          disabled={disabled}
          busy={panelBusy}
          uploadProgress={
            showProgress && uploadState.previewStatus !== PreviewStatus.None
              ? uploadState.progress
              : isUploading
                ? uploadState.progress
                : null
          }
          canAddMore={canAddMore}
          onAddMore={() => setShowAcquisition(true)}
          onDelete={onDeleteImage}
          onDeleteAll={onDeleteAllImages}
          onReplaceAll={() => setShowAcquisition(true)}
          onReplace={(imageId, file) => void replaceWithProcessed(imageId, file)}
          onReorder={onReorderImages}
          onRetryRecognition={onRetryRecognition}
          onRetryImageRecognition={onRetryImageRecognition}
          onRetryFailedUpload={onRetry}
          showRetryUpload={uploadState.uploadStatus === UploadStatus.Failed}
          onNotify={onNotify}
        />
      )}

      {showAddUi && (
        <>
          <PhotoAcquisitionMethodTabs
            value={method}
            onChange={(next) => {
              setPrepareError(null);
              setMethod(next);
            }}
            disabled={panelBusy || disabled || !canAddMore}
          />

          {method === "upload" ? (
            <ClassroomPhotoDropZone
              disabled={disabled || !canAddMore}
              busy={panelBusy}
              busyLabel={
                preparing
                  ? "Preparing classroom photo…"
                  : isUploading
                    ? "Uploading classroom photo…"
                    : null
              }
              error={prepareError ?? collectionError ?? uploadState.errorMessage}
              multiple
              remainingSlots={CLASSROOM_PHOTO_MAX_IMAGES_PER_SESSION - images.length}
              onSelectFile={(file) => submitUploadFiles([file])}
              onSelectFiles={(files) => submitUploadFiles(files)}
            />
          ) : (
            <Stack spacing={1.5}>
              <Typography variant="subtitle1" component="h3" sx={{ fontWeight: 600 }}>
                Classroom Photo
              </Typography>
              <CameraCapturePanel
                mode={method}
                disabled={disabled || !canAddMore}
                busy={panelBusy}
                onNotify={onNotify}
                onConfirmSingle={(frame, location) =>
                  void submitCapturedFrame(frame, "CameraCapture", location)
                }
                onConfirmMultiSelection={(frame, location) =>
                  void submitCapturedFrame(frame, "CameraMultiCapture", location)
                }
                onConfirmMultiAll={(frames, location) => void submitCapturedFrames(frames, location)}
              />
              {(prepareError || collectionError || uploadState.errorMessage) && (
                <Typography variant="body2" color="error" role="alert">
                  {prepareError ?? collectionError ?? uploadState.errorMessage}
                </Typography>
              )}
            </Stack>
          )}

          {hasCollection && (
            <Button variant="text" onClick={() => setShowAcquisition(false)} disabled={panelBusy}>
              Done adding photos
            </Button>
          )}
        </>
      )}

      {!showAddUi && (prepareError || collectionError) && (
        <Typography variant="body2" color="error" role="alert">
          {prepareError ?? collectionError}
        </Typography>
      )}

      {retryLabel && (
        <Typography variant="body2" color="warning.main" role="status">
          {retryLabel}
        </Typography>
      )}

      {showProgress && !hasCollection && (
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

      {uploadState.uploadStatus === UploadStatus.Failed && onRetry && !hasCollection && (
        <Button variant="outlined" onClick={() => void onRetry()} disabled={disabled} aria-label="Retry upload">
          Retry
        </Button>
      )}
    </Stack>
  );
};

export default ClassroomPhotoUpload;
