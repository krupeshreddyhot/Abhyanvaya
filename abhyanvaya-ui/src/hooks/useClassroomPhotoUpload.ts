import { useCallback, useEffect, useRef, useState } from "react";
import {
  createPhotoAttendanceSession,
  deleteClassroomImage,
  listClassroomImages,
  reorderClassroomImages,
  replaceClassroomImage,
  requeueClassroomImage,
  requeueClassroomRecognition,
  uploadClassroomPhoto,
} from "../services/attendanceSessionService";
import type { AttendanceContext } from "../types/attendanceContext";
import type { ClassroomPhotoCaptureContext } from "../types/photoAcquisition";
import type { AttendanceSessionImage } from "../types/sessionImage";
import { MAX_CLASSROOM_IMAGES_PER_SESSION } from "../types/sessionImage";
import { type AiAttendanceState } from "../types/aiAttendanceState";
import { UploadStatus } from "../types/uploadState";
import { getApiErrorMessage } from "../utils/apiErrorMessage";
import { useUploadState } from "./useUploadState";
import { AIStatus, AIWorkflowStep } from "../types/aiWorkflow";
import { CLASSROOM_PHOTO_MAX_IMAGES_PER_SESSION } from "../constants/classroomPhotoConstraints";

export type UseClassroomPhotoUploadOptions = {
  context: AttendanceContext;
  totalStudents: number;
  aiState: AiAttendanceState;
  setAiState: React.Dispatch<React.SetStateAction<AiAttendanceState>>;
};

