import { useCallback, useEffect, useRef } from "react";
import {
  createPhotoAttendanceSession,
  uploadClassroomPhoto,
} from "../services/attendanceSessionService";
import type { AttendanceContext } from "../types/attendanceContext";
import type { ClassroomPhotoCaptureContext } from "../types/photoAcquisition";
import { type AiAttendanceState } from "../types/aiAttendanceState";
import { UploadStatus } from "../types/uploadState";
import { getApiErrorMessage } from "../utils/apiErrorMessage";
import { useUploadState } from "./useUploadState";
import { AIStatus, AIWorkflowStep } from "../types/aiWorkflow";

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

  const runUpload = useCallback(
    async (file: File, captureContext?: ClassroomPhotoCaptureContext) => {
      abortRef.current?.abort();
      const controller = new AbortController();
      abortRef.current = controller;
      captureContextRef.current = captureContext;

      setAiState((current) => ({
        ...current,
        status: AIStatus.Uploading,
        workflowStep: AIWorkflowStep.Upload,
        uploadProgress: 0,
      }));

      try {
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

        setAiState((current) => ({
          ...current,
          attendanceSessionId: result.attendanceSessionId,
          uploadedImageUrl: result.imageUrl ?? current.uploadedImageUrl,
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
        markUploadFailed(message);
        setAiState((current) => ({
          ...current,
          status: AIStatus.Failed,
        }));
      }
    },
    [
      context,
      markUploadCancelled,
      markUploadCompleted,
      markUploadFailed,
      setAiState,
      setBytesProgress,
      setRetrying,
      setUploadProgress,
      totalStudents,
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

  const retryUpload = useCallback(async () => {
    if (!uploadState.selectedFile) {
      return;
    }

    await runUpload(uploadState.selectedFile, captureContextRef.current);
  }, [runUpload, uploadState.selectedFile]);

  const isUploading =
    uploadState.uploadStatus === UploadStatus.Uploading ||
    uploadState.uploadStatus === UploadStatus.Retrying ||
    aiState.status === AIStatus.Uploading;

  return {
    uploadState,
    handleSelectFile,
    retryUpload,
    resetUploadState,
    isUploading,
  };
};

export default useClassroomPhotoUpload;