export const useClassroomPhotoUpload = ({
  context,
  totalStudents,
  aiState,
  setAiState,
}: UseClassroomPhotoUploadOptions) => {
  const abortRef = useRef<AbortController | null>(null);
  const sessionIdRef = useRef<string | undefined>(aiState.attendanceSessionId);
  const captureContextRef = useRef<ClassroomPhotoCaptureContext | undefined>(undefined);
  const lastFailedFileRef = useRef<{
    file: File;
    captureContext?: ClassroomPhotoCaptureContext;
  } | null>(null);
  const [images, setImages] = useState<AttendanceSessionImage[]>([]);
  const [collectionError, setCollectionError] = useState<string | null>(null);

  const {
    uploadState,
    selectFile,
    resetUploadState,
    setUploadProgress,
    markUploadCompleted,
    markUploadFailed,
    markUploadCancelled,
    setRetrying,
    setBytesProgress,
  } = useUploadState();

  useEffect(() => {
    sessionIdRef.current = aiState.attendanceSessionId;
  }, [aiState.attendanceSessionId]);

  useEffect(() => {
    return () => {
      abortRef.current?.abort();
      markUploadCancelled();
    };
  }, [markUploadCancelled]);

  const refreshImages = useCallback(async (sessionId: string) => {
    const listed = await listClassroomImages(sessionId);
    setImages(listed);
    return listed;
  }, []);

  useEffect(() => {
    const sessionId = aiState.attendanceSessionId;
    if (!sessionId) {
      setImages([]);
      return;
    }

    void refreshImages(sessionId).catch(() => {
      // Keep local collection if refresh fails mid-session.
    });
  }, [aiState.attendanceSessionId, aiState.sessionStatusCode, refreshImages]);

  const ensureSession = useCallback(async () => {
    let sessionId = sessionIdRef.current;
    if (!sessionId) {
      const created = await createPhotoAttendanceSession(context, totalStudents);
      sessionId = created.attendanceSessionId;
      sessionIdRef.current = sessionId;
      setAiState((current) => ({
        ...current,
        attendanceSessionId: sessionId,
        status: AIStatus.Uploading,
      }));
    }
    return sessionId;
  }, [context, setAiState, totalStudents]);

  const runUpload = useCallback(
    async (file: File, captureContext?: ClassroomPhotoCaptureContext) => {
      abortRef.current?.abort();
      const controller = new AbortController();
      abortRef.current = controller;
      captureContextRef.current = captureContext;
      lastFailedFileRef.current = { file, captureContext };
      setCollectionError(null);

      setAiState((current) => ({
        ...current,
        status: AIStatus.Uploading,
        workflowStep: AIWorkflowStep.Upload,
        uploadProgress: 0,
      }));

      try {
        const sessionId = await ensureSession();

        if (images.length >= CLASSROOM_PHOTO_MAX_IMAGES_PER_SESSION) {
          throw new Error(
            `A session may contain at most ${CLASSROOM_PHOTO_MAX_IMAGES_PER_SESSION} classroom images.`,
          );
        }

        const result = await uploadClassroomPhoto(sessionId, file, {
          signal: controller.signal,
          captureContext,
          onProgress: ({ milestone, loaded, total }) => {
            setBytesProgress(loaded, total);
            setUploadProgress(milestone);
            setAiState((current) => ({
              ...current,
              uploadProgress: milestone,
              status: AIStatus.Uploading,
            }));
          },
          onRetryAttempt: (attempt) => {
            setRetrying(attempt);
          },
        });

        markUploadCompleted();
        lastFailedFileRef.current = null;
        const listed = await refreshImages(result.attendanceSessionId);

        setAiState((current) => ({
          ...current,
          attendanceSessionId: result.attendanceSessionId,
          uploadedImageUrl: result.imageUrl ?? listed[0]?.imageUrl ?? current.uploadedImageUrl,
          uploadProgress: 100,
          status: AIStatus.Pending,
          workflowStep: AIWorkflowStep.Upload,
          recognitionQueued: result.queued,
        }));
      } catch (error) {
        if (controller.signal.aborted) {
          markUploadCancelled();
          setAiState((current) => ({
            ...current,
            status: AIStatus.Ready,
          }));
          return;
        }

        const message = getApiErrorMessage(error, "Upload failed.");
        setCollectionError(message);
        markUploadFailed(message);
        setAiState((current) => ({
          ...current,
          status: AIStatus.Failed,
        }));
      }
    },
    [
      ensureSession,
      images.length,
      markUploadCancelled,
      markUploadCompleted,
      markUploadFailed,
      refreshImages,
      setAiState,
      setBytesProgress,
      setRetrying,
      setUploadProgress,
    ],
  );

  const handleSelectFile = useCallback(
    async (file: File, captureContext?: ClassroomPhotoCaptureContext) => {
      try {
        await selectFile(file);
      } catch {
        return;
      }

      await runUpload(file, captureContext);
    },
    [runUpload, selectFile],
  );

  const handleSelectFiles = useCallback(
    async (files: File[], captureContext?: ClassroomPhotoCaptureContext) => {
      const remaining = CLASSROOM_PHOTO_MAX_IMAGES_PER_SESSION - images.length;
      if (remaining <= 0) {
        setCollectionError(
          `A session may contain at most ${CLASSROOM_PHOTO_MAX_IMAGES_PER_SESSION} classroom images.`,
        );
        return;
      }

      const batch = files.slice(0, remaining);
      for (const file of batch) {
        await handleSelectFile(file, captureContext);
      }
    },
    [handleSelectFile, images.length],
  );

  const retryUpload = useCallback(async () => {
    const pending = lastFailedFileRef.current;
    if (!pending && !uploadState.selectedFile) {
      return;
    }

    await runUpload(
      pending?.file ?? uploadState.selectedFile!,
      pending?.captureContext ?? captureContextRef.current,
    );
  }, [runUpload, uploadState.selectedFile]);

  const handleDeleteImage = useCallback(
    async (imageId: string) => {
      const sessionId = sessionIdRef.current;
      if (!sessionId) {
        return;
      }

      setCollectionError(null);
      try {
        await deleteClassroomImage(sessionId, imageId);
        const listed = await refreshImages(sessionId);
        if (listed.length === 0) {
          resetUploadState();
          setAiState((current) => ({
            ...current,
            uploadedImageUrl: undefined,
            recognitionQueued: false,
            status: AIStatus.Ready,
          }));
        } else {
          setAiState((current) => ({
            ...current,
            uploadedImageUrl: listed[0]?.imageUrl ?? current.uploadedImageUrl,
            recognitionQueued: true,
            status: AIStatus.Pending,
          }));
        }
      } catch (error) {
        setCollectionError(getApiErrorMessage(error, "Unable to delete image."));
      }
    },
    [refreshImages, resetUploadState, setAiState],
  );

  const handleDeleteAllImages = useCallback(async () => {
    const sessionId = sessionIdRef.current;
    if (!sessionId || images.length === 0) {
      return;
    }

    setCollectionError(null);
    try {
      const ids = images.map((image) => image.id);
      for (const imageId of ids) {
        await deleteClassroomImage(sessionId, imageId);
      }
      await refreshImages(sessionId);
      resetUploadState();
      setAiState((current) => ({
        ...current,
        uploadedImageUrl: undefined,
        recognitionQueued: false,
        status: AIStatus.Ready,
      }));
    } catch (error) {
      setCollectionError(getApiErrorMessage(error, "Unable to delete all images."));
      const session = sessionIdRef.current;
      if (session) {
        await refreshImages(session).catch(() => undefined);
      }
    }
  }, [images, refreshImages, resetUploadState, setAiState]);

  const handleReplaceImage = useCallback(
    async (imageId: string, file: File, captureContext?: ClassroomPhotoCaptureContext) => {
      const sessionId = sessionIdRef.current;
      if (!sessionId) {
        return;
      }

      abortRef.current?.abort();
      const controller = new AbortController();
      abortRef.current = controller;
      setCollectionError(null);

      setAiState((current) => ({
        ...current,
        status: AIStatus.Uploading,
        workflowStep: AIWorkflowStep.Upload,
      }));

      try {
        await selectFile(file);
        const result = await replaceClassroomImage(sessionId, imageId, file, {
          signal: controller.signal,
          captureContext,
          onProgress: ({ milestone, loaded, total }) => {
            setBytesProgress(loaded, total);
            setUploadProgress(milestone);
          },
          onRetryAttempt: setRetrying,
        });

        markUploadCompleted();
        const listed = await refreshImages(sessionId);
        setAiState((current) => ({
          ...current,
          uploadedImageUrl: result.image.imageUrl ?? listed[0]?.imageUrl ?? current.uploadedImageUrl,
          uploadProgress: 100,
          status: AIStatus.Pending,
          recognitionQueued: result.queued,
        }));
      } catch (error) {
        if (controller.signal.aborted) {
          markUploadCancelled();
          return;
        }
        const message = getApiErrorMessage(error, "Replace failed.");
        setCollectionError(message);
        markUploadFailed(message);
        setAiState((current) => ({ ...current, status: AIStatus.Failed }));
      }
    },
    [
      markUploadCancelled,
      markUploadCompleted,
      markUploadFailed,
      refreshImages,
      selectFile,
      setAiState,
      setBytesProgress,
      setRetrying,
      setUploadProgress,
    ],
  );

  const handleReorderImages = useCallback(
    async (orderedIds: string[]) => {
      const sessionId = sessionIdRef.current;
      if (!sessionId) {
        return;
      }

      setCollectionError(null);
      const previous = images;
      setImages((current) => {
        const map = new Map(current.map((image) => [image.id, image]));
        return orderedIds
          .map((id, index) => {
            const image = map.get(id);
            return image ? { ...image, imageSequence: index + 1 } : null;
          })
          .filter((image): image is AttendanceSessionImage => image != null);
      });

      try {
        const listed = await reorderClassroomImages(sessionId, orderedIds);
        setImages(listed);
        setAiState((current) => ({
          ...current,
          recognitionQueued: true,
          status: AIStatus.Pending,
        }));
      } catch (error) {
        setImages(previous);
        setCollectionError(getApiErrorMessage(error, "Unable to reorder images."));
      }
    },
    [images, setAiState],
  );

  const handleRetryRecognition = useCallback(async () => {
    const sessionId = sessionIdRef.current;
    if (!sessionId) {
      return;
    }

    setCollectionError(null);
    try {
      await requeueClassroomRecognition(sessionId);
      setAiState((current) => ({
        ...current,
        recognitionQueued: true,
        status: AIStatus.Pending,
        workflowStep: AIWorkflowStep.Upload,
      }));
    } catch (error) {
      setCollectionError(getApiErrorMessage(error, "Unable to requeue recognition."));
    }
  }, [setAiState]);

  const handleRetryImageRecognition = useCallback(
    async (imageId: string) => {
      const sessionId = sessionIdRef.current;
      if (!sessionId) {
        return;
      }

      setCollectionError(null);
      try {
        await requeueClassroomImage(sessionId, imageId);
        await refreshImages(sessionId);
        setAiState((current) => ({
          ...current,
          recognitionQueued: true,
          status: AIStatus.Pending,
          workflowStep: AIWorkflowStep.Upload,
        }));
      } catch (error) {
        setCollectionError(getApiErrorMessage(error, "Unable to retry recognition for this image."));
      }
    },
    [refreshImages, setAiState],
  );

  const resetCollection = useCallback(() => {
    resetUploadState();
    setImages([]);
    setCollectionError(null);
    lastFailedFileRef.current = null;
  }, [resetUploadState]);

  const isUploading =
    uploadState.uploadStatus === UploadStatus.Uploading ||
    uploadState.uploadStatus === UploadStatus.Retrying ||
    aiState.status === AIStatus.Uploading;

  const canAddMore = images.length < MAX_CLASSROOM_IMAGES_PER_SESSION;

  return {
    uploadState,
    images,
    collectionError,
    canAddMore,
    handleSelectFile,
    handleSelectFiles,
    handleDeleteImage,
    handleDeleteAllImages,
    handleReplaceImage,
    handleReorderImages,
    handleRetryRecognition,
    handleRetryImageRecognition,
    retryUpload,
    resetUploadState: resetCollection,
    isUploading,
    refreshImages,
    sessionId: aiState.attendanceSessionId,
  };
};

export default useClassroomPhotoUpload;
